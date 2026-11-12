namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Writes AspireC4's generated files so that an unchanged application leaves the working tree untouched,
/// and records which of them git should ignore.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory "has the diagram changed" checks start empty every time the AppHost does, so on their
/// own they rewrite every file on the first write of each run. Comparing against the bytes already on
/// disk is what keeps a restart of an unchanged application from touching anything — content or
/// timestamp — which is what makes the output safe to commit and quiet for file watchers.
/// </para>
/// <para>
/// The <c>.gitignore</c> files name exactly the files AspireC4 writes, anchored to their directory,
/// rather than ignoring everything with <c>*</c>. An output directory can be one the user shares with
/// their own files (<c>WithOutputDirectory</c>, <c>WithImageExportDirectory</c>), and a blanket rule there
/// would silently hide their work from source control.
/// </para>
/// </remarks>
static class GeneratedOutput
{
	/// <summary>The file name of the ignore file written beside the output.</summary>
	internal const string GitIgnoreFileName = ".gitignore";

	/// <summary>
	/// The first line of every ignore file AspireC4 writes. A <c>.gitignore</c> that does not start with
	/// it belongs to the user and is never overwritten.
	/// </summary>
	internal const string GitIgnoreMarker = "# Written by AspireC4.";

	/// <summary>Writes <paramref name="content"/> unless the file already holds exactly that text.</summary>
	/// <returns><see langword="true"/> when the file was written.</returns>
	internal static async Task<bool> WriteIfChangedAsync(
		string path,
		string content,
		CancellationToken cancellationToken
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(content);

		if (File.Exists(path))
		{
			var existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
			if (string.Equals(existing, content, StringComparison.Ordinal))
			{
				return false;
			}
		}

		await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);

		return true;
	}

	/// <summary>Copies <paramref name="source"/> unless the destination already has the same bytes.</summary>
	/// <returns><see langword="true"/> when the file was copied.</returns>
	internal static bool CopyIfChanged(string source, string destination)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		if (File.Exists(destination))
		{
			FileInfo sourceInfo = new(source);
			FileInfo destinationInfo = new(destination);

			if (
				sourceInfo.Length == destinationInfo.Length
				&& File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(destination))
			)
			{
				return false;
			}
		}

		File.Copy(source, destination, overwrite: true);

		return true;
	}

	/// <summary>
	/// Writes, or refreshes, the <c>.gitignore</c> that tells git to ignore <paramref name="entries"/> in
	/// <paramref name="directory"/>.
	/// </summary>
	/// <param name="directory">The output directory the entries live in.</param>
	/// <param name="purpose">One line saying what the ignored files are, written as a comment.</param>
	/// <param name="entries">
	/// File names or patterns relative to <paramref name="directory"/>. A plain name is anchored with a
	/// leading <c>/</c> so it never matches a same-named file further down; a trailing <c>/</c> marks a
	/// directory. A pattern containing <c>*</c> is written as given.
	/// </param>
	/// <param name="cancellationToken">Cancelled on shutdown.</param>
	internal static async Task<GitIgnoreWrite> WriteGitIgnoreAsync(
		string directory,
		string purpose,
		IEnumerable<string> entries,
		CancellationToken cancellationToken
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
		ArgumentNullException.ThrowIfNull(entries);

		var path = Path.Combine(directory, GitIgnoreFileName);

		if (File.Exists(path))
		{
			var existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
			if (!existing.StartsWith(GitIgnoreMarker, StringComparison.Ordinal))
			{
				return GitIgnoreWrite.KeptUserFile;
			}
		}

		Directory.CreateDirectory(directory);

		var written = await WriteIfChangedAsync(path, BuildGitIgnore(purpose, entries), cancellationToken)
			.ConfigureAwait(false);

		return written ? GitIgnoreWrite.Written : GitIgnoreWrite.Unchanged;
	}

	/// <summary>Builds the ignore file's content. Exposed for tests.</summary>
	/// <remarks>
	/// Always <c>\n</c>, and the entries are de-duplicated and sorted ordinally, so the file is identical
	/// on every machine and every run. The file ignores itself: it is regenerated on each run, so
	/// committing it would only add a file nobody edits.
	/// </remarks>
	internal static string BuildGitIgnore(string purpose, IEnumerable<string> entries)
	{
		var lines = entries
			.Where(e => !string.IsNullOrWhiteSpace(e))
			.Select(Anchor)
			.Append("/" + GitIgnoreFileName)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal);

		return string.Join(
			'\n',
			[
				GitIgnoreMarker,
				"# " + purpose,
				"# Regenerated on every run; to stop AspireC4 writing it, turn off GenerateGitIgnoreFile.",
				.. lines,
				string.Empty,
			]
		);
	}

	static string Anchor(string entry)
	{
		var normalised = entry.Trim().Replace('\\', '/');

		return normalised.Contains('*', StringComparison.Ordinal) || normalised.StartsWith('/')
			? normalised
			: "/" + normalised;
	}
}

/// <summary>What <see cref="GeneratedOutput.WriteGitIgnoreAsync"/> did.</summary>
enum GitIgnoreWrite
{
	/// <summary>The file was created or its content changed.</summary>
	Written,

	/// <summary>The file already held exactly this content.</summary>
	Unchanged,

	/// <summary>A <c>.gitignore</c> AspireC4 did not write was already there, and was left alone.</summary>
	KeptUserFile,
}
