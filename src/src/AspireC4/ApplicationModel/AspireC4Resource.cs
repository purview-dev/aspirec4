namespace Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// The top-level Aspire resource for the LikeC4 visualization sidecar.
/// </summary>
/// <remarks>
/// <para>
/// By default the visualization is backed by the official <c>ghcr.io/likec4/likec4</c> Docker
/// container (<see cref="LikeC4ServerResource"/>).  Calling
/// <c>.WithLocalCLI()</c> on the returned builder replaces the container with a local
/// Node.js CLI executable (<see cref="LikeC4LocalServerResource"/>).
/// </para>
/// <para>
/// In either case the inner resource is wired up automatically; consumers interact only with
/// <c>IResourceBuilder&lt;AspireC4Resource&gt;</c> extension methods.
/// </para>
/// </remarks>
[AspireExport]
public sealed class AspireC4Resource : Resource
{
	/// <summary>The name of the HTTP endpoint exposed by the LikeC4 server.</summary>
	public const string HttpEndpointName = "http";

	/// <summary>The name of the HTTP endpoint used by LikeC4's Vite Hot Module Replacement (HMR) channel.</summary>
	public const string HMREndpointName = "http-hmr-channel";

	/// <summary>The default host port exposed by the LikeC4 server when running via the local CLI.</summary>
	public const int DefaultPort = 5173;

	/// <summary>The default host port used by LikeC4's Vite HMR channel when running via the local CLI.</summary>
	public const int DefaultHMRPort = 24678;

	string _name;

	internal AspireC4Resource(string name, string outputDirectory)
		: base(name)
	{
		_name = name;
		BaseName = name;
		OutputDirectory = outputDirectory;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Gains a <c>-likec4</c> suffix when a second diagram is registered and the two are nested under
	/// a shared parent row — see <see cref="AspireC4DiagramGrouping"/>.
	/// </remarks>
	public override string Name => _name;

	/// <summary>
	/// The name this resource was registered with, before any grouping rename.
	/// </summary>
	/// <remarks>
	/// The inner server resource name, its health-check registration and the LikeC4 DSL identifier are
	/// all derived from this rather than from <see cref="Name"/>, so they stay stable whether or not
	/// the dashboard row was renamed.
	/// </remarks>
	internal string BaseName { get; }

	/// <summary>
	/// Whether <see cref="AspireC4DiagramOptions.HideFromDashboard"/> was set when the builder ran.
	/// </summary>
	/// <remarks>
	/// Grouping is skipped when this is set: the architecture diagram has no dashboard row to nest, so
	/// a parent would be left with a single child.
	/// </remarks>
	internal bool HiddenFromDashboard { get; init; }

	/// <summary>
	/// Whether <see cref="AspireC4DiagramOptions.UseLegacyLayout"/> was set when the builder ran.
	/// </summary>
	/// <remarks>
	/// Read by the ER diagram as well, so one switch governs both diagrams' paths and dashboard names.
	/// </remarks>
	internal bool UsesLegacyLayout { get; init; }

	/// <summary>The absolute host path where the generated <c>.c4</c> file is written.</summary>
	internal string OutputDirectory { get; }

	/// <summary>
	/// The absolute host path the LikeC4 server serves as its project root.
	/// </summary>
	/// <remarks>
	/// Equal to <see cref="OutputDirectory"/> unless
	/// <see cref="AspireC4DiagramOptions.ProjectDirectory"/> points at a hand-authored LikeC4 project,
	/// in which case that project is served and the generated model is written beneath it.
	/// </remarks>
	internal string ServeDirectory { get; init; } = string.Empty;

	/// <summary>Renames the dashboard row. Only valid before the application is built.</summary>
	internal void RenameTo(string name) => _name = name;

	/// <summary>
	/// The underlying server resource — either a <see cref="LikeC4ServerResource"/> (Docker container,
	/// the default) or a <see cref="LikeC4LocalServerResource"/> (local CLI, after calling
	/// <c>.WithLocalCLI()</c>).
	/// </summary>
	internal IResource? InnerResource { get; set; }
}
