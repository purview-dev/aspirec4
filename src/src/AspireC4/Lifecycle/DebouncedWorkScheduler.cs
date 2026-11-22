namespace Aspire.Hosting.AspireC4.Lifecycle;

/// <summary>
/// Runs work once a burst of requests has settled, cancelling the work that a newer request supersedes.
/// </summary>
/// <remarks>
/// <para>
/// The debounce owns the <see cref="CancellationTokenSource"/> because its token has to be read while the
/// source is still alive. A newer request cancels and disposes the previous source, and every state change
/// at startup arrives within a few milliseconds, so reading <see cref="CancellationTokenSource.Token"/>
/// from the pending generation threw <see cref="ObjectDisposedException"/> for each superseded one. That
/// was reported as a diagram regeneration failure even though nothing had failed.
/// </para>
/// <para>
/// The work itself is not wrapped in a catch: the caller reports its own failures, which is what keeps a
/// real regeneration failure distinguishable from a superseded generation.
/// </para>
/// </remarks>
/// <param name="delay">How long the requests have to settle before the work runs.</param>
/// <param name="start">
/// Starts the debounced work. The hook starts it on the thread pool; a test captures it instead, so a
/// superseded generation can be run on demand.
/// </param>
sealed class DebouncedWorkScheduler(TimeSpan delay, Action<Func<Task>> start) : IDisposable
{
	readonly Lock _lock = new();

	// The generation still allowed to run. Scheduling cancels and disposes the one it replaces, so a
	// superseded generation never reaches the delay.
	CancellationTokenSource? _cts;

	bool _disposed;

	/// <summary>
	/// Requests <paramref name="work"/>, superseding any request that has not started yet.
	/// </summary>
	public void Schedule(Func<Task> work)
	{
		ArgumentNullException.ThrowIfNull(work);

		CancellationToken cancellationToken;
		lock (_lock)
		{
			if (_disposed)
			{
				return;
			}

			_cts?.Cancel();
			_cts?.Dispose();

			_cts = new CancellationTokenSource();

			// Read while holding the lock and while this source is the current one: the next Schedule
			// disposes it, and reading the token of a disposed source is what threw.
			cancellationToken = _cts.Token;
		}

		start(async () =>
		{
			try
			{
				// Superseded before this task even started, so the delay must not be entered at all.
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}

				await Task.Delay(delay, cancellationToken);
				await work();
			}
			catch (OperationCanceledException)
			{
				// A newer request superseded this one.
			}
		});
	}

	/// <summary>Cancels a request that has not run yet and releases its token source.</summary>
	public void Dispose()
	{
		CancellationTokenSource? cts;
		lock (_lock)
		{
			_disposed = true;
			cts = _cts;
			_cts = null;
		}

		cts?.Cancel();
		cts?.Dispose();
	}
}
