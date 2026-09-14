using Aspire.Hosting.AspireC4.LikeC4.Icons;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4;

/// <summary>Configuration options for the LikeC4 diagram generation.</summary>
[AspireExport(ExposeProperties = true)]
public sealed class AspireC4DiagramOptions
{
	/// <summary>
	/// The configuration section name for these options when bound from a configuration source (e.g. appsettings.json).
	/// </summary>
	public const string SectionName = "AspireC4";

	/// <summary>
	/// The LikeC4 view identifier emitted in the generated <c>.c4</c> file
	/// (e.g. <c>view <b>index</b> { ... }</c>).
	/// Defaults to <c>"index"</c>.
	/// </summary>
	/// <remarks>
	/// Change this if the generated view ID conflicts with a hand-authored view in the same
	/// LikeC4 project.  Any <see langword="null"/> or empty value is normalised to <c>"index"</c>.
	/// If you change this, set <see cref="DefaultViewId"/> to the same value so the Aspire
	/// dashboard link still opens the correct diagram.
	/// </remarks>
	public string? GeneratedViewId { get; set; }

	/// <summary>
	/// The LikeC4 view identifier used in the <c>/view/{id}</c> URL that the Aspire dashboard
	/// links to. Defaults to <c>"index"</c> (the ID of the auto-generated view).
	/// </summary>
	/// <remarks>
	/// Override this when you want the dashboard link to open a different view — for example,
	/// a hand-authored context diagram instead of the generated overview.
	/// <para>
	/// Setting this to <see langword="null"/> or an empty string instructs the dashboard link
	/// to navigate to the root of the LikeC4 server (<c>/</c>) rather than a specific view,
	/// which is useful when you prefer the server's own landing page.
	/// </para>
	/// </remarks>
	public string? DefaultViewId { get; set; } = "index";

	/// <summary>Title shown in the generated LikeC4 application. Defaults to <see langword="null"/>, leaving LikeC4 to choose the title.</summary>
	public string? Title { get; set; }

	/// <summary>Title shown in the generated LikeC4 view. Defaults to "Architecture".</summary>
	public string ViewTitle { get; set; } = "Architecture";

	/// <summary>Optional Description shown in the generated LikeC4 view. The description supports Markdown, if the version of LikeC4 you're using supports it. Defaults to <see langword="null"/>.</summary>
	public string? ViewDescription { get; set; }

	/// <summary>
	/// Directory where the generated <c>.c4</c> file is written.
	/// Defaults to <c>./likec4/gen/</c> relative to the AppHost working directory.
	/// </summary>
	public string OutputDirectory { get; set; } = "./likec4/gen/";

	/// <summary>
	/// Name of the generated <c>.c4</c> file (without extension).
	/// Defaults to "model.gen".
	/// </summary>
	public string FileName { get; set; } = "model.gen";

	/// <summary>
	/// Disables the Hot Module Replacement (HMR) channel between the LikeC4 server and the browser, which provides
	/// dynamic/ live updates to the diagram. Defaults to <see langword="false"/> (HMR enabled).
	/// </summary>
	public bool DisableHMR { get; set; }

	/// <summary>
	/// Specifies the HMR port to use when the LikeC4 server supports configurable HMR ports (LikeC4 v1.57+). This is ignored in older versions, which always use the fixed port 24678.
	/// When set to <see langword="null"/> (default), the port is dynamically allocated by the server. Set this to a specific port number to use a fixed port instead, which may be necessary in certain environments (e.g. when using a firewall that blocks dynamic ports).
	/// </summary>
	/// <remarks>This is ignored if <see cref="DisableHMR"/> is <see langword="true"/>.</remarks>
	public int? HMRPort { get; set; }

	/// <summary>
	/// The tag of the <c>ghcr.io/likec4/likec4</c> container image to use for the live server.
	/// Defaults to <c>null</c>, which resolves to <c>"latest"</c>.
	/// Set this to a specific version (e.g. <c>"1.57"</c>) to pin the LikeC4 server version.
	/// </summary>
	/// <remarks>
	/// Ignored when <see cref="AspireC4ResourceExtensions.WithLocalCLI"/> is used.
	/// </remarks>
	public string? ContainerImageTag { get; set; }

