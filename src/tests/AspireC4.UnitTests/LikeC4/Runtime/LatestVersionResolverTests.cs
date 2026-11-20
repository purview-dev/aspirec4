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

	[Test]
	public async Task TryExtractVersion_YarnDlxOutput_ExtractsLikeC4sVersionNotYarns()
	{
		// Arrange
		// yarn dlx prints its own version first; the CLI's version is the last line.
		const string cliOutput =
			"➤ YN0000: · Yarn 4.18.0\n"
			+ "➤ YN0085: │ + likec4@npm:1.61.0, react-dom@npm:19.3.1, and 119 more.\n"
			+ "➤ YN0000: · Done in 9s 516ms\n"
			+ "\n"
			+ "1.61.0\n";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("1.61.0");
	}

	[Test]
	public async Task TryExtractVersion_WholeMultiLineOutput_ExtractsTheVersion()
	{
		// Arrange
		// The whole of a runtime's output is handed over, preamble included, not one pre-selected line.
		const string cliOutput = "some preamble\r\n@likec4/cli/1.60.2 linux-x64 node-v22.14.0\r\n";

		// Act
		var found = LatestVersionResolver.TryExtractVersion(cliOutput, out var versionToken);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(versionToken).IsEqualTo("1.60.2");
	}

	[Test]
	public async Task ContainerArguments_AlwaysPullsTheImageBeforeAskingForItsVersion()
	{
		// The server pulls with ImagePullPolicy.Always; a probe against a stale cached "latest" would
		// report the old version while the server runs a newer one.
		// Act
		var arguments = LatestVersionResolver.ContainerArguments("ghcr.io/likec4/likec4:latest");

		// Assert
		await Assert
			.That(arguments)
			.IsEquivalentTo(["run", "--rm", "--pull", "always", "ghcr.io/likec4/likec4:latest", "--version"]);
	}
}
