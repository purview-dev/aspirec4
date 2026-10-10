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
