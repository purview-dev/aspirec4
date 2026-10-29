namespace Aspire.Hosting.AspireC4.Lifecycle;

public sealed partial class AspireC4LifecycleHookTests
{
	// All tests use absolute paths derived from the platform's path-root so that
	// ComputeCommonAncestor works correctly on both Windows and Unix.
	static readonly string Root = Path.GetPathRoot(Path.GetFullPath("."))!
		.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1877:Collapse consecutive Path.Combine or Path.Join operations"
	)]
	static string P(params string[] segments) =>
		Path.Combine(Root + Path.DirectorySeparatorChar, Path.Combine(segments));

	[Test]
	public async Task ComputeCommonAncestor_SinglePath_ReturnsThatPath()
	{
		// Arrange
		var outputDir = P("project", "apphost", "likec4", "gen");

		// Act
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([outputDir]);

		// Assert
		await Assert.That(result).IsEqualTo(outputDir);
	}

	[Test]
	public async Task ComputeCommonAncestor_OutputDirOnly_ReturnsOutputDir()
	{
		// Arrange
		// Simulates the case where no additional DSL folders or image aliases are registered —
		// the bind mount should cover exactly the output directory.
		var outputDir = P("project", "apphost", "likec4", "gen");

		// Act
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([outputDir]);

		// Assert
		await Assert.That(result).IsEqualTo(outputDir);
	}

	[Test]
	public async Task ComputeCommonAncestor_AdditionalDSLFolderUnderOutputParent_ExpandsToParent()
	{
		// Arrange
		// extensions/ is a sibling of gen/ — common ancestor must expand to likec4/.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");

		// Act
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([outputDir, dslFolder]);

		// Assert
		var expected = P("project", "apphost", "likec4");
		await Assert.That(result).IsEqualTo(expected);
	}

	[Test]
	public async Task ComputeCommonAncestor_ImageAliasFolderUnderOutputParent_ExpandsToParent()
	{
		// Arrange
		// images/ is a sibling of gen/ — common ancestor must expand to likec4/.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var imagesFolder = P("project", "apphost", "likec4", "images");

		// Act
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([outputDir, imagesFolder]);

		// Assert
		var expected = P("project", "apphost", "likec4");
		await Assert.That(result).IsEqualTo(expected);
	}

	[Test]
	public async Task ComputeCommonAncestor_DSLAndImageFoldersBothRegistered_ExpandsToLeastCommonAncestor()
	{
		// Arrange
		// extensions/ and images/ are siblings of gen/ — all three live under likec4/.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");
		var imagesFolder = P("project", "apphost", "likec4", "images");

		// Act
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([outputDir, dslFolder, imagesFolder]);

		// Assert
		var expected = P("project", "apphost", "likec4");
		await Assert.That(result).IsEqualTo(expected);
	}

	[Test]
	public async Task ComputeCommonAncestor_ExternalImageFolder_ExpandsToAppHostRoot()
	{
		// Arrange
		// When the image alias folder lives outside the likec4/ tree (e.g. src/icons/),
		// the common ancestor must back up to the shared parent (apphost/).
		var outputDir = P("project", "apphost", "likec4", "gen");
		var externalImages = P("project", "apphost", "src", "icons");

		// Act
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([outputDir, externalImages]);

		// Assert
		var expected = P("project", "apphost");
		await Assert.That(result).IsEqualTo(expected);
	}

	[Test]
	public async Task ComputeCommonAncestor_EmptyList_ThrowsArgumentException()
	{
		// Arrange

		// Act
		static string Act() => AspireC4LifecycleHook.ComputeCommonAncestor([]);

		// Assert
		await Assert.That(Act).Throws<ArgumentException>();
	}
}
