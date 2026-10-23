using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
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

	/// <summary>
	/// The number of contexts in the diagram last written.
	/// </summary>
	/// <remarks>
	/// Used to refuse a regeneration that finds nothing. Replacing a good diagram with an empty one
	/// loses information that cannot be recovered from, whereas keeping the previous file self-corrects
	/// on the next successful read.
	/// </remarks>
	int _writtenContextCount;

	/// <summary>The projects that contributed at least one context to the last load.</summary>
	/// <remarks>
	/// Set by <see cref="WriteDiagramAsync"/>, which runs before cross-linking starts, so the diagram
	/// link is added only to the projects that actually appear in it.
	/// </remarks>
	IReadOnlyList<IResource> _contributors = [];

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

				// Added before the first write so the command is on the row from the outset, disabled
				// until the first export resolves a directory for it.
				if (_options.ExportImages)
				{
					AddOpenImagesCommand(resource);
				}

				var contexts = await WriteDiagramAsync(evt.Model, resource, ct).ConfigureAwait(false);

				// Backstop for the descriptor lookup. The diagram is built from each contributing
				// project's build output, which may not exist yet, and the project watch below only
				// fires when a project reaches Running — which never happens when the project runner
				// itself fails. Retrying a bounded number of times lets the diagram converge on the
				// real model instead of staying empty for the life of the application.
				if (contexts == 0)
				{
					_ = Task.Run(() => RetryUntilModelFoundAsync(evt.Model, resource, ct), ct);
				}

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
				_ = Task.Run(() => CrossLinkAsync(resource, ct), ct);
				_ = Task.Run(() => PublishViewerAccessAsync(resource, ct), ct);
			}
		);

		return Task.CompletedTask;
	}

	internal async Task<int> WriteDiagramAsync(
		DistributedApplicationModel model,
		AspireC4EfResource resource,
		CancellationToken cancellationToken
	)
	{
		var logger = resourceLoggerService.GetLogger(resource);
		var load = LoadModel(model, _options, logger);

		_contributors = load.Contributors;

		var efModel = load.Model;
		var directory = ResolveOutputDirectory(resource);

		// A pass that finds nothing must not replace a diagram that was already produced. The
		// descriptor is read from a project's build output, which is briefly unreadable while that
		// project is being rebuilt — and reading it a moment too early used to leave an empty diagram
		// on disk for the rest of the run, because the next write is then skipped as a no-op.
		if (efModel.Contexts.Count == 0 && _writtenContextCount > 0)
		{
			ErdLog.NoModelsFoundKeepingExisting(logger, directory);

			return 0;
		}

		var files = ErdOutput.Create(efModel, _options);

		// The first file is the diagram itself; its header carries a timestamp, so comparing the
		// stripped body keeps the file off the git diff when nothing has actually changed.
		var body = StripHeader(files[0].Content);

		if (string.Equals(body, _lastRawBody, StringComparison.Ordinal))
		{
			ErdLog.Unchanged(logger);

			return efModel.Contexts.Count;
		}

		_lastRawBody = body;

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

		_writtenContextCount = efModel.Contexts.Count;

		ErdLog.Generated(logger, _options.Renderer, efModel.Contexts.Count, tables);

		// A context recovered from metadata cannot show fluent configuration, so say so rather than
		// letting a partial model pass for a complete one.
		var approximate = efModel.Contexts.Count(c => c.Fidelity == EfModelFidelity.Approximate);

		if (approximate > 0)
		{
			ErdLog.ApproximateContexts(logger, approximate, efModel.Contexts.Count);
		}

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

		// Reached only when the diagram actually changed — the no-op write returns above — so this is
		// the "on detected change" gate for export as well. Fire-and-forget: rendering an image shells
		// out to d2, which must not sit between the application and its own startup.
		if (_options.ExportImages && efModel.Contexts.Count > 0)
		{
			// Resolved here rather than inside the export so the dashboard property published below is
			// already correct; the export itself races the publish and could not be relied on for it.
			_imageDirectory = ErdImageExporter.ResolveImageDirectory(_options, resource.AppHostDirectory);

			_ = Task.Run(
				() => ExportImagesAsync(efModel, directory, resource.AppHostDirectory, logger, cancellationToken),
				cancellationToken
			);
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

		return efModel.Contexts.Count;
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
			// Shown so the folder can be found without knowing the convention; the command beside it
			// opens the same path. Omitted entirely when export is off, rather than advertising a
			// directory that will stay empty.
			.. _imageDirectory is { } images ? (ResourcePropertySnapshot[])[new("Images", images)] : [],
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

	/// <summary>Loads and merges the EF descriptors from every contributing project resource.</summary>
	/// <param name="model">The application model to read the contributing projects from.</param>
	/// <param name="options">The diagram options, which select the projects and filter the contexts.</param>
	/// <param name="logger">
	/// Where to explain a project that produced no model, or <see langword="null"/> to stay silent. The
	/// backstop probe passes <see langword="null"/> so a still-failing lookup is not logged per attempt.
	/// </param>
	internal static EfModelLoad LoadModel(
		DistributedApplicationModel model,
		AspireC4EfDiagramOptions options,
		ILogger? logger = null
	)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		List<EfModel> models = [];
		List<IResource> contributors = [];

		foreach (var resource in SelectProjects(model, options))
		{
			if (!resource.TryGetLastAnnotation<IProjectMetadata>(out var metadata))
			{
				continue;
			}

			var projectModel = ApplyProjectFilters(
				EfModelLoader.LoadFromProject(metadata.ProjectPath, out var diagnostics),
				resource
			);

			models.Add(projectModel);

			if (projectModel.Contexts.Count > 0)
			{
				contributors.Add(resource);
			}
			else if (logger is not null && resource.TryGetLastAnnotation<AspireC4EfProjectAnnotation>(out _))
			{
				// Only a project the user opted in explicitly is worth warning about: under automatic
				// discovery, a project with no DbContext is entirely expected.
				ErdLog.ProjectProducedNoModel(
					logger,
					diagnostics.ProjectPath,
					diagnostics.OutputDirectory ?? "(none)",
					diagnostics.AssembliesScanned,
					diagnostics.DescriptorsFound
				);
			}
		}

		var merged = EfModelLoader.Merge(models);
		var filtered = EfContextFilter.Apply(merged, options.IncludedContexts, options.ExcludedContexts);

		return new EfModelLoad(filtered, contributors);
	}

	/// <summary>The project resources the diagram is built from.</summary>
	/// <remarks>
	/// Every project resource is a candidate by default; <c>WithProjectOptInOnly()</c> narrows it to
	/// the projects annotated with <c>WithEntityFrameworkModel()</c>.
	/// </remarks>
	static IEnumerable<IResource> SelectProjects(DistributedApplicationModel model, AspireC4EfDiagramOptions options) =>
		model.Resources.Where(resource =>
			resource.TryGetLastAnnotation<IProjectMetadata>(out _)
			&& (options.DiscoverAllProjects || resource.TryGetLastAnnotation<AspireC4EfProjectAnnotation>(out _))
		);

	/// <summary>Applies a project's own include/exclude filters to its contribution.</summary>
	static EfModel ApplyProjectFilters(EfModel model, IResource resource) =>
		resource.TryGetLastAnnotation<AspireC4EfProjectAnnotation>(out var annotation)
			? EfContextFilter.Apply(model, annotation.Options.IncludedContexts, annotation.Options.ExcludedContexts)
			: model;

	/// <summary>How many times the empty-model backstop re-reads the descriptors.</summary>
	internal const int ModelRetryAttempts = 20;

	/// <summary>How long the empty-model backstop waits between attempts.</summary>
	internal static readonly TimeSpan ModelRetryDelay = TimeSpan.FromSeconds(3);

	/// <summary>
	/// Re-reads the descriptors until a model is found, for the case where nothing else will.
	/// </summary>
	/// <remarks>
	/// The build output a descriptor is read from can appear after startup, and the project watch only
	/// fires when a contributing project reaches <c>Running</c> — which never happens when the project
	/// runner itself fails. Without this the diagram would stay empty for the life of the application.
	/// </remarks>
	async Task RetryUntilModelFoundAsync(
		DistributedApplicationModel model,
		AspireC4EfResource resource,
		CancellationToken cancellationToken
	)
	{
		try
		{
			for (var attempt = 0; attempt < ModelRetryAttempts; attempt++)
			{
				await Task.Delay(ModelRetryDelay, cancellationToken).ConfigureAwait(false);

				// Probed silently: a still-failing lookup would otherwise repeat the per-project
				// warning on every attempt.
				if (LoadModel(model, _options).Model.Contexts.Count == 0)
				{
					continue;
				}

				await WriteDiagramAsync(model, resource, cancellationToken).ConfigureAwait(false);

				return;
			}
		}
		catch (OperationCanceledException)
		{
			// Shutdown.
		}
	}

	async Task WatchProjectsAsync(
		DistributedApplicationModel model,
		AspireC4EfResource resource,
		CancellationToken cancellationToken
	)
	{
		var watched = SelectProjects(model, _options).Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

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

/// <summary>A loaded model and the project resources that contributed at least one context to it.</summary>
/// <param name="Model">The merged, filtered model.</param>
/// <param name="Contributors">The projects that appear in the diagram.</param>
readonly record struct EfModelLoad(EfModel Model, IReadOnlyList<IResource> Contributors);
