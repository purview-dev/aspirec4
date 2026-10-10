using Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
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
	ResourceNotificationService resourceNotificationService
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

				// The descriptor is read from each project's build output, which may not exist on the
				// very first run after a clean. Regenerating once projects start makes that self-healing.
				_ = Task.Run(() => WatchProjectsAsync(evt.Model, resource, ct), ct);
				_ = Task.Run(() => CrossLinkAsync(evt.Model, resource, ct), ct);
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
		var efModel = LoadModel(model);
		var d2Model = D2ModelBuilder.Build(efModel, _options);
		var dsl = D2DSLGenerator.Generate(d2Model, _options);

		// The header carries a timestamp, so comparing the stripped body keeps the file off the
		// git diff when nothing has actually changed.
		var body = StripHeader(dsl);

		if (string.Equals(body, _lastRawBody, StringComparison.Ordinal))
		{
			return;
		}

		_lastRawBody = body;

		var directory = ResolveOutputDirectory(resource);

		Directory.CreateDirectory(directory);

		var path = Path.Combine(directory, $"{_options.FileName}.d2");

		await File.WriteAllTextAsync(path, dsl, cancellationToken).ConfigureAwait(false);
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

		while (index < lines.Length && lines[index].TrimStart().StartsWith('#'))
		{
			index++;
		}

		return string.Join("\n", lines.Skip(index)).TrimStart('\n');
	}
}
