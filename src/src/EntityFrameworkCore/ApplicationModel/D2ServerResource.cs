namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;

/// <summary>
/// The d2 sidecar container that serves the generated ER diagram.
/// </summary>
/// <param name="name">The resource name.</param>
public sealed class D2ServerResource(string name) : ContainerResource(name)
{
	/// <summary>The default container registry.</summary>
	public const string DefaultRegistry = "docker.io";

	/// <summary>
	/// The canonical d2 image for d2lang.com. Note that <c>terrastruct/d2</c> is an out-of-date fork and
	/// must not be used.
	/// </summary>
	public const string DefaultImage = "d2lang/d2";

	/// <summary>
	/// The pinned image tag. d2 v0.9.0 is the first release to bundle the open-sourced TALA engine,
	/// which — like ELK — targets individual <c>sql_table</c> rows.
	/// </summary>
	public const string DefaultTag = "v0.9.0";

	/// <summary>The directory the workspace is mounted at inside the container.</summary>
	public const string WorkspacePath = "/data";

	/// <summary>The port d2 listens on inside the container.</summary>
	public const int DefaultPort = 5174;
}