	/// <summary>
	/// When <see langword="true"/> (default) and the container image tag resolves to
	/// <c>"latest"</c>, AspireC4 runs a throwaway container at startup to call
	/// <c>likec4 --version</c> and detect the actual version pulled by Docker.
	/// The resolved version is then used to configure version-gated container features
	/// (such as configurable HMR port mode) correctly even when a specific tag is not pinned.
	/// <para>
	/// Set to <see langword="false"/> to skip the check for faster startup, at the cost of
	/// potentially misconfiguring features that depend on knowing the exact version.
	/// The default may change to <see langword="false"/> in a future release once
	/// <c>latest</c> consistently refers to a version that supports all configurable features.
	/// </para>
	/// </summary>
	/// <remarks>
	/// Has no effect when <see cref="ContainerImageTag"/> is set to a specific version tag.
	/// </remarks>
	public bool CheckLatestImageVersion { get; set; } = true;

	/// <summary>
	/// Enables automatic icon inference for known resource types and technologies.
	/// Defaults to <see langword="true" />.
	/// </summary>
	public bool AutoIconsEnabled { get; set; } = true;

	/// <summary>
	/// When <see langword="true"/>, hides the LikeC4 server resource from the Aspire dashboard
	/// and instead surfaces the diagram URL as a link and command on all project resources.
	/// Defaults to <see langword="false"/>.
	/// </summary>
	public bool HideFromDashboard { get; set; }

	/// <summary>
	/// Display name used for the architecture diagram link and command when
	/// <see cref="HideFromDashboard"/> is <see langword="true"/>.
	/// Defaults to <c>"Architecture Diagram"</c>.
	/// </summary>
	public string DashboardLinkDisplayName { get; set; } = "Architecture Diagram";

	/// <summary>
	/// Controls the DSL syntax used to emit typed relationships in the generated <c>.c4</c> file.
	/// <list type="bullet">
	///   <item><description><see cref="LikeC4RelationshipKindSyntax.Dot"/> — <c>SOURCE .KIND TARGET</c> (default, preferred).</description></item>
	///   <item><description><see cref="LikeC4RelationshipKindSyntax.Bracket"/> — <c>SOURCE -[KIND]-&gt; TARGET</c>.</description></item>
	/// </list>
	/// </summary>
	public LikeC4RelationshipKindSyntax RelationshipKindSyntax { get; set; } = LikeC4RelationshipKindSyntax.Dot;

	/// <summary>
	/// When <see langword="true"/> (default), runs <c>npx likec4 format --files &lt;file&gt;</c>
	/// against the generated <c>.c4</c> file immediately after it is written to disk.
	/// The formatter modifies the file in-place so the on-disk copy is human-readable;
	/// the formatted content is also what gets synced to the Docker container workspace.
	/// Failures are silently ignored — the application always continues regardless of the result.
	/// Set to <see langword="false"/> to skip formatting (useful if <c>npx</c> is slow or unavailable). Default is <see langword="false"/>.
	/// </summary>
	public bool FormatGeneratedFile { get; set; }

	/// <summary>
	/// Maximum number of seconds to wait for an external process (<c>npx likec4 format</c>)
	/// to complete before killing it and moving on.
	/// This caps how long <see cref="FormatGeneratedFile"/> can block application startup —
	/// the process is killed when the timeout elapses, and the existing best-effort error
	/// handling continues normally.
	/// Defaults to <c>30</c> seconds.
	/// </summary>
	public int ExternalProcessTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Utilises GraphViz' `dot` executable for LikeC4's automatic icon inference when available on the system PATH.
	/// This can improve the accuracy of inferred icons for certain technologies (e.g. databases) and allows inference
	/// of additional icons based on container/ component relationships (e.g. inferring a database
	/// icon for a container that contains a component with a database relationship).
	/// Defaults to <see langword="true"/>. Set to <see langword="false"/> to disable and rely solely on LikeC4's built-in heuristics.
	/// </summary>
	public bool UseDotIfAvailable { get; set; } = true;

	/// <summary>
	/// Custom element kind specifications emitted in the <c>specification { }</c> block.
	/// Each entry may include optional style tokens (shape, color, icon, border, opacity),
	/// a notation string, and a default technology label.
	/// <para>
	/// These are additive — kinds listed here but not present in the model are still declared.
	/// When a kind in the model matches an entry here, the full body (style, notation, technology)
	/// is emitted rather than a bare <c>element KIND</c> line.
	/// </para>
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	public List<LikeC4ElementKindSpec> ElementKindSpecs { get; set; } = [];

	/// <summary>
	/// Custom relationship kind specifications emitted in the <c>specification { }</c> block.
	/// Each entry may include an optional technology label.
	/// <para>
	/// These are additive — kinds listed here but not present in the model are still declared.
	/// When a kind in the model matches an entry here with a technology, the full body is emitted
	/// rather than a bare <c>relationship KIND</c> line.
	/// </para>
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	public List<LikeC4RelationshipKindSpec> RelationshipKindSpecs { get; set; } = [];

