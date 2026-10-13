using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

/// <summary>
/// Generates the d2 ER diagram on startup and regenerates it when a contributing project restarts.
/// </summary>
/// <remarks>
/// Deliberately a separate subscriber from the LikeC4 hook rather than an extension of it: that hook
/// resolves its resource with <c>OfType&lt;AspireC4Resource&gt;().FirstOrDefault()</c>, and keeping the
/// two independent avoids disturbing that assumption.
/// </remarks>
sealed partial class AspireC4EfLifecycleHook(
	IOptions<AspireC4EfDiagramOptions> options,
	ResourceNotificationService resourceNotificationService,
	ResourceLoggerService resourceLoggerService
) : IDistributedApplicationEventingSubscriber
{
	readonly AspireC4EfDiagramOptions _options = options.Value;

	/// <summary>The last written body, with the timestamp header stripped, used to skip no-op writes.</summary>
	string? _lastRawBody;

	/// <inheritdoc/>
	public Task SubscribeAsync(
		IDistributedApplicationEventing eventing,
		DistributedApplicationExecutionContext executionContext,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(eventing);

		eventing.Subscribe<BeforeStartEvent>(
			async (evt, ct) =>
			{
				var resource = evt.Model.Resources.OfType<AspireC4EfResource>().FirstOrDefault();

				if (resource is null)
				{
					return;
				}

				await WriteDiagramAsync(evt.Model, resource, ct).ConfigureAwait(false);

				// The sidecar is an implementation detail: the diagram row forwards its URLs and its
				// console output, so it is the single entry worth looking at. Matches how the LikeC4
				// hook treats its own server resource.
				if (resource.InnerResource is not null)
				{
					_ = Task.Run(() => KeepHiddenAsync(resource.InnerResource, ct), ct);
					_ = Task.Run(() => ForwardInnerResourceLogsAsync(resource, ct), ct);
				}

				// Hiding the diagram too leaves the URL cross-linked onto the contributing projects as
				// the only way in, which is the whole point of the option.
				if (_options.HideFromDashboard)
				{
					_ = Task.Run(() => KeepHiddenAsync(resource, ct), ct);
				}

				// The descriptor is read from each project's build output, which may not exist on the
				// very first run after a clean. Regenerating once projects start makes that self-healing.
				_ = Task.Run(() => WatchProjectsAsync(evt.Model, resource, ct), ct);
				_ = Task.Run(() => CrossLinkAsync(evt.Model, resource, ct), ct);
				_ = Task.Run(() => PublishViewerAccessAsync(resource, ct), ct);
			}
		);

		return Task.CompletedTask;
	}

	internal async Task WriteDiagramAsync(
		DistributedApplicationModel model,
		AspireC4EfResource resource,
		CancellationToken cancellationToken
	)
	{
		var logger = resourceLoggerService.GetLogger(resource);
		var efModel = LoadModel(model);
		var files = ErdOutput.Create(efModel, _options);

		// The first file is the diagram itself; its header carries a timestamp, so comparing the
		// stripped body keeps the file off the git diff when nothing has actually changed.
		var body = StripHeader(files[0].Content);

		if (string.Equals(body, _lastRawBody, StringComparison.Ordinal))
		{
			ErdLog.Unchanged(logger);

			return;
		}

		_lastRawBody = body;

		var directory = ResolveOutputDirectory(resource);

		Directory.CreateDirectory(directory);

		foreach (var file in files)
		{
			var path = Path.Combine(directory, file.FileName);

			await File.WriteAllTextAsync(path, file.Content, cancellationToken).ConfigureAwait(false);

			// Remembered so the dashboard command knows what to open.
			if (file.FileName.Equals("index.html", StringComparison.OrdinalIgnoreCase))
			{
				_viewerPath = path;
			}

			ErdLog.Wrote(logger, path);
		}

		var tables = efModel.Contexts.Sum(c => c.Tables.Count);

		ErdLog.Generated(logger, _options.Renderer, efModel.Contexts.Count, tables);

		if (efModel.Contexts.Count == 0)
		{
			ErdLog.NoModelsFound(logger);
		}

		// The viewer loads its JavaScript from vendor/ rather than a CDN, so the dependencies ship
		// with the package and are written out here. Without this the page loads and renders nothing.
		if (_viewerPath is not null)
		{
			var written = await InteractiveViewer
				.WriteVendorAssetsAsync(directory, _options.Renderer, cancellationToken)
				.ConfigureAwait(false);

			if (written > 0)
			{
				ErdLog.ViewerDependenciesWritten(logger, written);
			}
		}

		// This resource runs no process of its own, so it has to publish its own state or the
		// dashboard leaves it Unknown with no logs.
		await resourceNotificationService
			.PublishUpdateAsync(
				resource,
				snapshot =>
					snapshot with
					{
						State = KnownResourceStates.Running,
						Properties = WithDiagramProperties(
							snapshot.Properties,
							directory,
							efModel.Contexts.Count,
							tables
						),
					}
			)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Adds or updates this diagram's properties on a snapshot, leaving every other property in place.
	/// </summary>
	/// <remarks>
	/// Replacing the collection instead would discard what Aspire itself puts there — notably
	/// <c>resource.parentName</c>, which is what the dashboard builds its resource tree from. Aspire
	/// currently re-applies that one on each publish, so the nesting survives either way, but the
	/// dependency is invisible and silent when it breaks: the
	/// <see cref="ResourceRelationshipAnnotation"/> stays intact, so the application model and every
	/// unit test still look correct while the row quietly unnests itself in the dashboard. The LikeC4
	/// hook upserts for the same reason.
	/// </remarks>
	ImmutableArray<ResourcePropertySnapshot> WithDiagramProperties(
		ImmutableArray<ResourcePropertySnapshot> properties,
		string directory,
		int contexts,
		int tables
	)
	{
		ResourcePropertySnapshot[] diagram =
		[
			new("Renderer", _options.Renderer.ToString()),
			new("Output", directory),
			new("Contexts", contexts),
			new("Tables", tables),
		];

		// Regeneration republishes on every contributing project restart, so this has to be an upsert:
		// appending unconditionally would grow the row a duplicate set per restart.
		foreach (var property in diagram)
		{
			properties = Upsert(properties, property);
		}

		return properties;
	}

	static ImmutableArray<ResourcePropertySnapshot> Upsert(
		ImmutableArray<ResourcePropertySnapshot> properties,
		ResourcePropertySnapshot property
	)
	{
		for (var i = 0; i < properties.Length; i++)
		{
			if (properties[i].Name.Equals(property.Name, StringComparison.Ordinal))
			{
				return properties.SetItem(i, property);
			}
		}

		return properties.Add(property);
	}

	/// <summary>
	/// Resolves where the diagram is written, preferring the configuration-bound options over the
	/// directory captured when the builder ran.
	/// </summary>
	/// <remarks>
	/// The sidecar's bind mount is fixed at builder time and cannot follow a configuration override, so
	/// overriding <c>OutputDirectory</c> through configuration changes where the file is written but
	/// not what the container serves. Prefer the <c>configure</c> callback unless you only need the
	/// generated file.
	/// </remarks>
	string ResolveOutputDirectory(AspireC4EfResource resource) =>
		string.IsNullOrWhiteSpace(_options.OutputDirectory) ? resource.OutputDirectory
		: Path.IsPathRooted(_options.OutputDirectory) ? _options.OutputDirectory
		: Path.GetFullPath(Path.Combine(resource.AppHostDirectory, _options.OutputDirectory));

	/// <summary>Loads and merges the EF descriptors from every opted-in project resource.</summary>
	internal static EfModel LoadModel(DistributedApplicationModel model)
	{
		List<EfModel> models = [];

		foreach (var resource in model.Resources)
		{
			if (
				!resource.TryGetLastAnnotation<AspireC4EfProjectAnnotation>(out _)
				|| !resource.TryGetLastAnnotation<IProjectMetadata>(out var metadata)
			)
			{
				continue;
			}

			models.Add(EfModelLoader.LoadFromProject(metadata.ProjectPath));
		}

		return EfModelLoader.Merge(models);
	}

	async Task WatchProjectsAsync(
		DistributedApplicationModel model,
		AspireC4EfResource resource,
		CancellationToken cancellationToken
	)
	{
		var watched = model
			.Resources.Where(r => r.TryGetLastAnnotation<AspireC4EfProjectAnnotation>(out _))
			.Select(r => r.Name)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		if (watched.Count == 0)
		{
			return;
		}

		try
		{
			await foreach (var evt in resourceNotificationService.WatchAsync(cancellationToken).ConfigureAwait(false))
			{
				if (watched.Contains(evt.Resource.Name) && evt.Snapshot.State?.Text == KnownResourceStates.Running)
				{
					await WriteDiagramAsync(model, resource, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Shutdown.
		}
	}

	/// <summary>Removes the leading comment header so generated files compare by body alone.</summary>
	internal static string StripHeader(string dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		var lines = dsl.Split('\n');
		var index = 0;

		// Each text format comments differently: d2 uses '#', Mermaid '%%', DBML '//'. JSON carries no
		// header at all, in which case nothing is stripped.
		while (index < lines.Length && IsHeaderComment(lines[index]))
		{
			index++;
		}

		return string.Join("\n", lines.Skip(index)).TrimStart('\n');
	}

	static bool IsHeaderComment(string line)
	{
		var trimmed = line.TrimStart();

		return trimmed.StartsWith('#')
			|| trimmed.StartsWith("%%", StringComparison.Ordinal)
			|| trimmed.StartsWith("//", StringComparison.Ordinal);
	}
}
