using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

/// <summary>
/// Runs the ER diagram's background work, reporting an unexpected failure on the diagram's row
/// instead of leaving it as an unobserved task exception.
/// </summary>
/// <remarks>
/// Every task the ER hook starts is fire-and-forget, so an exception that escapes one is seen by no one:
/// the diagram simply stops updating and the row still says <c>Running</c>.
/// </remarks>
/// <param name="resourceNotificationService">Publishes the failed state onto the diagram's row.</param>
/// <param name="resourceLoggerService">Writes the failure to the diagram's Console tab.</param>
sealed class ErdBackgroundTask(
	ResourceNotificationService resourceNotificationService,
	ResourceLoggerService resourceLoggerService
)
{
	/// <summary>The state shown on the diagram's row when generating it failed.</summary>
	internal static readonly ResourceStateSnapshot FailedState = new("Failed", KnownResourceStateStyles.Error);

	/// <summary>1 while the diagram's row shows <see cref="FailedState"/>.</summary>
	int _failed;

	/// <summary>Whether the diagram's row currently shows <see cref="FailedState"/>.</summary>
	public bool HasFailed => Volatile.Read(ref _failed) == 1;

	/// <summary>
	/// Records that the diagram has been written successfully again, returning whether the row was
	/// showing a failure that the caller now needs to replace.
	/// </summary>
	public bool ClearFailure() => Interlocked.Exchange(ref _failed, 0) == 1;

	/// <summary>Starts <paramref name="work"/> on the thread pool and returns without waiting for it.</summary>
	/// <param name="task">A short name for the work, shown in the log line.</param>
	/// <param name="resource">The diagram resource whose Console tab the failure is written to.</param>
	/// <param name="work">The work to run.</param>
	/// <param name="writesDiagram">
	/// Whether the work writes the diagram, in which case a failure also marks the row as failed. Work
	/// that only links or hides rows leaves the diagram itself intact, so it is logged and nothing more.
	/// </param>
	/// <param name="cancellationToken">The application lifetime; cancellation is treated as shutdown.</param>
	public Task Start(
		string task,
		AspireC4EfResource resource,
		Func<Task> work,
		bool writesDiagram,
		CancellationToken cancellationToken
	) => Task.Run(() => RunAsync(task, resource, work, writesDiagram, cancellationToken), CancellationToken.None);

	/// <summary>
	/// Runs <paramref name="work"/>, returning <see langword="true"/> when it completed and
	/// <see langword="false"/> when it failed or the application is shutting down.
	/// </summary>
	/// <inheritdoc cref="Start" path="/param"/>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Background work must not fault unobserved; every failure is reported on the diagram's row"
	)]
	public async Task<bool> RunAsync(
		string task,
		AspireC4EfResource resource,
		Func<Task> work,
		bool writesDiagram,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(resource);
		ArgumentNullException.ThrowIfNull(work);

		try
		{
			await work().ConfigureAwait(false);

			return true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown.
			return false;
		}
		catch (Exception ex)
		{
			await ReportFailureAsync(task, resource, ex, writesDiagram).ConfigureAwait(false);

			return false;
		}
	}

	/// <summary>
	/// Logs a failure to the diagram's Console tab and, for a failed write, marks its row as failed until
	/// the next successful write sets it back to <c>Running</c>.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Reporting a failure must never throw from the catch block that is reporting it"
	)]
	async Task ReportFailureAsync(string task, AspireC4EfResource resource, Exception exception, bool writesDiagram)
	{
		try
		{
			ErdLog.BackgroundTaskFailed(resourceLoggerService.GetLogger(resource), task, exception.Message, exception);

			if (writesDiagram)
			{
				Interlocked.Exchange(ref _failed, 1);

				await resourceNotificationService
					.PublishUpdateAsync(resource, snapshot => snapshot with { State = FailedState })
					.ConfigureAwait(false);
			}
		}
		catch
		{
			// Nothing left to report to.
		}
	}
}
