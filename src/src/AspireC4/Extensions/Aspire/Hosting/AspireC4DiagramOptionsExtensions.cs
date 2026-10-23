using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AspireC4.LikeC4.Icons;
using Aspire.Hosting.AspireC4.LikeC4.Models;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting;

/// <summary>
/// Fluent extension methods for configuring <see cref="AspireC4DiagramOptions"/>.
/// Allows chaining configuration in a callback passed to <c>AddAspireC4</c>, e.g.:
/// <code>
/// builder.AddAspireC4(opts => opts
///     .WithTitle("My App")
///     .WithAutoIcons(false)
/// );
/// </code>
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4DiagramOptionsExtensions
{
	/// <summary>Sets the LikeC4 view identifier emitted in the generated <c>.c4</c> file.</summary>
	/// <seealso cref="AspireC4DiagramOptions.GeneratedViewId"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithGeneratedViewId(
		[NotNull] this AspireC4DiagramOptions options,
		string? viewId
	)
	{
		options.GeneratedViewId = viewId;
		return options;
	}

	/// <summary>Sets the LikeC4 view identifier used in the Aspire dashboard link URL.</summary>
	/// <seealso cref="AspireC4DiagramOptions.DefaultViewId"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithDefaultViewId(
		[NotNull] this AspireC4DiagramOptions options,
		string? viewId
	)
	{
		options.DefaultViewId = viewId;
		return options;
	}

	/// <summary>Sets the title shown in the generated LikeC4 view.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ViewTitle"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithViewTitle([NotNull] this AspireC4DiagramOptions options, string title)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		options.ViewTitle = title;
		return options;
	}

	/// <summary>Sets the description shown in the generated LikeC4 view.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ViewDescription"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithViewDescription(
		[NotNull] this AspireC4DiagramOptions options,
		string? description
	)
	{
		options.ViewDescription = description;
		return options;
	}

	/// <summary>Sets the title shown in the generated LikeC4 hosting application.</summary>
	/// <seealso cref="AspireC4DiagramOptions.Title"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithTitle([NotNull] this AspireC4DiagramOptions options, string? title)
	{
		if (title is not null)
			ArgumentException.ThrowIfNullOrWhiteSpace(title);
		options.Title = title;
		return options;
	}

	/// <summary>Sets the output directory where the generated <c>.c4</c> file is written.</summary>
	/// <seealso cref="AspireC4DiagramOptions.OutputDirectory"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithOutputDirectory(
		[NotNull] this AspireC4DiagramOptions options,
		string outputDirectory
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
		options.OutputDirectory = outputDirectory;
		return options;
	}

	/// <summary>Sets the file name ([NotNull]this AspireC4DiagramOptions options, without extension) for the generated <c>.c4</c> file.</summary>
	/// <seealso cref="AspireC4DiagramOptions.FileName"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithFileName([NotNull] this AspireC4DiagramOptions options, string fileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		options.FileName = fileName;
		return options;
	}

	/// <summary>Disables ([NotNull]this AspireC4DiagramOptions options, or re-enables) the Hot Module Replacement channel.</summary>
	/// <seealso cref="AspireC4DiagramOptions.DisableHMR"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithHMRDisabled(
		[NotNull] this AspireC4DiagramOptions options,
		bool disabled = true
	)
	{
		options.DisableHMR = disabled;
		return options;
	}

	/// <summary>Pins the <c>ghcr.io/likec4/likec4</c> container image to a specific tag.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ContainerImageTag"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithContainerImageTag(
		[NotNull] this AspireC4DiagramOptions options,
		string? tag
	)
	{
		options.ContainerImageTag = tag;
		return options;
	}

	/// <summary>Enables or disables automatic icon inference for known resource types.</summary>
	/// <seealso cref="AspireC4DiagramOptions.AutoIconsEnabled"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithAutoIcons(
		[NotNull] this AspireC4DiagramOptions options,
		bool enabled = true
	)
	{
		options.AutoIconsEnabled = enabled;
		return options;
	}

	/// <summary>
	/// Hides the LikeC4 server resource from the Aspire dashboard and surfaces the diagram
	/// URL as a link and command on every project resource row.
	/// </summary>
	/// <seealso cref="AspireC4DiagramOptions.HideFromDashboard"/>
	/// <seealso cref="AspireC4DiagramOptions.DashboardLinkDisplayName"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithHideFromDashboard(
		[NotNull] this AspireC4DiagramOptions options,
		string displayName = "Architecture Diagram"
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
		options.HideFromDashboard = true;
		options.DashboardLinkDisplayName = displayName;
		return options;
	}

	/// <summary>Sets the DSL syntax used to emit typed relationships.</summary>
	/// <seealso cref="AspireC4DiagramOptions.RelationshipKindSyntax"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithRelationshipKindSyntax(
		[NotNull] this AspireC4DiagramOptions options,
		LikeC4RelationshipKindSyntax syntax
	)
	{
		options.RelationshipKindSyntax = syntax;
		return options;
	}

	/// <summary>Enables or disables automatic formatting of the generated <c>.c4</c> file.</summary>
	/// <seealso cref="AspireC4DiagramOptions.FormatGeneratedFile"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithFormatGeneratedFile(
		[NotNull] this AspireC4DiagramOptions options,
		bool format = true
	)
	{
		options.FormatGeneratedFile = format;
		return options;
	}

	/// <summary>
	/// Adds a custom element kind specification to the <c>specification { }</c> block.
	/// </summary>
	/// <seealso cref="AspireC4DiagramOptions.ElementKindSpecs"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithElementKindSpec(
		[NotNull] this AspireC4DiagramOptions options,
		LikeC4ElementKindSpec spec
	)
	{
		ArgumentNullException.ThrowIfNull(spec);
		options.ElementKindSpecs.Add(spec);
		return options;
	}

	/// <summary>
	/// Adds a custom relationship kind specification to the <c>specification { }</c> block.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="spec">The relationship kind specification to add.</param>
	/// <seealso cref="AspireC4DiagramOptions.RelationshipKindSpecs"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithRelationshipKindSpec(
		[NotNull] this AspireC4DiagramOptions options,
		LikeC4RelationshipKindSpec spec
	)
	{
		ArgumentNullException.ThrowIfNull(spec);
		options.RelationshipKindSpecs.Add(spec);
		return options;
	}

	/// <summary>
	/// Adds a custom relationship kind identifier to the <c>specification { }</c> block.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="name">The kind identifier, e.g. <c>"async"</c> or <c>"grpc"</c>.</param>
	/// <param name="technology">Optional default technology label for all relationships of this kind ([NotNull]this AspireC4DiagramOptions options, e.g. <c>"AMQP"</c>, <c>"gRPC"</c>).</param>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	[AspireExportIgnore]
	public static AspireC4DiagramOptions WithRelationshipKindSpec(
		[NotNull] this AspireC4DiagramOptions options,
		string name,
		string? technology = null
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		options.RelationshipKindSpecs.Add(new LikeC4RelationshipKindSpec(name, technology));
		return options;
	}

	/// <summary>Controls which Aspire runtime metadata is injected into generated LikeC4 elements.</summary>
	/// <seealso cref="AspireC4DiagramOptions.AutoIncludeAspireMetadata"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithAutoIncludeAspireMetadata(
		[NotNull] this AspireC4DiagramOptions options,
		AspireMetadataInclusion inclusion
	)
	{
		options.AutoIncludeAspireMetadata = inclusion;
		return options;
	}

	/// <summary>Controls how invalid characters in LikeC4 metadata keys are handled.</summary>
	/// <seealso cref="AspireC4DiagramOptions.NormaliseMetadataBehaviour"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithNormaliseMetadataBehaviour(
		[NotNull] this AspireC4DiagramOptions options,
		NormaliseMetadataBehaviour behaviour
	)
	{
		options.NormaliseMetadataBehaviour = behaviour;
		return options;
	}

	/// <summary>Disables automatic generation of <c>likec4.config.json</c> in the output directory.</summary>
	/// <seealso cref="AspireC4DiagramOptions.GenerateConfigFile"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithoutConfigFileGeneration([NotNull] this AspireC4DiagramOptions options)
	{
		options.GenerateConfigFile = false;
		return options;
	}

	/// <summary>Enables or disables Aspire dashboard links on generated diagram elements.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IncludeAspireDashboardLinks"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithAspireDashboardLinks(
		[NotNull] this AspireC4DiagramOptions options,
		bool include = true
	)
	{
		options.IncludeAspireDashboardLinks = include;
		return options;
	}

	/// <summary>Enables or disables using GraphViz' Dot tool if available.</summary>
	/// <seealso cref="AspireC4DiagramOptions.UseDotIfAvailable"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithUseDotIfAvailable(
		[NotNull] this AspireC4DiagramOptions options,
		bool useDotIfAvailable
	)
	{
		options.UseDotIfAvailable = useDotIfAvailable;
		return options;
	}

	/// <summary>Enables or disables embedding the Aspire browser token in dashboard links.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IncludeAspireTokenInDashboardLinks"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithAspireTokenInDashboardLinks(
		[NotNull] this AspireC4DiagramOptions options,
		bool include = true
	)
	{
		options.IncludeAspireTokenInDashboardLinks = include;
		return options;
	}

	/// <summary>Enables or disables emitting default <c>aspire-run-state-*</c> style rules in the generated view.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IncludeDefaultStateStyles"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithDefaultStateStyles(
		[NotNull] this AspireC4DiagramOptions options,
		bool include = true
	)
	{
		options.IncludeDefaultStateStyles = include;
		return options;
	}

	/// <summary>
	/// Overrides the tag applied to diagram elements for a specific Aspire resource state.
	/// The <paramref name="state"/> value should be one of the <see cref="KnownResourceStates"/> string constants.
	/// Set <paramref name="tag"/> to <see langword="null"/> to suppress tag assignment for that state.
	/// </summary>
	/// <seealso cref="AspireC4DiagramOptions.StateTagMap"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithStateTag(
		[NotNull] this AspireC4DiagramOptions options,
		string state,
		string? tag
	)
	{
		options.StateTagMap[state] = tag;
		return options;
	}

	/// <summary>Adds a custom icon resolver evaluated before built-in icon inference.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IconResolvers"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithIconResolver(
		[NotNull] this AspireC4DiagramOptions options,
		Func<IconResolverContext, string?> resolver
	)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		options.IconResolvers.Add(resolver);
		return options;
	}

	/// <summary>
	/// Adds <typeparamref name="T"/> ([NotNull]this AspireC4DiagramOptions options, and any subclass) to the set of resource types that are
	/// automatically excluded from the generated LikeC4 diagram.
	/// </summary>
	/// <typeparam name="T">
	/// The resource type to exclude. Any resource whose runtime type is <typeparamref name="T"/>
	/// or a subclass of <typeparamref name="T"/> will be omitted from the diagram.
	/// </typeparam>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	/// <seealso cref="AspireC4DiagramOptions.ExcludedResourceTypes"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithExcludedResourceType<T>([NotNull] this AspireC4DiagramOptions options)
		where T : IResource
	{
		options.ExcludedResourceTypes.Add(typeof(T));
		return options;
	}

	/// <summary>
	/// Removes <typeparamref name="T"/> from the set of resource types that are automatically
	/// excluded from the generated LikeC4 diagram, allowing resources of that type to appear.
	/// </summary>
	/// <typeparam name="T">The resource type to re-include in the diagram.</typeparam>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	/// <seealso cref="AspireC4DiagramOptions.ExcludedResourceTypes"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithoutExcludedResourceType<T>([NotNull] this AspireC4DiagramOptions options)
		where T : IResource
	{
		options.ExcludedResourceTypes.Remove(typeof(T));
		return options;
	}

	/// <summary>
	/// Enables or disables the startup version check that runs when the <c>"latest"</c>
	/// container image tag is in use.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="check">
	/// <see langword="true"/> (default) to run <c>likec4 --version</c> in a throwaway
	/// container at startup and use the resolved version to configure version-gated features;
	/// <see langword="false"/> to skip the check for faster startup.
	/// </param>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	/// <seealso cref="AspireC4DiagramOptions.CheckLatestImageVersion"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithCheckLatestImageVersion(
		[NotNull] this AspireC4DiagramOptions options,
		bool check = true
	)
	{
		options.CheckLatestImageVersion = check;
		return options;
	}

	/// <summary>
	/// Registers an image alias that maps a shorthand key (e.g. <c>"@icons"</c>) to a directory
	/// of image files, written to the <c>imageAliases</c> section of the generated
	/// <c>likec4.config.json</c>.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="aliasKey">The alias identifier, which must start with <c>@</c>.</param>
	/// <param name="folderPath">The absolute path to the image directory.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="aliasKey"/> does not start with <c>@</c>.</exception>
	/// <exception cref="DirectoryNotFoundException">Thrown if <paramref name="folderPath"/> does not exist.</exception>
	[AspireExport]
	public static AspireC4DiagramOptions WithImageAliasFolder(
		[NotNull] this AspireC4DiagramOptions options,
		string aliasKey,
		string folderPath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		if (!aliasKey.StartsWith('@'))
			throw new ArgumentException("Image alias keys must start with '@'.", nameof(aliasKey));

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The image alias folder does not exist: '{absoluteFolder}'");

		options.ImageAliases[aliasKey] = absoluteFolder;

		return options;
	}

	/// <summary>
	/// Restores the pre-13.6 generated-output paths and dashboard resource names, so an existing
	/// AppHost upgrades without changing anything else.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Generated output goes back to <c>./likec4/gen/</c> and <c>./erd/gen/</c>, and the diagram
	/// resources stay flat in the dashboard as <c>aspirec4</c> and <c>aspirec4-er</c> rather than
	/// nesting under a shared parent. One switch covers both diagrams.
	/// </para>
	/// <para>
	/// This is a compatibility shim and will be removed in a future major version. Prefer adopting the
	/// default layout — see the migration guide in the repository wiki.
	/// </para>
	/// </remarks>
	/// <param name="options"></param>
	/// <seealso cref="AspireC4DiagramOptions.UseLegacyLayout"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithLegacyLayout([NotNull] this AspireC4DiagramOptions options)
	{
		options.UseLegacyLayout = true;
		return options;
	}

	/// <summary>
	/// Points AspireC4 at an existing, hand-authored LikeC4 project: that directory is served, and
	/// the generated model joins the project's own <c>.c4</c> files rather than replacing them.
	/// </summary>
	/// <remarks>
	/// The project's <c>likec4.config.json</c>, if it has one, is used as-is and never overwritten.
	/// Unless <see cref="AspireC4DiagramOptions.OutputDirectory"/> was also set, generated output goes
	/// to <c>.aspirec4/</c> beneath the project so it stays out of the hand-curated files.
	/// </remarks>
	/// <param name="options"></param>
	/// <param name="folderPath">The path to the LikeC4 project directory.</param>
	/// <exception cref="DirectoryNotFoundException">
	/// Thrown if <paramref name="folderPath"/> does not refer to an existing directory.
	/// </exception>
	/// <seealso cref="AspireC4ResourceBuilderExtensions.WithLikeC4Element{T}"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithLikeC4ProjectFolder(
		[NotNull] this AspireC4DiagramOptions options,
		string folderPath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The LikeC4 project folder does not exist: '{absoluteFolder}'");

		options.ProjectDirectory = absoluteFolder;

		return options;
	}

	/// <summary>
	/// Registers an additional folder whose <c>.c4</c> files will be included in the LikeC4
	/// project via the <c>include.paths</c> field of the generated <c>likec4.config.json</c>.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="folderPath">The absolute path to a directory containing <c>.c4</c> source files.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	/// <exception cref="DirectoryNotFoundException">
	/// Thrown if <paramref name="folderPath"/> does not refer to an existing directory.
	/// </exception>
	[AspireExport]
	public static AspireC4DiagramOptions WithAdditionalDSLFolder(
		[NotNull] this AspireC4DiagramOptions options,
		string folderPath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The additional DSL folder does not exist: '{absoluteFolder}'");

		options.AdditionalDSLFolders.Add(absoluteFolder);

		return options;
	}

	/// <summary>
	/// Registers an additional <c>.c4</c> source file that will be copied to the LikeC4
	/// output directory alongside the auto-generated model file.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="sourcePath">
	/// The path to the source file. Relative paths are resolved from the current working directory.
	/// </param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport]
	public static AspireC4DiagramOptions WithAdditionalDSLFile(
		[NotNull] this AspireC4DiagramOptions options,
		string sourcePath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

		var absoluteSource = Path.GetFullPath(sourcePath);
		if (!File.Exists(absoluteSource))
			throw new FileNotFoundException(
				$"The additional DSL file does not exist: '{absoluteSource}'",
				absoluteSource
			);

		options.AdditionalDSLFiles.Add(absoluteSource);

		return options;
	}

	/// <summary>
	/// Enables or disables the inclusion of Aspire's internal resource definitions (e.g. <c>aspire::Container</c>, <c>aspire::Database</c>) in the generated LikeC4 model.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="include">True to include AspireC4's internal resource definitions; false to exclude them.</param>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	[AspireExport]
	public static AspireC4DiagramOptions WithIncludeAspireC4InternalResource(
		[NotNull] this AspireC4DiagramOptions options,
		bool include
	)
	{
		options.IncludeAspireC4InternalResource = include;

		return options;
	}

	/// <summary>
	/// Restricts image export to the listed formats, replacing the PNG default.
	/// </summary>
	/// <remarks>
	/// Also sets <see cref="AspireC4DiagramOptions.ExportImages"/>, so naming a format is enough to turn
	/// export on. Passing no format turns it off, the same as <see cref="WithoutImageExport"/>.
	/// </remarks>
	/// <param name="options"></param>
	/// <param name="formats">The formats to export.</param>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	/// <seealso cref="AspireC4DiagramOptions.ImageFormats"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithImageExport(
		[NotNull] this AspireC4DiagramOptions options,
		params LikeC4ImageFormat[] formats
	)
	{
		ArgumentNullException.ThrowIfNull(formats);

		options.ImageFormats = [.. formats];
		options.ExportImages = formats.Length > 0;

		return options;
	}

	/// <summary>Sets the directory exported images are written to.</summary>
	/// <param name="options"></param>
	/// <param name="directory">
	/// Absolute, or relative to <see cref="AspireC4DiagramOptions.OutputDirectory"/>.
	/// </param>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	/// <seealso cref="AspireC4DiagramOptions.ImageExportDirectory"/>
	[AspireExport]
	public static AspireC4DiagramOptions WithImageExportDirectory(
		[NotNull] this AspireC4DiagramOptions options,
		string directory
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);

		options.ImageExportDirectory = directory;

		return options;
	}

	/// <summary>
	/// Stops the views being exported as images, leaving only the generated <c>.c4</c> model.
	/// </summary>
	/// <remarks>
	/// Export drives a headless browser; see <see cref="AspireC4DiagramOptions.ExportImages"/> for what
	/// that needs. This is how a consumer who cannot, or would rather not, pay for it opts out.
	/// </remarks>
	/// <param name="options"></param>
	/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
	[AspireExport]
	public static AspireC4DiagramOptions WithoutImageExport([NotNull] this AspireC4DiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		options.ExportImages = false;

		return options;
	}
}