	/// <summary>
	/// Controls which Aspire runtime metadata is automatically injected into generated LikeC4 elements.
	/// When <see cref="AspireMetadataInclusion.Metadata"/> is set, each element receives
	/// <c>aspire-name</c> and <c>aspire-type</c> metadata entries.
	/// When <see cref="AspireMetadataInclusion.Links"/> is set, allocated HTTP/HTTPS endpoint URLs
	/// are added as element links.
	/// Defaults to <see cref="AspireMetadataInclusion.All"/>.
	/// </summary>
	public AspireMetadataInclusion AutoIncludeAspireMetadata { get; set; } = AspireMetadataInclusion.All;

	/// <summary>
	/// Controls how invalid characters in LikeC4 metadata keys are handled when building the diagram model.
	/// Valid metadata key characters are letters, digits, hyphens (<c>-</c>), and underscores (<c>_</c>).
	/// Defaults to <see cref="NormaliseMetadataBehaviour.Normalise"/>.
	/// </summary>
	public NormaliseMetadataBehaviour NormaliseMetadataBehaviour { get; set; } = NormaliseMetadataBehaviour.Normalise;

	/// <summary>
	/// Additional user-managed <c>.c4</c> source files that are copied into the output directory
	/// (and synced to the Docker volume when in container mode) alongside the generated model file.
	/// LikeC4 automatically discovers all <c>.c4</c> files in the project directory, so these files
	/// are included in the diagram without any further configuration.
	/// </summary>
	/// <remarks>
	/// Each entry should be an absolute path or a path relative to the AppHost working directory.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	public List<string> AdditionalDSLFiles { get; set; } = [];

	/// <summary>
	/// Additional directories containing <c>.c4</c> source files to include in the LikeC4 project
	/// via the <c>include.paths</c> field in the generated <c>likec4.config.json</c>.
	/// LikeC4 recursively scans each directory for <c>.c4</c> files.
	/// </summary>
	/// <remarks>
	/// Each entry must be an absolute path to an existing directory.
	/// Use <see cref="AspireC4DiagramOptionsExtensions.WithAdditionalDSLFolder"/> to register directories;
	/// that method validates existence at call time.
	/// In Docker container mode, each folder is bind-mounted read-only into the container at
	/// a deterministic path under <c>/data/ext/</c>.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	public List<string> AdditionalDSLFolders { get; set; } = [];

	/// <summary>
	/// Image alias definitions written to the <c>imageAliases</c> section of the generated
	/// <c>likec4.config.json</c>. Each key must start with <c>@</c> and maps to an absolute
	/// path of a directory that contains image files.
	/// </summary>
	/// <remarks>
	/// Use <see cref="AspireC4DiagramOptionsExtensions.WithImageAliasFolder"/> to register aliases; that method
	/// validates that the key starts with <c>@</c> and that the directory exists at call time.
	/// In Docker container mode, each image directory is bind-mounted read-only at a deterministic
	/// path under <c>/data/img/</c>.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0028:Simplify collection initialization")]
	public Dictionary<string, string> ImageAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// When <see langword="true"/> (default), generates a <c>likec4.config.json</c> file in the
	/// output directory. The file includes the project title, any <see cref="AdditionalDSLFolders"/>
	/// as <c>include.paths</c> entries, and any <see cref="ImageAliases"/>.
	/// <para>
	/// Set to <see langword="false"/> to opt out of automatic config generation and manage
	/// <c>likec4.config.json</c> manually — useful when the output directory is already part of a
	/// hand-curated LikeC4 project with its own config.
	/// </para>
	/// </summary>
	public bool GenerateConfigFile { get; set; } = true;

	/// <summary>
	/// When <see langword="true"/>, adds links from each LikeC4 element back to the Aspire dashboard
	/// console logs and structured logs pages for that resource. The links are constructed at runtime
	/// once the Aspire dashboard URL is discovered.
	///
	/// WARNING: The Aspire browser token is included in the generated links when <see cref="IncludeAspireTokenInDashboardLinks"/> is <see langword="true"/>, which may pose a security risk if the generated diagram file is shared or stored in an insecure location/ source control. See the remarks on <see cref="IncludeAspireTokenInDashboardLinks"/> for details.
	/// Requires <see cref="AutoIncludeAspireMetadata"/> to include <see cref="AspireMetadataInclusion.Links"/>.
	/// Defaults to <see langword="true"/>.
	/// </summary>
	public bool IncludeAspireDashboardLinks { get; set; } = true;

