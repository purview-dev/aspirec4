namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Covers which version the container's version-gated features are decided against.
/// </summary>
public sealed class ContainerWorkspaceOptionsTests
{
	[Test]
	public async Task EffectiveVersion_WithAResolvedVersion_PrefersItOverTheTag()
	{
		// Arrange
		var sut = CreateSut(imageTag: "latest", resolvedVersion: "1.57.0");

		// Act
		var version = sut.EffectiveVersion;

		// Assert
		await Assert.That(version).IsEqualTo("1.57.0");
	}

	[Test]
	public async Task EffectiveVersion_WithoutAResolvedVersion_FallsBackToTheTag()
	{
		// Arrange
		var sut = CreateSut(imageTag: "1.56.0");

		// Act
		var version = sut.EffectiveVersion;

		// Assert
		await Assert.That(version).IsEqualTo("1.56.0");
	}

	[Test]
	public async Task EffectiveVersion_ForLatestOnceResolved_SelectsConfigurableHMR()
	{
		// The point of resolving "latest": the tag parses as no version, which keeps HMR on the
		// fixed-port fallback even when the image supports a configurable port.
		// Arrange
		var unresolved = CreateSut(imageTag: "latest");
		var resolved = CreateSut(imageTag: "latest", resolvedVersion: "1.57.0");

		// Act
		var before = HMRPortCompatibility.Resolve(unresolved.EffectiveVersion);
		var after = HMRPortCompatibility.Resolve(resolved.EffectiveVersion);

		// Assert
		await Assert.That(before).IsEqualTo(HMRPortMode.FixedPort);
		await Assert.That(after).IsEqualTo(HMRPortMode.Configurable);
	}

	// Helpers

	static ContainerWorkspaceOptions CreateSut(string? imageTag = null, string? resolvedVersion = null) =>
		new() { ImageTag = imageTag, ResolvedVersion = resolvedVersion };
}
