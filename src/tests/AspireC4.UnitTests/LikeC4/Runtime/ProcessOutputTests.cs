using System.Diagnostics;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Covers waiting on a process whose standard output and error are both redirected.
/// </summary>
public sealed class ProcessOutputTests
{
	/// <summary>Comfortably more than an OS pipe buffer (4–64 KB), so an unread stream would block.</summary>
	const int PayloadBytes = 1024 * 1024;

	[Test]
	public async Task WaitAsync_WhenBothStreamsOverflowThePipeBuffer_CompletesWithBothOutputs(
		CancellationToken cancellationToken
	)
	{
		// The formatter and the image export used to wait with a redirected stream left unread: a process
		// that wrote more than the pipe holds blocked until the timeout killed it.
		// Arrange
		var payload = await CreatePayloadFileAsync(cancellationToken);
		using var process = StartWritingToBothStreams(payload);

		try
		{
			// Act
			var (standardOutput, standardError) = await ProcessOutput.WaitAsync(
				process,
				TimeSpan.FromSeconds(60),
				cancellationToken
			);

			// Assert
			await Assert.That(process.ExitCode).IsEqualTo(0);
			await Assert.That(standardOutput.Length).IsGreaterThanOrEqualTo(PayloadBytes);
			await Assert.That(standardError.Length).IsGreaterThanOrEqualTo(PayloadBytes);
		}
		finally
		{
			File.Delete(payload);
		}
	}

	[Test]
	public async Task WaitAsync_WhenTheTimeoutElapses_KillsTheProcessAndThrows(CancellationToken cancellationToken)
	{
		// Arrange
		using var process = StartSleeping();

		// Act
		async Task Act() => await ProcessOutput.WaitAsync(process, TimeSpan.FromMilliseconds(200), cancellationToken);

		// Assert
		await Assert.That(Act).Throws<OperationCanceledException>();

		using var exit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		exit.CancelAfter(TimeSpan.FromSeconds(10));
		await process.WaitForExitAsync(exit.Token);
		await Assert.That(process.HasExited).IsTrue();
	}

	// Helpers

	static async Task<string> CreatePayloadFileAsync(CancellationToken cancellationToken)
	{
		var path = Path.Combine(Path.GetTempPath(), $"aspirec4-process-output-{Guid.NewGuid():N}.txt");
		var line = new string('x', 1023) + "\n";

		await File.WriteAllTextAsync(
			path,
			string.Concat(Enumerable.Repeat(line, (PayloadBytes / line.Length) + 1)),
			cancellationToken
		);

		return path;
	}

	// cmd.exe parses its own command line rather than using the C runtime's rules, so it is given the
	// raw string; ArgumentList would escape the quotes in a way cmd does not understand.
	static Process StartWritingToBothStreams(string payload) =>
		OperatingSystem.IsWindows()
			? Start("cmd.exe", $"/d /c type \"{payload}\" & type \"{payload}\" 1>&2")
			: Start("/bin/sh", $"-c \"cat '{payload}'; cat '{payload}' >&2\"");

	static Process StartSleeping() =>
		OperatingSystem.IsWindows()
			? Start("cmd.exe", "/d /c ping -n 30 127.0.0.1 >nul")
			: Start("/bin/sh", "-c \"sleep 30\"");

	static Process Start(string fileName, string arguments) =>
		Process.Start(
			new ProcessStartInfo(fileName, arguments)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			}
		) ?? throw new InvalidOperationException($"Could not start {fileName}.");
}
