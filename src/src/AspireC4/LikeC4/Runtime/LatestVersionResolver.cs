using System.Diagnostics;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Detects the LikeC4 version that will actually run, by asking the CLI itself with
/// <c>likec4 --version</c> — through the local JavaScript runtime, or in a one-shot container of the
/// image the server will use.
/// </summary>
static class LatestVersionResolver
{
	/// <summary>
	/// Extracts the first version-like token from the output of <c>likec4 --version</c>.
	/// The typical output format is <c>@likec4/cli/1.57.0 linux-x64 node-v22.14.0</c>;
	/// this method splits on <c>'/'</c> and <c>' '</c> and returns the first token that
	/// <see cref="HMRPortCompatibility.TryParseVersion"/> accepts (e.g. <c>"1.57.0"</c>).
	/// </summary>
	/// <remarks>
	/// Lines are read from the last one back. A package manager prints its own progress first —
	/// <c>yarn dlx</c> opens with <c>Yarn 4.18.0</c> — and the CLI's version comes last, so the first
	/// version-like token in the output can belong to the package manager rather than to LikeC4.
	/// </remarks>
	public static bool TryExtractVersion(string cliOutput, out string versionToken)
	{
		var lines = cliOutput.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
		for (var i = lines.Length - 1; i >= 0; i--)
		{
			foreach (var part in lines[i].Split(['/', ' '], StringSplitOptions.RemoveEmptyEntries))
			{
				if (HMRPortCompatibility.TryParseVersion(part, out _))
				{
					versionToken = part;
					return true;
				}
			}
		}

		versionToken = string.Empty;
		return false;
	}

	/// <summary>
	/// The arguments that run <c>likec4 --version</c> in a throwaway container of
	/// <paramref name="imageReference"/>.
	/// </summary>
	/// <remarks>
	/// <c>--pull always</c> matches the server's <c>ImagePullPolicy.Always</c>. Without it a cached
	/// <c>latest</c> would answer with whatever version was pulled last, while the server pulls a newer
	/// one and runs that instead. The image's entrypoint is the <c>likec4</c> CLI, so the arguments after
	/// the image are the CLI's own.
	/// </remarks>
	public static IReadOnlyList<string> ContainerArguments(string imageReference) =>
		["run", "--rm", "--pull", "always", imageReference, "--version"];

	/// <summary>
	/// Runs <c>&lt;runtime&gt; run --rm --pull always &lt;image&gt; --version</c> and extracts the version
	/// the image carries.
	/// </summary>
	public static Task<VersionResolution> TryResolveFromContainerAsync(
		string runtimeCommand,
		string imageReference,
		int timeoutSeconds,
		CancellationToken cancellationToken
	) => TryResolveAsync(runtimeCommand, ContainerArguments(imageReference), timeoutSeconds, cancellationToken);

	/// <summary>
	/// Runs <c>&lt;command&gt; [prefixArgs…] --version</c> via the local JavaScript package
	/// manager and extracts the version token from the output.
	/// This is used when <c>WithLocalCLI()</c> is in use to determine the installed LikeC4 version.
	/// </summary>
	public static Task<VersionResolution> TryResolveFromLocalCLIAsync(
		string command,
		IReadOnlyList<string> prefixArgs,
		int timeoutSeconds,
		CancellationToken cancellationToken
	) => TryResolveAsync(command, [.. prefixArgs, "--version"], timeoutSeconds, cancellationToken);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Version detection is best-effort; failures are reported via telemetry and the caller falls back gracefully"
	)]
	static async Task<VersionResolution> TryResolveAsync(
		string command,
		IReadOnlyList<string> arguments,
		int timeoutSeconds,
		CancellationToken cancellationToken
	)
	{
		var commandLine = string.Join(' ', [command, .. arguments]);

		try
		{
			ProcessStartInfo startInfo = new()
			{
				FileName = command,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			foreach (var argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}

			using var process = Process.Start(startInfo);
			if (process is null)
			{
				return VersionResolution.Failed(commandLine, "the process could not be started");
			}

			var (output, error) = await ProcessOutput
				.WaitAsync(process, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken)
				.ConfigureAwait(false);

			if (process.ExitCode != 0)
			{
				return VersionResolution.Failed(
					commandLine,
					string.IsNullOrWhiteSpace(error) ? $"it exited with code {process.ExitCode}" : error.Trim()
				);
			}

			return TryExtractVersion(output.Trim(), out var version)
				? VersionResolution.Resolved(version)
				: VersionResolution.Failed(commandLine, $"no version in its output '{output.Trim()}'");
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return VersionResolution.Failed(commandLine, $"it did not finish within {timeoutSeconds} seconds");
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			return VersionResolution.Failed(commandLine, ex.Message);
		}
	}
}

/// <summary>The outcome of asking LikeC4 for its version.</summary>
/// <param name="Version">The version, or <see langword="null"/> when it could not be determined.</param>
/// <param name="Command">The command that was run, for the failure message.</param>
/// <param name="Reason">Why it failed, or <see langword="null"/> on success.</param>
readonly record struct VersionResolution(string? Version, string? Command, string? Reason)
{
	public static VersionResolution Resolved(string version) => new(version, null, null);

	public static VersionResolution Failed(string command, string reason) => new(null, command, reason);
}
