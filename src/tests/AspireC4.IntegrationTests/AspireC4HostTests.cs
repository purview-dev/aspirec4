using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Integration tests that verify the LikeC4 visualization starts successfully in a real Aspire app host.
/// The application is started once per test session by the shared <see cref="Fixtures.TestAppHostFixture"/>
/// (see <c>ClassDataSource</c>) and torn down automatically, avoiding the resource contention that occurs
/// when 10+ Aspire apps (each with postgres/redis/docker containers) start in parallel.
/// HMR is disabled during testing to avoid port binding on the CI host.
/// </summary>
[ClassDataSource<Fixtures.TestAppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class AspireC4HostTests(Fixtures.TestAppHostFixture fixture)
{
	const string AspireC4ResourceName = AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName;
	const string AspireC4ServerResourceName =
		AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
		+ AspireC4DistributedApplicationBuilderExtensions.AspireC4ServerResourceSuffix;

	/// <summary>
	/// Skips the current test when no container runtime is available so the suite
	/// shows "Skipped" rather than "Failed" on developer machines without Docker.
	/// </summary>
	[Before(Test)]
	public void SkipWhenNoContainerRuntime()
	{
		if (fixture.SkipReason is not null)
			Skip.Test(fixture.SkipReason);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesC4File()
	{
		// Arrange
		// (app started by the shared fixture)

		// Act
		// (side effect occurred during startup)

		// Assert
		await Assert.That(fixture.ModelPath).IsNotNull();
		await Assert.That(File.Exists(fixture.ModelPath)).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_C4FileContainsDslStructure(CancellationToken cancellationToken)
	{
		// Arrange
		// (app started by the shared fixture)

		// Act
		var content = await File.ReadAllTextAsync(fixture.ModelPath, cancellationToken);

		// Assert
		await Assert.That(content).Contains("specification {");
		await Assert.That(content).Contains("model {");
		await Assert.That(content).Contains("views {");
		await Assert.That(content).Contains("node_app");
		await Assert.That(content).Contains("AspireC4 Architecture");
	}

	[Test]
	public async Task StartAsync_WithLikeC4Container_ReachesRunningState()
	{
		// Arrange
		// (app started by the shared fixture, which waits for all resources to be ready)

		// Act
		var snapshot = fixture.GetResourceSnapshot(AspireC4ResourceName);

		// Assert
		await Assert.That(snapshot).IsNotNull();
		await Assert.That(snapshot!.State).IsEqualTo(KnownResourceStates.Running);
	}

	[Test]
	[Timeout(120_000)]
	public async Task StartAsync_WithLikeC4Container_HttpEndpointReturnsSuccess(CancellationToken cancellationToken)
	{
		// Arrange
		// (app started by the shared fixture)
		using var client = fixture.CreateHttpClient(AspireC4ServerResourceName, AspireC4Resource.HttpEndpointName);

		// Act
		HttpResponseMessage? response = null;
		for (var attempt = 0; attempt < 10; attempt++)
		{
			try
			{
				response = await client.GetAsync("/", cancellationToken);
				break;
			}
			catch (HttpRequestException) when (attempt < 9)
			{
				await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
			}
		}

		// Assert
		await Assert.That(response).IsNotNull();
		await Assert.That((int)response!.StatusCode).IsLessThan(500);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesLikeC4ConfigFile(CancellationToken cancellationToken)
	{
		// Arrange
		// (app started by the shared fixture)
		var configPath = fixture.ConfigPath;

		// Act
		var json = await File.ReadAllTextAsync(configPath, cancellationToken);

		// Assert
		await Assert.That(File.Exists(configPath)).IsTrue();
		await Assert.That(json).Contains("aspirec4");
		await Assert.That(json).Contains("Integration Test Architecture");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ConfigFileContainsExtensionsPath(CancellationToken cancellationToken)
	{
		// Arrange
		// (app started by the shared fixture)
		var configPath = fixture.ConfigPath;

		// Act
		var json = await File.ReadAllTextAsync(configPath, cancellationToken);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var includeFound = root.TryGetProperty("include", out var include);
		JsonElement paths = default;
		var pathsFound = includeFound && include.TryGetProperty("paths", out paths);
		var hasExtensionsPath =
			pathsFound
			&& paths
				.EnumerateArray()
				.Any(p => p.GetString()?.Contains("extensions", StringComparison.OrdinalIgnoreCase) == true);

		// Assert
		await Assert.That(includeFound).IsTrue();
		await Assert.That(pathsFound).IsTrue();
		await Assert.That(hasExtensionsPath).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ConfigFileContainsImageAlias(CancellationToken cancellationToken)
	{
		// Arrange
		// (app started by the shared fixture)
		var configPath = fixture.ConfigPath;

		// Act
		var json = await File.ReadAllTextAsync(configPath, cancellationToken);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var aliasesFound = root.TryGetProperty("imageAliases", out var aliases);
		var imageAliasFound = aliasesFound && aliases.TryGetProperty("@", out _);

		// Assert
		await Assert.That(aliasesFound).IsTrue();
		await Assert.That(imageAliasFound).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ImageAliasFolderContainsFiles()
	{
		// Arrange
		// (app started by the shared fixture)
		var imagesDir = fixture.ImagesDirectory;

		// Act
		var files = Directory.GetFiles(imagesDir, "*", SearchOption.AllDirectories);
		var svgCount = files.Count(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase));
		var pngCount = files.Count(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

		// Assert
		await Assert.That(Directory.Exists(imagesDir)).IsTrue();
		await Assert.That(svgCount).IsGreaterThan(0);
		await Assert.That(pngCount).IsGreaterThan(0);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratedDslPassesValidation(CancellationToken cancellationToken)
	{
		// Arrange
		// (app started by the shared fixture)

		// Act
		var (totalErrors, rawOutput) = await RunLikeC4ValidateDirectoryAsync(
			fixture.OutputDirectory,
			cancellationToken
		);

		// Assert
		if (totalErrors < 0)
		{
			throw new InvalidOperationException($"LikeC4 validation did not produce JSON output.\n\n{rawOutput}");
		}

		if (totalErrors != 0)
		{
			throw new InvalidOperationException(
				$"Expected 0 LikeC4 validation errors but got {totalErrors}.\n\nValidator output:\n{rawOutput}"
			);
		}

		await Assert.That(totalErrors).IsEqualTo(0);
	}

	static async Task<(int TotalErrors, string RawOutput)> RunLikeC4ValidateDirectoryAsync(
		string directory,
		CancellationToken cancellationToken
	)
	{
		var useDotFlag = await Helpers.IsDotAvailableAsync(cancellationToken) ? " --use-dot" : "";

		string shellFile,
			shellArgs;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			shellFile = "cmd.exe";
			shellArgs = $"/c npx --yes likec4 validate --json --no-layout{useDotFlag} \"{directory}\"";
		}
		else
		{
			shellFile = "/bin/sh";
			shellArgs = $"-c \"npx --yes likec4 validate --json --no-layout{useDotFlag} '{directory}'\"";
		}

		using Process process = new()
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = shellFile,
				Arguments = shellArgs,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = directory,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		var rawOutput = $"stdout:\n{stdout}\nstderr:\n{stderr}";

		var jsonStart = stdout.IndexOf('{', StringComparison.Ordinal);
		var jsonEnd = stdout.LastIndexOf('}');
		if (jsonStart < 0 || jsonEnd < 0)
		{
			return (-1, rawOutput);
		}

		var json = stdout[jsonStart..(jsonEnd + 1)];
		using var doc = JsonDocument.Parse(json);
		var stats = doc.RootElement.GetProperty("stats");
		var totalErrors = stats.GetProperty("totalErrors").GetInt32();
		return (totalErrors, rawOutput);
	}
}
