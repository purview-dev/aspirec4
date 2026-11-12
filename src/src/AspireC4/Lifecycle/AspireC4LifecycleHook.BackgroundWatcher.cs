using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// Watches for the Aspire dashboard resource to start, captures its base URL, and triggers
	/// a diagram regeneration so that dashboard deep-links are injected into the generated file.
	/// </summary>
	async Task WatchDashboardUrlAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != AspireDashboardResourceName)
					continue;
				if (notification.Snapshot.State?.Text != KnownResourceStates.Running)
					continue;

				var baseUrl = SelectDashboardBaseUrl(notification.Snapshot.Urls);
				if (baseUrl is null)
					continue;

				// Only regenerate if the URL is genuinely new.
				if (_dashboardBaseUrl == baseUrl)
					break;

				_dashboardBaseUrl = baseUrl;
				telemetry.DashboardUrlDiscovered(baseUrl);
				ScheduleRegeneration(appModel, cancellationToken);
				break;
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Selects the browser-facing Aspire dashboard base URL (<c>scheme://host:port</c>) from
	/// a resource snapshot URL list, mirroring Aspire's own <c>PopulateDashboardUrls</c> logic.
	/// </summary>
	/// <remarks>
	/// The <c>aspire-dashboard</c> resource exposes multiple non-internal HTTPS endpoints when
	/// TLS is configured — most notably the browser frontend (<c>Name = "https"</c>) and the
	/// OTLP HTTP endpoint (<c>Name = "otlp-http"</c>). Filtering by endpoint name, not by
	/// URL scheme alone, ensures the correct browser-facing URL is always selected first.
	/// </remarks>
	public static string? SelectDashboardBaseUrl(IEnumerable<UrlSnapshot> urls)
	{
		var list = urls.Where(u => !u.IsInternal).ToList();

		// Primary strategy: endpoint name "https"/"http" identifies the browser frontend.
		// This matches how Aspire itself selects the public frontend URL in PopulateDashboardUrls.
		var rawUrl = list.FirstOrDefault(u => u.Name == "https")?.Url
			?? list.FirstOrDefault(u => u.Name == "http")?.Url
			// Fallbacks for future-proofing in case endpoint names change.
			?? list.FirstOrDefault(u => u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))?.Url
			?? list.FirstOrDefault(u => u.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))?.Url;

		return rawUrl is null || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed)
			? null
			: $"{parsed.Scheme}://{parsed.Authority}";
	}

	/// <summary>
	/// Returns the Aspire browser token from configuration when
	/// <see cref="AspireC4DiagramOptions.IncludeAspireTokenInDashboardLinks"/> is <see langword="true"/>;
	/// otherwise <see langword="null"/> (token is suppressed by default).
	/// </summary>
	internal static string? ResolveAspireBrowserToken(IConfiguration configuration, AspireC4DiagramOptions options) =>
		options.IncludeAspireTokenInDashboardLinks ? configuration["AppHost:BrowserToken"] : null;
}
