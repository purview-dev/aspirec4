using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

/// <summary>
/// Covers how the ER diagram's fire-and-forget work reports a failure instead of losing it.
/// </summary>
public sealed class ErdBackgroundTaskTests
{
	[Test]
	public async Task RunAsync_WhenWorkCompletes_ReturnsTrueAndLeavesTheRowAlone(CancellationToken cancellationToken)
	{
		// Arrange
		var (sut, notifications, _) = CreateSut();
		var resource = CreateResource();

		// Act
		var result = await sut.RunAsync(
			"write",
			resource,
			() => Task.CompletedTask,
			writesDiagram: true,
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(sut.HasFailed).IsFalse();
		await Assert.That(notifications.TryGetCurrentState(resource.Name, out _)).IsFalse();
	}

	[Test]
	public async Task RunAsync_WhenADiagramWriteThrows_ReturnsFalseAndMarksTheRowFailed(
		CancellationToken cancellationToken
	)
	{
		// The case that used to be lost: an exception escaping fire-and-forget work, leaving the row
		// at Running while the diagram silently stopped updating.
		// Arrange
		var (sut, notifications, _) = CreateSut();
		var resource = CreateResource();

		// Act
		var result = await sut.RunAsync(
			"write",
			resource,
			() => throw new IOException("locked"),
			writesDiagram: true,
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsFalse();
		await Assert.That(sut.HasFailed).IsTrue();
		await Assert.That(notifications.TryGetCurrentState(resource.Name, out var current)).IsTrue();
		await Assert.That(current!.Snapshot.State).IsEqualTo(ErdBackgroundTask.FailedState);
	}

	[Test]
	public async Task RunAsync_WhenADiagramWriteThrows_WritesTheErrorToTheResourceLog(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (sut, _, logs) = CreateSut();
		var resource = CreateResource();

		// Act
		await sut.RunAsync("regenerate", resource, () => throw new IOException("locked"), true, cancellationToken);

		// Assert
		var lines = await ReadLogAsync(logs, resource, cancellationToken);

		await Assert.That(lines).Contains(line => line.Contains("regenerate", StringComparison.Ordinal));
		await Assert.That(lines).Contains(line => line.Contains("locked", StringComparison.Ordinal));
	}

	[Test]
	public async Task RunAsync_WhenNonWritingWorkThrows_LogsButLeavesTheRowAlone(CancellationToken cancellationToken)
	{
		// Linking or hiding rows going wrong leaves the diagram itself intact, so the row must not
		// claim it failed.
		// Arrange
		var (sut, notifications, _) = CreateSut();
		var resource = CreateResource();

		// Act
		var result = await sut.RunAsync(
			"cross-link",
			resource,
			() => throw new InvalidOperationException("boom"),
			writesDiagram: false,
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsFalse();
		await Assert.That(sut.HasFailed).IsFalse();
		await Assert.That(notifications.TryGetCurrentState(resource.Name, out _)).IsFalse();
	}

	[Test]
	public async Task RunAsync_OnShutdown_ReturnsFalseWithoutReporting(CancellationToken cancellationToken)
	{
		// Arrange
		var (sut, _, _) = CreateSut();
		var resource = CreateResource();
		using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		await shutdown.CancelAsync();

		// Act
		var result = await sut.RunAsync(
			"write",
			resource,
			() => Task.FromCanceled(shutdown.Token),
			writesDiagram: true,
			shutdown.Token
		);

		// Assert
		await Assert.That(result).IsFalse();
		await Assert.That(sut.HasFailed).IsFalse();
	}

	[Test]
	public async Task ClearFailure_AfterAFailure_ReturnsTrueOnceThenFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var (sut, _, _) = CreateSut();
		await sut.RunAsync("write", CreateResource(), () => throw new IOException(), true, cancellationToken);

		// Act
		var first = sut.ClearFailure();
		var second = sut.ClearFailure();

		// Assert
		await Assert.That(first).IsTrue();
		await Assert.That(second).IsFalse();
		await Assert.That(sut.HasFailed).IsFalse();
	}

	// Helpers

	static (ErdBackgroundTask Sut, ResourceNotificationService Notifications, ResourceLoggerService Logs) CreateSut()
	{
		ResourceLoggerService logs = new();
		ResourceNotificationService notifications = new(
			NullLogger<ResourceNotificationService>.Instance,
			IHostApplicationLifetime.Mock(),
			new ServiceCollection().BuildServiceProvider(),
			logs
		);

		return (new ErdBackgroundTask(notifications, logs), notifications, logs);
	}

	static AspireC4EfResource CreateResource(string name = "aspirec4-er") =>
		new(name)
		{
			OutputDirectory = Path.GetTempPath(),
			AppHostDirectory = Path.GetTempPath(),
			InnerResource = null,
		};

	static async Task<List<string>> ReadLogAsync(
		ResourceLoggerService logs,
		IResource resource,
		CancellationToken cancellationToken
	)
	{
		List<string> lines = [];
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(5));

		try
		{
			// The log is replayed from its backlog, so the first batch holds what was already written.
			await foreach (var batch in logs.WatchAsync(resource).WithCancellation(timeout.Token))
			{
				lines.AddRange(batch.Select(line => line.Content));

				if (lines.Count > 0)
				{
					break;
				}
			}
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			// Nothing was written; the assertions report it.
		}

		return lines;
	}
}
