namespace Aspire.Hosting.AspireC4;

/// <summary>
/// The single directory both diagrams export their images to, and how it is resolved.
/// </summary>
/// <remarks>
/// <para>
/// One folder for the whole integration rather than one per diagram: the images exist to be picked up
/// and attached to something, which is a poor fit for hunting through two generated-output directories
/// — and worse for the ER diagram, whose images would otherwise sit among the viewer's own
/// <c>index.html</c>, <c>app.js</c> and <c>vendor/</c>.
/// </para>
/// <para>
/// Resolved against the AppHost directory rather than either diagram's <c>OutputDirectory</c>, so the
/// two cannot drift apart when only one of them is overridden. Shared with the EF Core package through
/// <c>InternalsVisibleTo</c>.
/// </para>
/// </remarks>
static class AspireC4ImageExport
{
	/// <summary>Where images are written when neither diagram configures a directory.</summary>
	/// <remarks>
	/// Beneath the same <c>.aspirec4/</c> root as everything else AspireC4 generates, so a consumer
	/// still has exactly one directory to ignore in source control.
	/// </remarks>
	internal const string DefaultDirectory = "./.aspirec4/images/";

	/// <summary>
	/// Prefix applied to the architecture diagram's images.
	/// </summary>
	/// <remarks>
	/// <c>likec4 export</c> names a file after its view id, which in a folder shared with the ER
	/// diagram's images says nothing about where it came from — and an <c>index</c> view would produce a
	/// bare <c>index.png</c>. The prefix also keeps the two diagrams from colliding on a common view name.
	/// </remarks>
	internal const string ArchitecturePrefix = "architecture-";

	/// <summary>Resolves the configured image directory to an absolute path.</summary>
	/// <param name="configured">
	/// The configured directory, or <see langword="null"/>/blank for <see cref="DefaultDirectory"/>.
	/// A relative path is resolved against <paramref name="baseDirectory"/>.
	/// </param>
	/// <param name="baseDirectory">The AppHost directory.</param>
	internal static string Resolve(string? configured, string baseDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

		var directory = string.IsNullOrWhiteSpace(configured) ? DefaultDirectory : configured;

		return Path.TrimEndingDirectorySeparator(
			Path.GetFullPath(Path.IsPathRooted(directory) ? directory : Path.Combine(baseDirectory, directory))
		);
	}
}
