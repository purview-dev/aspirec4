using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Configures the generated d2 ER diagram.
/// </summary>
/// <remarks>
/// Bound from the <c>AspireC4:EntityFramework</c> configuration section. Register through
/// <c>AddOptions&lt;AspireC4EfDiagramOptions&gt;()</c> — never <c>Options.Create</c>, which would
/// suppress later <c>Configure</c> callbacks.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class AspireC4EfDiagramOptions
{
	/// <summary>The configuration section this options class binds from.</summary>
	public const string SectionName = "AspireC4:EntityFramework";

	/// <summary>Title shown in the generated diagram.</summary>
	public string? Title { get; set; }

	/// <summary>
	/// Which renderer produces the diagram. Defaults to <see cref="ErdRenderer.Interactive"/>, the only
	/// renderer that loses nothing; the text formats are projections for publishing and interchange.
	/// </summary>
	public ErdRenderer Renderer { get; set; } = ErdRenderer.Interactive;

	/// <summary>
	/// Whether the Mermaid renderer groups entities into one <c>subgraph</c> per <c>DbContext</c>.
	/// </summary>
	/// <remarks>
	/// <c>subgraph</c> inside <c>erDiagram</c> requires Mermaid 11.17+. GitHub was confirmed to render
	/// it in October 2026. Set this to <see langword="false"/> to emit flat output for a renderer
	/// pinned to an older Mermaid.
	/// </remarks>
	public bool UseSubgraphs { get; set; } = true;

	/// <summary>Directory the generated diagram is written to, relative to the AppHost directory.</summary>
	/// <remarks>
	/// Sits beneath the same <c>.aspirec4/</c> root as the architecture diagram, so a consumer has a
	/// single directory to ignore in source control. Reverts to <c>./erd/gen/</c> when
	/// <c>AspireC4DiagramOptions.UseLegacyLayout</c> is set on the architecture diagram, unless set
	/// explicitly here.
	/// </remarks>
	public string OutputDirectory { get; set; } = DefaultOutputDirectory;

	/// <summary>
	/// The default <see cref="OutputDirectory"/>, used to detect whether a consumer chose one.
	/// </summary>
	internal const string DefaultOutputDirectory = "./.aspirec4/erd/";

	/// <summary>The <see cref="OutputDirectory"/> used before 13.6, restored by the legacy layout.</summary>
	internal const string LegacyOutputDirectory = "./erd/gen/";

	/// <summary>Whether <see cref="OutputDirectory"/> still holds the value the property initialised to.</summary>
	internal static bool IsDefaultOutputDirectory(string? outputDirectory) =>
		string.IsNullOrWhiteSpace(outputDirectory)
		|| string.Equals(
			outputDirectory.Replace('\\', '/').TrimEnd('/'),
			DefaultOutputDirectory.Replace('\\', '/').TrimEnd('/'),
			StringComparison.OrdinalIgnoreCase
		);

	/// <summary>Generated file name, without extension.</summary>
	public string FileName { get; set; } = "schema.gen";

	/// <summary>
	/// The layout engine pinned into the generated file's <c>d2-config</c> block. Defaults to
	/// <see cref="D2LayoutEngine.Elk"/> because dagre cannot target <c>sql_table</c> rows.
	/// </summary>
	public D2LayoutEngine LayoutEngine { get; set; } = D2LayoutEngine.Elk;

	/// <summary>Whether to render each column's nullability with a trailing <c>?</c>.</summary>
	public bool ShowNullability { get; set; } = true;

	/// <summary>Whether to group tables into d2 containers by database schema.</summary>
	public bool GroupBySchema { get; set; } = true;

	/// <summary>Whether to emit one container per <c>DbContext</c> when more than one is discovered.</summary>
	public bool GroupByContext { get; set; } = true;

	/// <summary>Pin the <c>d2lang/d2</c> image tag. <see langword="null"/> uses the package default.</summary>
	public string? ContainerImageTag { get; set; }

	/// <summary>
	/// Whether the file-based renderers serve their viewer over HTTP so the dashboard can link to it.
	/// </summary>
	/// <remarks>
	/// On by default: without a server there is no URL to publish, because a <c>file://</c> link
	/// cannot be navigated to from the dashboard's own origin. Set to <see langword="false"/> where no
	/// container runtime is available — the diagram is still generated, and the dashboard offers a
	/// command that opens it locally instead.
	/// </remarks>
	public bool UseViewerServer { get; set; } = true;

	/// <summary>Pin the viewer server image tag. <see langword="null"/> uses the package default.</summary>
	public string? ViewerServerImageTag { get; set; }

	/// <summary>Hide the d2 sidecar from the Aspire dashboard, surfacing it as a link instead.</summary>
	public bool HideFromDashboard { get; set; }

	/// <summary>Name used for the diagram link when hidden from the dashboard.</summary>
	public string DashboardLinkDisplayName { get; set; } = "ER Diagram";
}
