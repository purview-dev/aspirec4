using System.Diagnostics;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Detects the actual version of the LikeC4 container image by parsing the output of
/// a one-shot <c>likec4 --version</c> container run via the Aspire probe resource.
/// </summary>
static class LatestVersionResolver
{
	/// <summary>
	/// Extracts the first version-like token from the output of <c>likec4 --version</c>.
	/// The typical output format is <c>@likec4/cli/1.57.0 linux-x64 node-v22.14.0</c>;
	/// this method splits on <c>'/'</c> and <c>' '</c> and returns the first token that
	/// <see cref="HMRPortCompatibility.TryParseVersion"/> accepts (e.g. <c>"1.57.0"</c>).
	/// </summary>
	public static bool TryExtractVersion(string cliOutput, out string versionToken)
	{
		var parts = cliOutput.Split(['/', ' '], StringSplitOptions.RemoveEmptyEntries);
		foreach (var part in parts)
		{
			if (HMRPortCompatibility.TryParseVersion(part, out _))
			{
				versionToken = part;
				return true;
			}
		}

		versionToken = string.Empty;
		return false;
	}

	/// <summary>
	/// Runs <c>&lt;command&gt; [prefixArgs…] --version</c> via the local JavaScript package
	/// manager and extracts the version token from the output.
	/// This is used when <c>WithLocalCLI()</c> is in use to determine the installed LikeC4 version.
	/// Returns <see langword="null"/> if the process fails, times out, or the output cannot be parsed.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Version detection is best-effort; failures are reported via telemetry and the caller falls back gracefully"
	)]
	public static async Task<string?> TryResolveFromLocalCLIAsync(
		string command,
		IReadOnlyList<string> prefixArgs,
		int timeoutSeconds,
		CancellationToken cancellationToken
	)
	{
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

			foreach (var arg in prefixArgs)
				startInfo.ArgumentList.Add(arg);

			startInfo.ArgumentList.Add("--version");

			using var process = Process.Start(startInfo);
			if (process is null)
				return null;

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

			string output;
			try
			{
				output = await process.StandardOutput.ReadToEndAsync(cts.Token);
				await process.WaitForExitAsync(cts.Token);
			}
			catch (OperationCanceledException)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
					// Best-effort kill.
				}

				return null;
			}

			return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
				? TryExtractVersion(output.Trim(), out var version)
					? version
					: null
				: null;
		}
		catch
		{
			return null;
		}
	}
}
