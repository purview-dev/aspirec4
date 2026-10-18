namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Resolves the two directories the integration works with: where the generated model is written,
/// and which directory the LikeC4 server is pointed at.
/// </summary>
/// <remarks>
/// They are the same directory unless <see cref="AspireC4DiagramOptions.ProjectDirectory"/> names an
/// existing LikeC4 project, in which case that project is served and the generated model is written
/// into a subfolder of it. Shared by the builder and the lifecycle hook so the container's bind mount
/// and the file actually written never disagree.
/// </remarks>
static class LikeC4ProjectLayout
{
	/// <summary>Subfolder of a hand-authored project that generated output is written into.</summary>
	internal const string GeneratedSubfolder = ".aspirec4";

	/// <summary>The config file a hand-authored project uses to configure itself.</summary>
	internal const string ConfigFileName = "likec4.config.json";

	/// <summary>Resolves the generated-output and served directories as absolute paths.</summary>
	/// <param name="baseDirectory">Directory that relative option paths are resolved against.</param>
	/// <param name="options">The diagram options to read the configured paths from.</param>
	internal static (string OutputDirectory, string ServeDirectory) Resolve(
		string baseDirectory,
		AspireC4DiagramOptions options
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
		ArgumentNullException.ThrowIfNull(options);

		var projectDirectory = string.IsNullOrWhiteSpace(options.ProjectDirectory)
			? null
			: MakeAbsolute(baseDirectory, options.ProjectDirectory);

		// An OutputDirectory the consumer set is always honoured, so only the default is reinterpreted.
		// A hand-authored project wins over the legacy layout: generated output has to live beneath the
		// directory being served, which a path outside the project would not.
		var outputDirectory = (IsDefaultOutputDirectory(options.OutputDirectory), projectDirectory) switch
		{
			(true, not null) => Path.Combine(projectDirectory, GeneratedSubfolder),
			(true, null) when options.UseLegacyLayout => MakeAbsolute(
				baseDirectory,
				AspireC4DiagramOptions.LegacyOutputDirectory
			),
			_ => MakeAbsolute(baseDirectory, options.OutputDirectory),
		};

		return (outputDirectory, projectDirectory ?? outputDirectory);
	}

	/// <summary>
	/// Whether the directory being served is a hand-authored project rather than AspireC4's own
	/// output directory.
	/// </summary>
	/// <remarks>
	/// AspireC4 generates no <c>likec4.config.json</c> for such a project. If the project has one it is
	/// the project's to own, and overwriting it would discard settings AspireC4 knows nothing about; if
	/// it has none, the config is optional to LikeC4 anyway, and writing one into a hand-curated
	/// directory is exactly the clean-up this option exists to avoid.
	/// </remarks>
	internal static bool IsHandAuthoredProject(string serveDirectory, string outputDirectory) =>
		!PathsEqual(serveDirectory, outputDirectory);

	/// <summary>
	/// Resolves a configured path to an absolute one without a trailing separator, so the two
	/// directories compare and combine consistently however they were written in configuration.
	/// </summary>
	static string MakeAbsolute(string baseDirectory, string path) =>
		Path.TrimEndingDirectorySeparator(
			Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path))
		);

	static bool PathsEqual(string left, string right) =>
		string.Equals(Normalise(left), Normalise(right), StringComparison.OrdinalIgnoreCase);

	static bool IsDefaultOutputDirectory(string? outputDirectory) =>
		string.IsNullOrWhiteSpace(outputDirectory)
		|| string.Equals(
			Normalise(outputDirectory),
			Normalise(AspireC4DiagramOptions.DefaultOutputDirectory),
			StringComparison.OrdinalIgnoreCase
		);

	static string Normalise(string path) => path.Replace('\\', '/').TrimEnd('/');
}
