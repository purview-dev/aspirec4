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
	/// When <see langword="true"/> (default), writes a <c>.gitignore</c> into <see cref="OutputDirectory"/>
	/// listing the files this diagram generates there, and one into the image directory ignoring only the
	/// export stamps.
	/// </summary>
	/// <remarks>
	/// The diagram itself is regenerated on every run from whatever the projects were last built with,
	/// so it is build output rather than source; the exported images are the stable, reviewable part
	/// and stay visible to git. Only the files AspireC4 writes are listed, anchored to the directory, and
	/// a <c>.gitignore</c> AspireC4 did not write is never touched. Files are rewritten only when their
	/// content changes, so turning this off to commit the diagram produces a diff only when the model
	/// really changed.
	/// </remarks>
	public bool GenerateGitIgnoreFile { get; set; } = true;

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

	/// <summary>
	/// Rules that style the tables, columns and relationships matching their patterns, applied in order.
	/// </summary>
	/// <remarks>
	/// Populate with <see cref="WithStyle(Action{ErdStyleRule})"/>, <see cref="WithTableStyle"/>,
	/// <see cref="WithColumnStyle"/> or <see cref="WithTypeStyle"/>, or bind from configuration
	/// (<c>AspireC4:EntityFramework:Styles:0:Table</c>, <c>…:Styles:0:Style:Accent:Light</c>). Where
	/// several rules match one element, a later rule overrides only what it sets.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2227:Collection properties should be read only",
		Justification = "Settable for configuration binding and direct assignment in user code"
	)]
	public List<ErdStyleRule> Styles { get; set; } = [];

	/// <summary>
	/// Callbacks that adjust each table's style after the <see cref="Styles"/> rules, run in order on
	/// every regeneration.
	/// </summary>
	/// <remarks>Populate with <see cref="OnTableStyle"/>; see <see cref="ErdTableStyleContext"/>.</remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	public List<Action<ErdTableStyleContext>> TableStyleCallbacks { get; } = [];

	/// <summary>
	/// Callbacks that adjust each column's style after the <see cref="Styles"/> rules, run in order on
	/// every regeneration.
	/// </summary>
	/// <remarks>Populate with <see cref="OnColumnStyle"/>; see <see cref="ErdColumnStyleContext"/>.</remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	public List<Action<ErdColumnStyleContext>> ColumnStyleCallbacks { get; } = [];

	/// <summary>
	/// Callbacks that adjust each relationship's style after the <see cref="Styles"/> rules, run in order
	/// on every regeneration.
	/// </summary>
	/// <remarks>Populate with <see cref="OnRelationshipStyle"/>; see <see cref="ErdRelationshipStyleContext"/>.</remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	public List<Action<ErdRelationshipStyleContext>> RelationshipStyleCallbacks { get; } = [];

	/// <summary>
	/// The colours each <c>DbContext</c> is drawn in, cycled in order. Empty (the default) uses the
	/// built-in palette of six.
	/// </summary>
	/// <remarks>
	/// Used by the interactive viewer and by Mermaid's per-context fills. Populate with
	/// <see cref="WithContextPalette"/>.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2227:Collection properties should be read only",
		Justification = "Settable for configuration binding and direct assignment in user code"
	)]
	public List<ErdColour> ContextPalette { get; set; } = [];

	/// <summary>
	/// The direction relationships flow in. Defaults to <see cref="ErdLayoutDirection.Down"/>.
	/// </summary>
	/// <remarks>Applied by the interactive viewer, d2 and Mermaid.</remarks>
	public ErdLayoutDirection LayoutDirection { get; set; } = ErdLayoutDirection.Down;

	/// <summary>
	/// Pixels between neighbouring tables in the interactive viewer. Defaults to <c>72</c>.
	/// </summary>
	public int NodeSpacing { get; set; } = DefaultNodeSpacing;

	/// <summary>
	/// Pixels between a table and the tables it relates to, along <see cref="LayoutDirection"/>, in the
	/// interactive viewer. Defaults to <c>120</c>.
	/// </summary>
	/// <remarks>This gap carries the relationship labels, so too small a value puts them on the cards.</remarks>
	public int LayerSpacing { get; set; } = DefaultLayerSpacing;

	/// <summary>
	/// Pixels between the <c>DbContext</c> containers in the interactive viewer. Defaults to <c>88</c>.
	/// </summary>
	public int ContextSpacing { get; set; } = DefaultContextSpacing;

	/// <summary>
	/// How the interactive viewer draws relationships. Defaults to <see cref="ErdEdgeStyle.Bezier"/>.
	/// </summary>
	public ErdEdgeStyle EdgeStyle { get; set; } = ErdEdgeStyle.Bezier;

	/// <summary>
	/// How fast relationship lines flow in the interactive viewer, by default. Defaults to
	/// <see cref="ErdEdgeAnimation.Normal"/>.
	/// </summary>
	public ErdEdgeAnimation EdgeAnimation { get; set; } = ErdEdgeAnimation.Normal;

	/// <summary>
	/// Whether tables glide to their new place, rather than jumping, when the viewer re-arranges them —
	/// on switching what the cards show, or opening a complex value object. On by default.
	/// </summary>
	/// <remarks>Turned off by a browser that asks for reduced motion.</remarks>
	public bool AnimateTransitions { get; set; } = true;

	/// <summary>
	/// Whether hovering over a table in the interactive viewer picks out its relationships and the tables
	/// at their other end, fading the rest. On by default.
	/// </summary>
	public bool HighlightOnHover { get; set; } = true;

	/// <summary>The interactive viewer's theme, by default. Defaults to <see cref="ErdViewerTheme.Auto"/>.</summary>
	public ErdViewerTheme Theme { get; set; } = ErdViewerTheme.Auto;

	/// <summary>
	/// The pattern behind the interactive viewer's diagram, by default. Defaults to
	/// <see cref="ErdViewerBackground.Lines"/>.
	/// </summary>
	public ErdViewerBackground ViewerBackground { get; set; } = ErdViewerBackground.Lines;

	/// <summary>
	/// What each card in the interactive viewer shows, by default. Defaults to
	/// <see cref="ErdTableDisplay.Columns"/>.
	/// </summary>
	public ErdTableDisplay TableDisplay { get; set; } = ErdTableDisplay.Columns;

	/// <summary>
	/// Whether the interactive viewer opens with a legend explaining the context colours, the key badges
	/// and any badges the styles add. Off by default.
	/// </summary>
	public bool ShowLegend { get; set; }

	/// <summary>
	/// Whether hovering over a column or a table header in the interactive viewer shows its
	/// detail — CLR type, store type, length, nullability, keys, comment, description and any style tooltip.
	/// On by default.
	/// </summary>
	public bool ShowTooltips { get; set; } = true;

	/// <summary>
	/// Whether tables and columns show a description. Off by default.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The description comes from the first of <see cref="DescriptionSources"/> that has one: a
	/// <c>[Description]</c>, <c>[Display(Description = …)]</c> or <c>[DisplayName]</c> attribute, then the
	/// XML documentation summary, then the database comment.
	/// </para>
	/// <para>
	/// The interactive viewer adds a description column to each card and a line under each table name,
	/// and the reader can switch them on or off in its settings — descriptions are always sent to the
	/// viewer, and this sets whether they start shown. Mermaid and DBML add the description to the
	/// column's note, DBML to the table's note too, and d2 to the table's tooltip.
	/// </para>
	/// </remarks>
	public bool ShowDescriptions { get; set; }

	/// <summary>
	/// Where descriptions may come from. Defaults to <see cref="ErdDescriptionSources.All"/>.
	/// </summary>
	public ErdDescriptionSources DescriptionSources { get; set; } = ErdDescriptionSources.All;

	internal const int DefaultNodeSpacing = 72;
	internal const int DefaultLayerSpacing = 120;
	internal const int DefaultContextSpacing = 88;

	/// <summary>Adds a style rule, configured by the callback.</summary>
	/// <remarks>
	/// The general form, for a combination of patterns:
	/// <c>options.WithStyle(rule =&gt; rule.ForTable("Order*").ForType("decimal").WithAccent("#b45309", "#f59e0b"))</c>.
	/// </remarks>
	/// <param name="configure">Configures the rule's patterns and style.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithStyle(Action<ErdStyleRule> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		ErdStyleRule rule = new();
		configure(rule);

		return WithStyle(rule);
	}

	/// <summary>Adds a style rule.</summary>
	/// <param name="rule">The rule.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithStyle(ErdStyleRule rule)
	{
		ArgumentNullException.ThrowIfNull(rule);

		Styles.Add(rule);

		return this;
	}

	/// <summary>Styles the tables matching a pattern.</summary>
	/// <param name="table">A table or entity name pattern, such as <c>Orders</c> or <c>Audit*</c>.</param>
	/// <param name="configure">Configures the style.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithTableStyle(string table, Action<ErdElementStyle> configure) =>
		WithElementStyle(new ErdStyleRule().ForTable(table), configure);

	/// <summary>Styles the columns matching a pattern, in every table or in the tables matching another.</summary>
	/// <param name="column">A column name pattern, such as <c>*Id</c> or <c>Email</c>.</param>
	/// <param name="configure">Configures the style.</param>
	/// <param name="table">Optionally, a table pattern the columns must be in.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithColumnStyle(
		string column,
		Action<ErdElementStyle> configure,
		string? table = null
	)
	{
		var rule = new ErdStyleRule().ForColumn(column);

		return WithElementStyle(table is null ? rule : rule.ForTable(table), configure);
	}

	/// <summary>Styles the columns of a matching type, in every table or in the tables matching a pattern.</summary>
	/// <param name="type">
	/// A type pattern: a store type (<c>decimal</c>), a domain type (<c>OrderId</c>), a CLR type, or a
	/// kind of domain type (<c>enum</c>, <c>union</c>, <c>ValueObject</c>).
	/// </param>
	/// <param name="configure">Configures the style.</param>
	/// <param name="table">Optionally, a table pattern the columns must be in.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithTypeStyle(string type, Action<ErdElementStyle> configure, string? table = null)
	{
		var rule = new ErdStyleRule().ForType(type);

		return WithElementStyle(table is null ? rule : rule.ForTable(table), configure);
	}

	/// <summary>Adds a callback that adjusts each table's style, after the rules, on every regeneration.</summary>
	/// <param name="callback">The callback; change <see cref="ErdTableStyleContext.Style"/> in place.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions OnTableStyle(Action<ErdTableStyleContext> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);

		TableStyleCallbacks.Add(callback);

		return this;
	}

	/// <summary>Adds a callback that adjusts each column's style, after the rules, on every regeneration.</summary>
	/// <param name="callback">The callback; change <see cref="ErdColumnStyleContext.Style"/> in place.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions OnColumnStyle(Action<ErdColumnStyleContext> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);

		ColumnStyleCallbacks.Add(callback);

		return this;
	}

	/// <summary>
	/// Adds a callback that adjusts each relationship's style, after the rules, on every regeneration.
	/// </summary>
	/// <param name="callback">The callback; change <see cref="ErdRelationshipStyleContext.Style"/> in place.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions OnRelationshipStyle(Action<ErdRelationshipStyleContext> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);

		RelationshipStyleCallbacks.Add(callback);

		return this;
	}

	/// <summary>Replaces the colours each <c>DbContext</c> is drawn in.</summary>
	/// <remarks>Passing none restores the built-in palette.</remarks>
	/// <param name="colours">The colours, cycled in order.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithContextPalette(params ErdColour[] colours)
	{
		ArgumentNullException.ThrowIfNull(colours);

		ContextPalette = [.. colours.Where(c => c is { IsEmpty: false })];

		return this;
	}

	/// <summary>Sets how the diagram is laid out.</summary>
	/// <param name="direction">The direction relationships flow in.</param>
	/// <param name="nodeSpacing">Pixels between neighbouring tables; unchanged when omitted.</param>
	/// <param name="layerSpacing">Pixels between related tables; unchanged when omitted.</param>
	/// <param name="contextSpacing">Pixels between context containers; unchanged when omitted.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithLayout(
		ErdLayoutDirection direction,
		int? nodeSpacing = null,
		int? layerSpacing = null,
		int? contextSpacing = null
	)
	{
		LayoutDirection = direction;
		NodeSpacing = Spacing(nodeSpacing, NodeSpacing, nameof(nodeSpacing));
		LayerSpacing = Spacing(layerSpacing, LayerSpacing, nameof(layerSpacing));
		ContextSpacing = Spacing(contextSpacing, ContextSpacing, nameof(contextSpacing));

		return this;
	}

	/// <summary>Sets how the interactive viewer draws relationships.</summary>
	/// <param name="style">The edge style.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithEdgeStyle(ErdEdgeStyle style)
	{
		EdgeStyle = style;

		return this;
	}

	/// <summary>Sets the interactive viewer's animation.</summary>
	/// <param name="edges">How fast relationship lines flow.</param>
	/// <param name="transitions">Whether tables glide when re-arranged; unchanged when omitted.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithAnimation(ErdEdgeAnimation edges, bool? transitions = null)
	{
		EdgeAnimation = edges;
		AnimateTransitions = transitions ?? AnimateTransitions;

		return this;
	}

	/// <summary>Turns off every animation in the interactive viewer: flowing lines and gliding tables.</summary>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithoutAnimation()
	{
		EdgeAnimation = ErdEdgeAnimation.Off;
		AnimateTransitions = false;

		return this;
	}

	/// <summary>Sets the interactive viewer's theme, background and card display, by default.</summary>
	/// <remarks>The reader can still change each in the viewer; this is where it starts.</remarks>
	/// <param name="theme">The theme.</param>
	/// <param name="background">The background pattern; unchanged when omitted.</param>
	/// <param name="tables">What each card shows; unchanged when omitted.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithViewerDefaults(
		ErdViewerTheme theme,
		ErdViewerBackground? background = null,
		ErdTableDisplay? tables = null
	)
	{
		Theme = theme;
		ViewerBackground = background ?? ViewerBackground;
		TableDisplay = tables ?? TableDisplay;

		return this;
	}

	/// <summary>Opens the interactive viewer with its legend shown.</summary>
	/// <param name="show">Whether the legend starts shown.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfDiagramOptions WithLegend(bool show = true)
	{
		ShowLegend = show;

		return this;
	}

	/// <summary>Shows a description for each table and column, from the given sources.</summary>
	/// <param name="sources">Where descriptions may come from. Defaults to every source.</param>
	/// <returns>This options instance, for chaining.</returns>
	/// <seealso cref="ShowDescriptions"/>
	public AspireC4EfDiagramOptions WithDescriptions(ErdDescriptionSources sources = ErdDescriptionSources.All)
	{
		ShowDescriptions = sources != ErdDescriptionSources.None;
		DescriptionSources = sources;

		return this;
	}

	AspireC4EfDiagramOptions WithElementStyle(ErdStyleRule rule, Action<ErdElementStyle> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		configure(rule.Style);

		return WithStyle(rule);
	}

	static int Spacing(int? value, int current, string name)
	{
		if (value is null)
		{
			return current;
		}

		ArgumentOutOfRangeException.ThrowIfNegative(value.Value, name);

		return value.Value;
	}

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

	/// <summary>
	/// Stops AspireC4 writing a <c>.gitignore</c> beside the generated ER diagram, for a repository that
	/// commits it deliberately.
	/// </summary>
	/// <returns>This options instance, for chaining.</returns>
	/// <seealso cref="GenerateGitIgnoreFile"/>
	public AspireC4EfDiagramOptions WithoutGitIgnoreFileGeneration()
	{
		GenerateGitIgnoreFile = false;

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
