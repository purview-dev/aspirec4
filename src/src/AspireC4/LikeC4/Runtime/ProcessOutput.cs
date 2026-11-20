using System.Diagnostics;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Waits for a process whose standard output and error are both redirected.
/// </summary>
static class ProcessOutput
{
	/// <summary>
	/// Reads both redirected streams to the end while waiting for the process to exit, and returns what
	/// each one carried. The process is killed if it outlives <paramref name="timeout"/> or
	/// <paramref name="cancellationToken"/> is cancelled.
	/// </summary>
	/// <remarks>
	/// Both reads start before the wait. A redirected stream that nobody reads fills its pipe buffer, at
	/// which point the process blocks on its next write and never exits; reading one stream to the end
	/// before starting the other has the same problem the other way round.
	/// </remarks>
	/// <exception cref="OperationCanceledException">
	/// The timeout elapsed or <paramref name="cancellationToken"/> was cancelled; the process has been
	/// killed.
	/// </exception>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Killing a process that may already have exited is best-effort"
	)]
	public static async Task<(string StandardOutput, string StandardError)> WaitAsync(
		Process process,
		TimeSpan timeout,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(process);

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(timeout);

		try
		{
			var standardOutput = process.StandardOutput.ReadToEndAsync(cts.Token);
			var standardError = process.StandardError.ReadToEndAsync(cts.Token);

			await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

			return (await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
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

			throw;
		}
	}
}
