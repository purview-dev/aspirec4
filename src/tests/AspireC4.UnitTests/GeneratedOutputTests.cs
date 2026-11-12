namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Covers how generated files reach disk: written only when their content changes, and listed in an
/// ignore file AspireC4 owns without ever replacing one it does not.
/// </summary>
public sealed class GeneratedOutputTests
{
	// Helpers

	static string CreateDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), "aspirec4-output-" + Guid.NewGuid().ToString("N")[..8]);

		Directory.CreateDirectory(directory);

		return directory;
	}

	static async Task<string> CreateFileAsync(
		string directory,
		string name,
		string content,
		CancellationToken cancellationToken
	)
	{
		var path = Path.Combine(directory, name);

		await File.WriteAllTextAsync(path, content, cancellationToken);

		// Backdated so a rewrite is visible in the timestamp even on a filesystem with coarse resolution.
		File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));

		return path;
	}

	// Tests

	[Test]
	public async Task WriteIfChangedAsync_WhenTheFileHoldsTheSameContent_LeavesItUntouched(
		CancellationToken cancellationToken
	)
	{
		// A restart of an unchanged application must not touch the working tree — not even the timestamp
		// a file watcher or an IDE would react to.
		// Arrange
		var directory = CreateDirectory();
		var path = await CreateFileAsync(directory, "model.gen.c4", "model", cancellationToken);
		var before = File.GetLastWriteTimeUtc(path);

		// Act
		var written = await GeneratedOutput.WriteIfChangedAsync(path, "model", cancellationToken);

		// Assert
		await Assert.That(written).IsFalse();
		await Assert.That(File.GetLastWriteTimeUtc(path)).IsEqualTo(before);
	}

	[Test]
	public async Task WriteIfChangedAsync_WhenTheContentDiffers_WritesIt(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();
		var path = await CreateFileAsync(directory, "model.gen.c4", "old", cancellationToken);

		// Act
		var written = await GeneratedOutput.WriteIfChangedAsync(path, "new", cancellationToken);

		// Assert
		await Assert.That(written).IsTrue();
		await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo("new");
	}

	[Test]
	public async Task WriteIfChangedAsync_WhenTheFileIsMissing_WritesIt(CancellationToken cancellationToken)
	{
		// Arrange
		var path = Path.Combine(CreateDirectory(), "model.gen.c4");

		// Act
		var written = await GeneratedOutput.WriteIfChangedAsync(path, "model", cancellationToken);

		// Assert
		await Assert.That(written).IsTrue();
		await Assert.That(File.Exists(path)).IsTrue();
	}

	[Test]
	public async Task CopyIfChanged_WhenTheDestinationHasTheSameBytes_LeavesItUntouched(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var directory = CreateDirectory();
		var source = await CreateFileAsync(directory, "extend.c4", "extend api {}", cancellationToken);
		var destination = await CreateFileAsync(CreateDirectory(), "extend.c4", "extend api {}", cancellationToken);
		var before = File.GetLastWriteTimeUtc(destination);

		// Act
		var copied = GeneratedOutput.CopyIfChanged(source, destination);

		// Assert
		await Assert.That(copied).IsFalse();
		await Assert.That(File.GetLastWriteTimeUtc(destination)).IsEqualTo(before);
	}

	[Test]
	public async Task CopyIfChanged_WhenTheSourceChanged_CopiesIt(CancellationToken cancellationToken)
	{
		// Arrange
		var source = await CreateFileAsync(CreateDirectory(), "extend.c4", "extend api { #new }", cancellationToken);
		var destination = await CreateFileAsync(CreateDirectory(), "extend.c4", "extend api {}", cancellationToken);

		// Act
		var copied = GeneratedOutput.CopyIfChanged(source, destination);

		// Assert
		await Assert.That(copied).IsTrue();
		await Assert.That(await File.ReadAllTextAsync(destination, cancellationToken)).IsEqualTo("extend api { #new }");
	}

	[Test]
	public async Task BuildGitIgnore_ForPlainNames_AnchorsThemToTheDirectory()
	{
		// An unanchored "model.gen.c4" would also ignore a same-named hand-written file further down.
		// Act
		var content = GeneratedOutput.BuildGitIgnore("purpose", ["model.gen.c4", "vendor/", ".aspirec4-*"]);

		// Assert
		await Assert.That(content).Contains("\n/model.gen.c4\n");
		await Assert.That(content).Contains("\n/vendor/\n");
		await Assert.That(content).Contains("\n.aspirec4-*\n");
	}

	[Test]
	public async Task BuildGitIgnore_ForTheSameEntriesInAnyOrder_IsIdentical()
	{
		// Act
		var first = GeneratedOutput.BuildGitIgnore("purpose", ["b.c4", "a.c4", "a.c4"]);
		var second = GeneratedOutput.BuildGitIgnore("purpose", ["a.c4", "b.c4"]);

		// Assert
		await Assert.That(second).IsEqualTo(first);
		await Assert.That(first).StartsWith(GeneratedOutput.GitIgnoreMarker);
		await Assert.That(first).DoesNotContain("\r");
	}

	[Test]
	public async Task BuildGitIgnore_Always_IgnoresItself()
	{
		// It is regenerated on every run, so committing it would only add a file nobody edits.
		// Act
		var content = GeneratedOutput.BuildGitIgnore("purpose", ["model.gen.c4"]);

		// Assert
		await Assert.That(content).Contains("\n/.gitignore\n");
	}

	[Test]
	public async Task WriteGitIgnoreAsync_WhenNoFileExists_WritesOne(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = Path.Combine(CreateDirectory(), "images");

		// Act
		var result = await GeneratedOutput.WriteGitIgnoreAsync(
			directory,
			"purpose",
			[".aspirec4-*"],
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsEqualTo(GitIgnoreWrite.Written);
		await Assert
			.That(await File.ReadAllTextAsync(Path.Combine(directory, ".gitignore"), cancellationToken))
			.Contains(".aspirec4-*");
	}

	[Test]
	public async Task WriteGitIgnoreAsync_WhenItsOwnFileIsCurrent_ReportsUnchanged(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();
		await GeneratedOutput.WriteGitIgnoreAsync(directory, "purpose", ["model.gen.c4"], cancellationToken);

		// Act
		var result = await GeneratedOutput.WriteGitIgnoreAsync(
			directory,
			"purpose",
			["model.gen.c4"],
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsEqualTo(GitIgnoreWrite.Unchanged);
	}

	[Test]
	public async Task WriteGitIgnoreAsync_WhenTheEntriesChange_RefreshesItsOwnFile(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();
		await GeneratedOutput.WriteGitIgnoreAsync(directory, "purpose", ["model.gen.c4"], cancellationToken);

		// Act
		var result = await GeneratedOutput.WriteGitIgnoreAsync(
			directory,
			"purpose",
			["model.gen.c4", "likec4.config.json"],
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsEqualTo(GitIgnoreWrite.Written);
		await Assert
			.That(await File.ReadAllTextAsync(Path.Combine(directory, ".gitignore"), cancellationToken))
			.Contains("/likec4.config.json");
	}

	[Test]
	public async Task WriteGitIgnoreAsync_WhenTheUserOwnsTheFile_LeavesItAlone(CancellationToken cancellationToken)
	{
		// An output directory can be one the user shares with their own files; their ignore rules are theirs.
		// Arrange
		var directory = CreateDirectory();
		var path = await CreateFileAsync(directory, ".gitignore", "secrets.json\n", cancellationToken);

		// Act
		var result = await GeneratedOutput.WriteGitIgnoreAsync(
			directory,
			"purpose",
			["model.gen.c4"],
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsEqualTo(GitIgnoreWrite.KeptUserFile);
		await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo("secrets.json\n");
	}
}
