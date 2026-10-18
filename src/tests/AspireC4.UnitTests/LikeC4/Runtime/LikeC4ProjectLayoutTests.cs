namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

public sealed class LikeC4ProjectLayoutTests
{
	// Helpers

	static string CreateBaseDirectory() =>
		Path.Combine(Path.GetTempPath(), "aspirec4-layout-" + Guid.NewGuid().ToString("N")[..8]);

	static AspireC4DiagramOptions CreateOptions(
		string? projectDirectory = null,
		string? outputDirectory = null,
		bool useLegacyLayout = false
	)
	{
		AspireC4DiagramOptions options = new()
		{
			ProjectDirectory = projectDirectory,
			UseLegacyLayout = useLegacyLayout,
		};

		if (outputDirectory is not null)
		{
			options.OutputDirectory = outputDirectory;
		}

		return options;
	}

	// Tests

	[Test]
	public async Task Resolve_WithNoProjectDirectory_ServesTheOutputDirectory()
	{
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var options = CreateOptions();

		// Act
		var (outputDirectory, serveDirectory) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(outputDirectory).IsEqualTo(Path.Combine(baseDirectory, ".aspirec4", "likec4"));
		await Assert.That(serveDirectory).IsEqualTo(outputDirectory);
	}

	[Test]
	public async Task Resolve_WithAProjectDirectory_ServesTheProjectAndGeneratesBeneathIt()
	{
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var project = Path.Combine(baseDirectory, "my-arch");
		var options = CreateOptions(projectDirectory: project);

		// Act
		var (outputDirectory, serveDirectory) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(serveDirectory).IsEqualTo(project);
		await Assert.That(outputDirectory).IsEqualTo(Path.Combine(project, ".aspirec4"));
	}

	[Test]
	public async Task Resolve_WithAProjectDirectoryAndAnExplicitOutputDirectory_HonoursBoth()
	{
		// An explicit output directory is a deliberate choice and is not overridden by the project.
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var project = Path.Combine(baseDirectory, "my-arch");
		var options = CreateOptions(projectDirectory: project, outputDirectory: "./generated/");

		// Act
		var (outputDirectory, serveDirectory) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(serveDirectory).IsEqualTo(project);
		await Assert.That(outputDirectory).IsEqualTo(Path.Combine(baseDirectory, "generated"));
	}

	[Test]
	public async Task Resolve_WithARelativeProjectDirectory_ResolvesItAgainstTheBaseDirectory()
	{
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var options = CreateOptions(projectDirectory: "./architecture/");

		// Act
		var (_, serveDirectory) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(serveDirectory).IsEqualTo(Path.Combine(baseDirectory, "architecture"));
	}

	[Test]
	public async Task Resolve_WithTheLegacyLayout_UsesThePre136OutputDirectory()
	{
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var options = CreateOptions(useLegacyLayout: true);

		// Act
		var (outputDirectory, serveDirectory) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(outputDirectory).IsEqualTo(Path.Combine(baseDirectory, "likec4", "gen"));
		await Assert.That(serveDirectory).IsEqualTo(outputDirectory);
	}

	[Test]
	public async Task Resolve_WithTheLegacyLayoutAndAnExplicitOutputDirectory_HonoursTheExplicitOne()
	{
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var options = CreateOptions(outputDirectory: "./generated/", useLegacyLayout: true);

		// Act
		var (outputDirectory, _) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(outputDirectory).IsEqualTo(Path.Combine(baseDirectory, "generated"));
	}

	[Test]
	public async Task Resolve_WithTheLegacyLayoutAndAProjectDirectory_KeepsOutputInsideTheProject()
	{
		// Generated output has to live beneath the directory being served, so the project wins.
		// Arrange
		var baseDirectory = CreateBaseDirectory();
		var project = Path.Combine(baseDirectory, "my-arch");
		var options = CreateOptions(projectDirectory: project, useLegacyLayout: true);

		// Act
		var (outputDirectory, serveDirectory) = LikeC4ProjectLayout.Resolve(baseDirectory, options);

		// Assert
		await Assert.That(serveDirectory).IsEqualTo(project);
		await Assert.That(outputDirectory).IsEqualTo(Path.Combine(project, ".aspirec4"));
	}

	[Test]
	public async Task IsHandAuthoredProject_WhenTheServedDirectoryIsTheOutputDirectory_IsFalse()
	{
		// Arrange
		var directory = CreateBaseDirectory();

		// Act
		var result = LikeC4ProjectLayout.IsHandAuthoredProject(directory, directory);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task IsHandAuthoredProject_WhenTheProjectContainsTheOutputDirectory_IsTrue()
	{
		// Arrange
		var project = CreateBaseDirectory();

		// Act
		var result = LikeC4ProjectLayout.IsHandAuthoredProject(project, Path.Combine(project, ".aspirec4"));

		// Assert
		await Assert.That(result).IsTrue();
	}
}