	/// <summary>
	/// When <see langword="true"/>, includes the Aspire browser token in the dashboard links when
	/// <see cref="IncludeAspireDashboardLinks"/> is <see langword="true"/>. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	///	/// **THIS IS A POTENTIAL SECURITY RISK**
	///
	/// Only enable this if you understand the implications of exposing the browser token in the generated diagram file, which is typically served on a local development machine but may be accessible to other users or processes depending on your environment and file permissions. The token grants access to the Aspire dashboard with the same permissions as the browser session, so treat it with the same level of confidentiality as any other authentication credential.
	///
	/// If this is enabled, consider EXCLUDING the generated diagram file from source control and sharing it only over secure channels. If you are using the dashboard links for quick access during development, it's generally safer to keep this disabled and navigate to the dashboard manually to access the resource links, rather than embedding the token in the diagram.
	///
	/// This is only effective when <see cref="IncludeAspireDashboardLinks"/> is <see langword="true"/>.
	/// </remarks>
	public bool IncludeAspireTokenInDashboardLinks { get; set; }

	/// <summary>
	/// Optional overrides mapping a <see cref="KnownResourceStates"/> string to the tag applied
	/// to the corresponding element in the generated diagram. Set a state's value to
	/// <see langword="null"/> to suppress tag assignment for that state.
	/// When a state has no entry in this map, the tag is automatically derived as
	/// <c>$"aspire-run-state-{state.ToLowerInvariant()}"</c>.
	/// </summary>
	/// <remarks>
	/// Tags that match the <c>aspire-run-state-*</c> pattern will receive the default
	/// built-in style rules. Use <see cref="IncludeDefaultStateStyles"/> to opt out.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	public Dictionary<string, string?> StateTagMap { get; set; } = [];

	/// <summary>
	/// When <see langword="true"/>, emits <c>style element.tag = #aspire-run-state-* { }</c> rules in the
	/// generated view for each state tag that is present in the model.
	/// Set to <see langword="false"/> if you prefer to define state styles in your own DSL file.
	/// Defaults to <see langword="true"/>.
	/// </summary>
	public bool IncludeDefaultStateStyles { get; set; } = true;

	/// <summary>
	/// Exists purely for debugging/ monitoring - this will render on the internal (container or Exe-based)
	/// AspireC4Resource that serves the LikeC4 diagram.
	/// </summary>
	public bool IncludeAspireC4InternalResource { get; set; }

	/// <summary>
	/// Resource types that are automatically excluded from the generated LikeC4 diagram.
	/// Exclusion is based on the runtime type of each Aspire resource: a resource is excluded
	/// when its type is the same as, or a subclass of, any type in this set.
	/// <para>
	/// Defaults to <c>{ typeof(<see cref="ParameterResource"/>) }</c>, which suppresses password
	/// and secret parameters added via <c>.AddParameter()</c> or <c>.WithParameter()</c>.
	/// </para>
	/// <para>
	/// Use <c>opts.WithExcludedResourceType&lt;T&gt;()</c> to add a type and
	/// <c>opts.WithoutExcludedResourceType&lt;T&gt;()</c> to remove one (including the default).
	/// </para>
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2227:Collection properties should be read only",
		Justification = "Settable for configuration binding and direct assignment in user code"
	)]
	public HashSet<Type> ExcludedResourceTypes { get; set; } = [typeof(ParameterResource)];

	/// <summary>
	/// Custom icon resolvers that are evaluated before the built-in auto-icon inference.
	/// Each resolver receives a <see cref="IconResolverContext"/> and returns either
	/// a LikeC4 icon string (e.g. <c>"tech:redis"</c>) or <see langword="null"/> to defer.
	/// Resolvers are evaluated in registration order; the first non-<see langword="null"/>
	/// result wins. If all resolvers return <see langword="null"/> (or the list is empty),
	/// the built-in icon inference runs as normal.
	/// </summary>
	/// <example>
	/// <code>
	/// builder.AddAspireC4Visualization(options =>
	/// {
	///     options.IconResolvers.Add(ctx =>
	///         ctx.Resource is MyCustomResource ? "tech:dotnet" : null);
	/// });
	/// </code>
	/// </example>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	public List<Func<IconResolverContext, string?>> IconResolvers { get; } = [];

	/// <summary>
	/// When <see cref="GenerateConfigFile"/> is <see langword="true"/>, provides additional metadata to include in the generated config file.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	public Dictionary<string, string> ConfigFileMetadata { get; set; } = [];
}
