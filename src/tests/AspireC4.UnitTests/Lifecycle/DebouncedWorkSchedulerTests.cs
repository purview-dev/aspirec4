using System.Collections.Concurrent;

namespace Aspire.Hosting.AspireC4.Lifecycle;

/// <summary>
/// Covers how <see cref="DebouncedWorkScheduler"/> settles a burst of requests, and in particular that a
/// request superseded before its task started does not read a token source the next request disposed.
/// </summary>
public sealed class DebouncedWorkSchedulerTests
{
	[Test]
	public async Task Schedule_WhenTheDelayElapses_RunsTheWorkOnce(CancellationToken cancellationToken)
	{
		// Arrange
		TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
		var runs = 0;
		using var sut = CreateSut(TimeSpan.FromMilliseconds(10), StartOnThreadPool);

		// Act
		sut.Schedule(() =>
		{
			Interlocked.Increment(ref runs);
			completed.TrySetResult();

			return Task.CompletedTask;
		});

		// Assert
		await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
		await Assert.That(runs).IsEqualTo(1);
	}

	[Test]
	public async Task Schedule_WhenTheWorkStartsAfterANewerRequest_DoesNotThrowOrRun()
	{
		// A startup burst schedules one request per state change within the same few milliseconds, so a
		// superseded request's task can start only after the next request cancelled and disposed its token
		// source. Reading the token then threw ObjectDisposedException, which was reported as a diagram
		// regeneration failure for every superseded request.
		// Arrange
		List<Func<Task>> started = [];
		var supersededRan = false;
		using var sut = CreateSut(TimeSpan.FromMilliseconds(50), started.Add);

		sut.Schedule(() =>
		{
			supersededRan = true;

			return Task.CompletedTask;
		});
		sut.Schedule(() => Task.CompletedTask);

		// Act
		await started[0]();

		// Assert
		await Assert.That(supersededRan).IsFalse();
	}

	[Test]
	public async Task Schedule_WhenANewerRequestArrives_RunsOnlyTheNewestWork()
	{
		// Arrange
		List<Func<Task>> started = [];
		List<string> ran = [];
		using var sut = CreateSut(TimeSpan.FromMilliseconds(50), started.Add);

		sut.Schedule(() =>
		{
			ran.Add("first");

			return Task.CompletedTask;
		});
		sut.Schedule(() =>
		{
			ran.Add("second");

			return Task.CompletedTask;
		});

		// Act
		await started[0]();
		await started[1]();

		// Assert
		await Assert.That(ran).IsEquivalentTo(["second"]);
	}

	[Test]
	public async Task Schedule_WhenStateChangesArriveInABurst_RunsOnceAndReportsNoFailure(
		CancellationToken cancellationToken
	)
	{
		// The shape of the real startup burst: a hundred requests in a row, one thread-pool task each.
		// Arrange
		ConcurrentBag<Exception> failures = [];
		TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
		var runs = 0;
		using var sut = CreateSut(TimeSpan.FromMilliseconds(25), work => StartCapturingFailures(work, failures));

		// Act
		for (var i = 0; i < 100; i++)
		{
			sut.Schedule(() =>
			{
				Interlocked.Increment(ref runs);
				completed.TrySetResult();

				return Task.CompletedTask;
			});
		}

		// Assert
		await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

		// A superseded request may only start now, so give it time to fail if it is going to.
		await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

		await Assert.That(runs).IsEqualTo(1);
		await Assert.That(failures).IsEmpty();
	}

	[Test]
	public async Task Dispose_WhenARequestIsStillWaiting_CancelsIt()
	{
		// Arrange
		List<Func<Task>> started = [];
		var ran = false;
		var sut = CreateSut(TimeSpan.FromMilliseconds(50), started.Add);

		sut.Schedule(() =>
		{
			ran = true;

			return Task.CompletedTask;
		});

		// Act
		sut.Dispose();
		await started[0]();

		// Assert
		await Assert.That(ran).IsFalse();
	}

	[Test]
	public async Task Schedule_AfterDispose_DoesNotStartTheWork()
	{
		// Arrange
		List<Func<Task>> started = [];
		var sut = CreateSut(TimeSpan.FromMilliseconds(50), started.Add);
		sut.Dispose();

		// Act
		sut.Schedule(() => Task.CompletedTask);

		// Assert
		await Assert.That(started).IsEmpty();
	}

	// Helpers

	static DebouncedWorkScheduler CreateSut(TimeSpan delay, Action<Func<Task>> start) => new(delay, start);

	static void StartOnThreadPool(Func<Task> work) => _ = Task.Run(work, CancellationToken.None);

	static void StartCapturingFailures(Func<Task> work, ConcurrentBag<Exception> failures) =>
		_ = Task.Run(
			async () =>
			{
				try
				{
					await work();
				}
#pragma warning disable CA1031 // intentional: the test asserts that nothing reaches this catch
				catch (Exception ex)
				{
					failures.Add(ex);
				}
#pragma warning restore CA1031
			},
			CancellationToken.None
		);
}
