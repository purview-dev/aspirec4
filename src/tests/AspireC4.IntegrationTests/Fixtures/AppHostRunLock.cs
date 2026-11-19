namespace Aspire.Hosting.AspireC4.Fixtures;

/// <summary>
/// A machine-wide lock that lets one sample AppHost run at a time, across test processes.
/// </summary>
/// <remarks>
/// <para>
/// <c>just test</c> runs every test project at once, and there are two integration suites: this one on
/// the Aspire 17 TestAppHost and AspireC4.Aspire13.IntegrationTests on TestAppHost.Aspire13. Each
/// AppHost starts about eight containers. Two at once on a developer machine left Postgres health
/// checks failing and resources short of Running within the startup timeout, failing both suites, while
/// either alone passes.
/// </para>
/// <para>
/// The lock is an exclusively opened file (<see cref="FileShare.None"/>, which .NET enforces with
/// <c>flock</c> on Unix), so the operating system releases it when the holder exits, even after a
/// crash, and it works the same on Windows, Linux and macOS.
/// </para>
/// </remarks>
sealed class AppHostRunLock : IDisposable
{
	static readonly string LockPath = Path.Combine(Path.GetTempPath(), "aspirec4-integration-apphost.lock");
	static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

	readonly FileStream _stream;

	AppHostRunLock(FileStream stream) => _stream = stream;

	/// <summary>Waits until no other test process holds the lock, then takes it.</summary>
	public static async Task<AppHostRunLock> AcquireAsync(CancellationToken cancellationToken)
	{
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				// The empty file is left behind on purpose: deleting it on close lets a waiter on Unix
				// lock the unlinked file while a third process locks a new one at the same path.
				FileStream stream = new(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

				return new(stream);
			}
			catch (IOException)
			{
				// Held by another test process; wait for it to finish with its AppHost.
				await Task.Delay(RetryDelay, cancellationToken);
			}
		}
	}

	public void Dispose() => _stream.Dispose();
}
