namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

public sealed class LatestVersionResolverTests
{
	[Test]
	public async Task TryExtractVersion_FullCLIOutput_ExtractsVersionToken()
	{
		// Arrange
		const string cliOutput = "@likec4/cli/1.57.0 linux-x64 node-v22.14.0";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("1.57.0");
	}

	[Test]
	public async Task TryExtractVersion_OutputWithVPrefix_ExtractsVersionToken()
	{
		// Arrange
		const string cliOutput = "@likec4/cli/v1.57.0 linux-x64 node-v22.14.0";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("v1.57.0");
	}

	[Test]
	public async Task TryExtractVersion_NodeVersionNotPickedUp_ReturnsFalseWhenNoLikeC4Version()
	{
		// Arrange
		const string cliOutput = "linux-x64 node-v22.14.0";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(versionToken).IsEmpty();
	}

	[Test]
	public async Task TryExtractVersion_EmptyInput_ReturnsFalse()
	{
		// Arrange
		const string cliOutput = "";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(versionToken).IsEmpty();
	}

	[Test]
	public async Task TryExtractVersion_PrereleaseVersion_ExtractsBaseVersion()
	{
		// Arrange
		const string cliOutput = "@likec4/cli/1.57.0-beta.1 linux-x64 node-v22.14.0";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("1.57.0-beta.1");
	}

	[Test]
	public async Task TryExtractVersion_BareVersionToken_Succeeds()
	{
		// Arrange
		const string cliOutput = "1.57.0";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("1.57.0");
	}

	[Test]
	public async Task TryExtractVersion_MultiLineOutput_ExtractsVersionFromFirstMatchingLine()
	{
		// Arrange
		// pnpm and other runtimes may emit multiple lines; the version line is somewhere in the output.
		const string cliOutput = "some preamble\n@likec4/cli/1.60.2 linux-x64 node-v22.14.0\n";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput.Split('\n')[1].Trim(), out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("1.60.2");
	}
}
