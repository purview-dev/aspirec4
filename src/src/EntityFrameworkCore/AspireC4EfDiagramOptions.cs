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
	public string OutputDirectory { get; set; } = "./erd/gen/";

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

	/// <summary>Hide the d2 sidecar from the Aspire dashboard, surfacing it as a link instead.</summary>
	public bool HideFromDashboard { get; set; }

	/// <summary>Name used for the diagram link when hidden from the dashboard.</summary>
	public string DashboardLinkDisplayName { get; set; } = "ER Diagram";
}
