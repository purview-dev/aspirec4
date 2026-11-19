using Aspire.Hosting.AspireC4.Fixtures;

namespace Aspire.Hosting.AspireC4.AppHosts;

/// <summary>
/// Runs each polyglot sample AppHost through the Aspire CLI and asserts it draws the shared
/// <see cref="SampleAppHostContract"/>, which the C# integration suites assert against <c>TestAppHost</c>
/// on Aspire 17 and 13.6.5. Together they gate CI on every AppHost implementation.
/// </summary>
/// <remarks>
/// <para>
/// A missing toolchain (the Aspire CLI, Docker, npm, and Bun, Go or uv for the sample's language) skips
/// the test, so the shared pipeline, which does not install them, reports it as skipped. The dedicated CI
/// job sets <c>ASPIREC4_REQUIRE_APPHOSTS=true</c>, which turns a missing tool into a failure, so a
/// broken runner cannot pass the gate by skipping.
/// </para>
/// <para>
/// The samples run one at a time, and never beside a C# sample AppHost (<see cref="AppHostRunLock"/>):
/// each starts about eight containers.
/// </para>
/// </remarks>
[NotInParallel]
public sealed class PolyglotAppHostContractTests
{
	const int TestTimeoutMilliseconds = 20 * 60 * 1000;

	[Test]
	[Timeout(TestTimeoutMilliseconds)]
	[Arguments("typescript-app-host", "bun")]
	[Arguments("go-app-host", "go")]
	[Arguments("python-app-host", "uv")]
	public async Task StartAsync_WhenTheSampleAppHostStarts_DrawsTheSharedSampleArchitecture(
		string sample,
		string languageTool,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		EnsureToolsAvailable(["aspire", "docker", "npm", languageTool]);
		using var runLock = await AppHostRunLock.AcquireAsync(cancellationToken);
		await using var appHost = await SampleAppHost.PrepareAsync(sample, cancellationToken);

		// Act
		await appHost.StartAsync(cancellationToken);
		var violations = await appHost.WaitForContractAsync(cancellationToken);

		// Assert
		await Assert.That(violations).IsEmpty().Because(string.Join(Environment.NewLine, violations));
	}

	static void EnsureToolsAvailable(IReadOnlyList<string> tools)
	{
		var missing = tools.Where(tool => !ToolLocator.IsOnPath(tool)).ToArray();
		if (missing.Length == 0)
			return;

		var message = $"Not on PATH: {string.Join(", ", missing)}.";
		if (ToolLocator.ToolsRequired)
			throw new InvalidOperationException($"{message} ASPIREC4_REQUIRE_APPHOSTS is set, so this fails.");

		Skip.Test($"{message} Set ASPIREC4_REQUIRE_APPHOSTS=true to make this a failure.");
	}
}
