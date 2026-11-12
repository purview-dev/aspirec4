using System.Diagnostics;

namespace Aspire.Hosting.AspireC4.Repository;

/// <summary>
/// Runs the repository's TypeScript test suite as part of the .NET test run.
/// </summary>
/// <remarks>
/// <para>
/// Purview.Build discovers test projects by <c>*Tests.csproj</c> and runs them with
/// <c>dotnet test</c>; it has no JavaScript test module. Without this bridge <c>bun test</c> would
/// only run when somebody remembered to invoke it, so neither <c>just test</c> nor the shared
/// pipeline would execute the TypeScript tests at all.
/// </para>
/// <para>
/// The suite is dependency-free - it imports only <c>node:*</c> and <c>bun:test</c> - so it runs
/// from a clean checkout without an install step. The shared pipeline installs Bun before it runs;
/// a developer machine needs Bun for the repository's other scripts anyway.
/// </para>
/// </remarks>
public sealed class TypeScriptTests
{
	const string Bun = "bun";

	[Test]
	public async Task BunTest_RepositorySuite_ShouldPass(CancellationToken cancellationToken)
	{
		// Arrange
		var repositoryRoot = FindRepositoryRoot();

		// Act
		var (exitCode, output) = await RunBunTestsAsync(repositoryRoot, cancellationToken);

		// Assert
		await Assert
			.That(exitCode)
			.IsEqualTo(0)
			.Because($"`bun test` failed in '{repositoryRoot}'.{Environment.NewLine}{output}");
	}

	static async Task<(int ExitCode, string Output)> RunBunTestsAsync(
		string repositoryRoot,
		CancellationToken cancellationToken
	)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = Bun,
			WorkingDirectory = repositoryRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		startInfo.ArgumentList.Add("test");

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException($"Could not start '{Bun}'. Ensure Bun is installed.");

		var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

		try
		{
			await process.WaitForExitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}

			throw;
		}

		return (process.ExitCode, await stdout + await stderr);
	}

	static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory is not null)
		{
			if (
				File.Exists(Path.Combine(directory.FullName, "global.json"))
				&& Directory.Exists(Path.Combine(directory.FullName, "scripts"))
			)
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException($"Could not find the repository root above '{AppContext.BaseDirectory}'.");
	}
}
