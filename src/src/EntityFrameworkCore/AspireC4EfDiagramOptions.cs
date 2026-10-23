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
	/// Whether the diagram is also written out as an image each time it changes. On by default.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The images are written beside the diagram as <c>{FileName}.svg</c> and <c>{FileName}.png</c>, so a
	/// schema can be attached to a pull request or pasted into a wiki without opening the viewer. Export
	/// runs in the background, so it never delays startup, and only when it would actually change
	/// something: a stamp beside the images records what they were rendered from, so a restart that
	/// produces the same model runs nothing at all — not even a container. It is also skipped entirely
	/// during a publish, which generates its output and exits.
	/// </para>
	/// <para>
	/// Rendering always goes through d2, whatever <see cref="Renderer"/> is in use: the interactive
	/// renderer is a browser application with no headless path, so an image of it could only be produced
	/// by driving a browser. That means export needs either the <c>d2lang/d2</c> container or a local
	/// <c>d2</c> binary (<see cref="D2Command"/>), and that the <c>{FileName}.d2</c> source is written
	/// alongside the diagram even for the other renderers. Set to <see langword="false"/>, or use
	/// <see cref="WithoutImageExport"/>, where neither is available.
	/// </para>
	/// </remarks>
	public bool ExportImages { get; set; } = true;

	/// <summary>
	/// The formats written when <see cref="ExportImages"/> is set. Defaults to both SVG and PNG.
	/// </summary>
	/// <remarks>
	/// Emptying this disables export as surely as clearing <see cref="ExportImages"/>. Populate with
	/// <see cref="WithImageExport"/>.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2227:Collection properties should be read only",
		Justification = "Settable for configuration binding and direct assignment in user code"
	)]
	public HashSet<ErdImageFormat> ImageFormats { get; set; } = [ErdImageFormat.Svg, ErdImageFormat.Png];

	/// <summary>
	/// Where exported images are written. Relative paths are resolved against the AppHost directory;
	/// <see langword="null"/> (the default) uses <c>./.aspirec4/images/</c>.
	/// </summary>
	/// <remarks>
	/// The architecture diagram writes to the same folder by default, so every image AspireC4 produces is
	/// in one place. It matters most here: the interactive renderer writes its own <c>index.html</c>,
	/// <c>app.js</c> and <c>vendor/</c> into <see cref="OutputDirectory"/>, and an image among those is
	/// easy to miss.
	/// </remarks>
	public string? ImageExportDirectory { get; set; }

	/// <summary>
	/// A local <c>d2</c> executable to render exported images with. <see langword="null"/> (the default)
	/// runs the <c>d2lang/d2</c> container instead.
	/// </summary>
	/// <remarks>
	/// Set by <see cref="AspireC4EfResourceBuilderExtensions.WithLocalD2CLI"/>, which also replaces the
	/// sidecar. Set it here to render images locally while leaving the sidecar — or the renderer's own
	/// viewer — as it is.
	/// </remarks>
	public string? D2Command { get; set; }

	/// <summary>
	/// The container runtime used to run the one-shot export container. Defaults to <c>docker</c>.
	/// </summary>
	/// <remarks>Set to <c>podman</c> where that is the runtime Aspire itself is using.</remarks>
	public string ContainerRuntimeCommand { get; set; } = "docker";

	/// <summary>Seconds to wait for an image export before abandoning it. Defaults to 120.</summary>
	public int ExportTimeoutSeconds { get; set; } = 120;

	/// <summary>
	/// Whether exported images are produced by the interactive viewer itself rather than by d2. Off by
	/// default.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The d2 export is a different diagram, not a differently-styled one: d2 draws <c>sql_table</c>
	/// shapes with its own theme and layout, while the viewer draws React Flow cards with context
	/// colours, key badges, complex-type groups and crow's-foot markers. Setting this makes the exported
	/// file identical to the one the viewer's own <b>Save as SVG / PNG</b> control produces.
	/// </para>
	/// <para>
	/// The cost is a browser: the viewer only runs in one, so this pulls the Playwright image — around
	/// 3.7 GB — and an export takes seconds rather than milliseconds. That is why it is opt-in.
	/// Applies only to <see cref="ErdRenderer.Interactive"/>, the renderer that has a viewer; every other
	/// renderer exports through d2 whatever this is set to.
	/// </para>
	/// </remarks>
	/// <seealso cref="WithViewerFidelityExport"/>
	public bool UseViewerFidelityExport { get; set; }

	/// <summary>
	/// Overrides the image used for the viewer-fidelity export, including its tag.
	/// <see langword="null"/> uses the pinned Playwright image.
	/// </summary>
	/// <remarks>
	/// The image has to supply both <c>node</c> and a chromium Playwright can drive. Pointing this at
	/// something else is how a consumer uses a mirror, or an image they have already cached.
	/// </remarks>
	public string? ViewerExportImage { get; set; }

	/// <summary>
	/// Seconds to wait for a viewer-fidelity export. Defaults to 600.
	/// </summary>
	/// <remarks>
	/// Far longer than <see cref="ExportTimeoutSeconds"/> because the first run may be pulling the
	/// browser image. Export runs in the background, so this never caps startup.
	/// </remarks>
	public int ViewerExportTimeoutSeconds { get; set; } = 600;

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

	/// <summary>Restricts image export to the listed formats, replacing the SVG and PNG default.</summary>
	/// <remarks>
	/// Also sets <see cref="ExportImages"/>, so naming a format is enough to turn export on. Passing no
	/// format turns it off, the same as <see cref="WithoutImageExport"/>.
	/// </remarks>
	/// <param name="formats">The formats to export.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithImageExport(params ErdImageFormat[] formats)
	{
		ArgumentNullException.ThrowIfNull(formats);

		ImageFormats = [.. formats];
		ExportImages = formats.Length > 0;

		return this;
	}

	/// <summary>
	/// Exports images by rendering the interactive viewer in a headless browser, so the file matches
	/// the viewer's own <b>Save as SVG / PNG</b> output exactly.
	/// </summary>
	/// <remarks>
	/// Pulls a browser image of around 3.7 GB and makes each export take seconds rather than
	/// milliseconds; see <see cref="UseViewerFidelityExport"/>. Only affects
	/// <see cref="ErdRenderer.Interactive"/>.
	/// </remarks>
	/// <param name="use">Whether to render through the viewer.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithViewerFidelityExport(bool use = true)
	{
		UseViewerFidelityExport = use;

		return this;
	}

	/// <summary>Stops the diagram being exported as an image, leaving only the generated diagram.</summary>
	/// <remarks>
	/// This is how a consumer with no container runtime and no local <c>d2</c> opts out; see
	/// <see cref="ExportImages"/> for why export needs one of the two.
	/// </remarks>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithoutImageExport()
	{
		ExportImages = false;

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
