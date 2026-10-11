namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;

/// <summary>
/// Serves the generated ER diagram viewer and its exports over HTTP.
/// </summary>
/// <param name="name">The resource name.</param>
/// <remarks>
/// The interactive and Mermaid renderers write static files rather than running a server of their
/// own. Without one the dashboard has no URL to publish — a <c>file://</c> link cannot be navigated
/// to from the dashboard's own origin — so the resource never feels like a first-class Aspire
/// resource. Serving the output directory fixes that, and has the side benefit of making every
/// generated export downloadable from the same place.
/// </remarks>
public sealed class ErdViewerServerResource(string name) : ContainerResource(name)
{
	/// <summary>The default container registry.</summary>
	public const string DefaultRegistry = "docker.io";

	/// <summary>A minimal static file server.</summary>
	public const string DefaultImage = "nginx";

	/// <summary>Pinned so a regenerated AppHost does not silently change what serves the diagram.</summary>
	public const string DefaultTag = "1.29-alpine";

	/// <summary>Where nginx serves its root from.</summary>
	public const string WebRoot = "/usr/share/nginx/html";

	/// <summary>The port nginx listens on inside the container.</summary>
	public const int DefaultPort = 80;
}
