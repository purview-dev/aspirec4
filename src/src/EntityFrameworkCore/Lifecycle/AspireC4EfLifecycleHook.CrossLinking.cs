using System.Diagnostics;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

/// <summary>
/// Links the architecture diagram and the Aspire dashboard to the generated ER diagram.
/// </summary>
sealed partial class AspireC4EfLifecycleHook
{
	const string ErDiagramUrlName = "er-diagram";

	/// <summary>The viewer written beside the diagram, once it exists.</summary>
	string? _viewerPath;

	/// <summary>
	/// Makes the diagram reachable from the dashboard.
	/// </summary>
	/// <remarks>
	/// The d2 renderer has a sidecar with a real endpoint, so its URLs are forwarded onto the wrapper
	/// exactly as the LikeC4 integration does. The other renderers write a viewer to disk with no
	/// server behind it: a <c>file://</c> URL would not be clickable from the dashboard's own origin,
	/// so a command is published instead and the AppHost opens the file itself.
	/// </remarks>
	async Task PublishViewerAccessAsync(AspireC4EfResource resource, CancellationToken cancellationToken)
	{
		if (resource.InnerResource is null)
		{
			AddOpenViewerCommand(resource);

			return;
		}

		try
		{
			await foreach (var evt in resourceNotificationService.WatchAsync(cancellationToken).ConfigureAwait(false))
			{
				if (evt.Resource.Name != resource.InnerResource.Name || evt.Snapshot.Urls.Length == 0)
				{
					continue;
				}

				await resourceNotificationService
					.PublishUpdateAsync(resource, snapshot => snapshot with { Urls = evt.Snapshot.Urls })
					.ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			// Shutdown.
		}
	}

	void AddOpenViewerCommand(AspireC4EfResource resource)
	{
		if (resource.Annotations.OfType<ResourceCommandAnnotation>().Any(a => a.Name == OpenViewerCommand))
		{
			return;
		}

		resource.Annotations.Add(
			new ResourceCommandAnnotation(
				name: OpenViewerCommand,
				displayName: _options.DashboardLinkDisplayName,
				updateState: _ =>
					_viewerPath is not null ? ResourceCommandState.Enabled : ResourceCommandState.Disabled,
				executeCommand: _ =>
				{
					var path = _viewerPath;

					if (path is null || !File.Exists(path))
					{
						return Task.FromResult(
							CommandResults.Failure($"The viewer has not been generated yet ({path ?? "no path"}).")
						);
					}

					// UseShellExecute hands the file to the OS, which opens it in the default browser.
					Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

					return Task.FromResult(CommandResults.Success());
				},
				displayDescription: "Opens the generated ER diagram in your default browser.",
				parameter: null,
				confirmationMessage: null,
				iconName: "Database",
				iconVariant: IconVariant.Filled,
				isHighlighted: true
			)
		);
	}

	const string OpenViewerCommand = "aspirec4-open-er-diagram";

	/// <summary>
	/// Waits for the d2 sidecar to publish its URL, then surfaces that URL on every contributing
	/// project — both as a dashboard link and as a link on the project's architecture-diagram element.
	/// </summary>
	/// <remarks>
	/// The architecture-diagram link is applied by mutating the resource's
	/// <see cref="LikeC4NodeDetailsAnnotation"/>. The LikeC4 hook rebuilds its model from annotations on
	/// every regeneration, so the link is picked up on the next regeneration rather than needing the two
	/// hooks to coordinate.
	/// </remarks>
	async Task CrossLinkAsync(
		DistributedApplicationModel model,
		AspireC4EfResource resource,
		CancellationToken cancellationToken
	)
	{
		var contributors = model
			.Resources.Where(r => r.TryGetLastAnnotation<AspireC4EfProjectAnnotation>(out _))
			.ToList();

		if (contributors.Count == 0)
		{
			return;
		}

		var url = await WaitForDiagramUrlAsync(resource, cancellationToken).ConfigureAwait(false);

		if (url is null)
		{
			return;
		}

		foreach (var contributor in contributors)
		{
			ApplyArchitectureDiagramLink(contributor, url);

			await resourceNotificationService
				.PublishUpdateAsync(
					contributor,
					snapshot =>
						snapshot.Urls.Any(u => u.Name == ErDiagramUrlName)
							? snapshot
							: snapshot with
							{
								Urls =
								[
									.. snapshot.Urls,
									new UrlSnapshot(ErDiagramUrlName, url, IsInternal: false)
									{
										DisplayProperties = new UrlDisplayPropertiesSnapshot(
											_options.DashboardLinkDisplayName
										),
									},
								],
							}
				)
				.ConfigureAwait(false);
		}
	}

	static void ApplyArchitectureDiagramLink(IResource resource, string url)
	{
		if (!resource.TryGetLastAnnotation<LikeC4NodeDetailsAnnotation>(out var details))
		{
			details = new LikeC4NodeDetailsAnnotation(resource.Name);
			resource.Annotations.Add(details);
		}

		if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			details.WithLink(uri, "ER diagram");
		}
	}

	/// <summary>Watches the sidecar until it reports a public URL.</summary>
	async Task<string?> WaitForDiagramUrlAsync(AspireC4EfResource resource, CancellationToken cancellationToken)
	{
		// Only the d2 renderer runs a server; the others write files opened straight from disk, so
		// there is no URL to surface and nothing to wait for.
		if (resource.InnerResource is null)
		{
			return null;
		}

		try
		{
			await foreach (var evt in resourceNotificationService.WatchAsync(cancellationToken).ConfigureAwait(false))
			{
				if (evt.Resource.Name != resource.InnerResource.Name)
				{
					continue;
				}

				var url = evt.Snapshot.Urls.FirstOrDefault(u => !u.IsInternal)?.Url;

				if (!string.IsNullOrWhiteSpace(url))
				{
					return url;
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Shutdown.
		}

		return null;
	}
}
