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
	/// Whether every project resource in the application is scanned for <c>DbContext</c> types.
	/// </summary>
	/// <remarks>
	/// <para>
	/// On by default, so calling <see cref="AspireC4EfResourceBuilderExtensions.WithEntityFrameworkDiagram"/>
	/// alone is enough: the diagram finds every associated context with no per-project configuration.
	/// Each project's build output is scanned, and a context is recovered from either a source-generated
	/// descriptor (full fidelity) or, failing that, the assembly's metadata (approximate).
	/// </para>
	/// <para>
	/// Set to <see langword="false"/> to restrict the diagram to the projects opted in explicitly with
	/// <c>WithEntityFrameworkModel()</c>. Use <see cref="WithProjectOptInOnly"/> for that.
	/// </para>
	/// </remarks>
	public bool DiscoverAllProjects { get; set; } = true;

	/// <summary>
	/// Contexts to include. When empty, every discovered context is included.
	/// </summary>
	/// <remarks>
	/// Entries match the context's short name or its fully-qualified type name, case-insensitively.
	/// Populate with <see cref="WithIncludedContexts"/>.
	/// </remarks>
	public IList<string> IncludedContexts { get; } = [];

	/// <summary>
	/// Contexts to exclude, matched the same way as <see cref="IncludedContexts"/>. An entry here wins
	/// over an include entry.
	/// </summary>
	/// <remarks>Populate with <see cref="WithExcludedContexts"/>.</remarks>
	public IList<string> ExcludedContexts { get; } = [];

	/// <summary>
	/// How a column shows its domain type — a value object or an enum — next to the real database type.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="DomainTypeDisplay.DomainTypeFirst"/>, so a strongly-typed identifier reads
	/// <c>AssetId (guid)</c>. The database type is never discarded, whichever mode is chosen.
	/// </remarks>
	public DomainTypeDisplay DomainTypeDisplay { get; set; } = DomainTypeDisplay.DomainTypeFirst;

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

	/// <summary>Restricts the diagram to the contexts whose short or full type name is listed.</summary>
	/// <remarks>Entries are matched case-insensitively; see <see cref="IncludedContexts"/>.</remarks>
	/// <param name="contextNames">The context names to include.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithIncludedContexts(params string[] contextNames)
	{
		AddContextNames(IncludedContexts, contextNames);

		return this;
	}

	/// <summary>Removes the listed contexts from the diagram, whatever else is configured.</summary>
	/// <remarks>
	/// This is how a context shared by several projects — an <c>IdentityDbContext</c> in a common
	/// library, say — is kept out of the diagram without editing the library.
	/// </remarks>
	/// <param name="contextNames">The context names to exclude.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithExcludedContexts(params string[] contextNames)
	{
		AddContextNames(ExcludedContexts, contextNames);

		return this;
	}

	/// <summary>Restricts the diagram to the projects opted in with <c>WithEntityFrameworkModel()</c>.</summary>
	/// <remarks>Clears <see cref="DiscoverAllProjects"/>, which is on by default.</remarks>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithProjectOptInOnly()
	{
		DiscoverAllProjects = false;

		return this;
	}

	/// <summary>Sets how a column shows its domain type next to the database type.</summary>
	/// <param name="display">The display mode.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithDomainTypeDisplay(DomainTypeDisplay display)
	{
		DomainTypeDisplay = display;

		return this;
	}

	static void AddContextNames(IList<string> target, string[] contextNames)
	{
		ArgumentNullException.ThrowIfNull(contextNames);

		foreach (var name in contextNames)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(name);

			if (!target.Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				target.Add(name);
			}
		}
	}
}
