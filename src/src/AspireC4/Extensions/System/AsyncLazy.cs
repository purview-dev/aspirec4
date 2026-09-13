namespace System;

/// <summary>
/// Simple async lazy implementation for container initialization.
/// </summary>
sealed class AsyncLazy<T> : IDisposable, IAsyncDisposable
{
	readonly SemaphoreSlim _lock = new(1, 1);
	readonly Func<CancellationToken, Task<T>> _factory;

	Task<T>? _task;
	bool _disposedValue;

	public AsyncLazy(Func<CancellationToken, Task<T>> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);

		_factory = factory;
	}

	public AsyncLazy(Func<Task<T>> factory)
		: this(_ => factory())
	{
		ArgumentNullException.ThrowIfNull(factory);
	}

	[Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code")]
	public async Task<T> GetValueAsync(CancellationToken cancellationToken = default)
	{
		if (_task is not null)
			return await _task;

		await _lock.WaitAsync(cancellationToken);
		try
		{
			// Double-check after acquiring the lock
			_task ??= _factory(cancellationToken);
			return await _task;
		}
		catch
		{
			// Don't cache failed tasks — allow retry
			_task = null;
			throw;
		}
		finally
		{
			_lock.Release();
		}
	}

	public bool IsValueCreated => _task?.IsCompletedSuccessfully ?? false;

	void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				if (_task is { IsCompletedSuccessfully: true } && _task.Result is IDisposable disposable)
					disposable.Dispose();

#pragma warning disable CA1031 // Do not catch general exception types
				try
				{
					_lock.Release();
				}
				catch { }
#pragma warning restore CA1031 // Do not catch general exception types

				_lock.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposedValue)
		{
			if (_task is { IsCompletedSuccessfully: true })
			{
				var result = await _task;
				if (result is IAsyncDisposable asyncDisposable)
					await asyncDisposable.DisposeAsync();
				else if (result is IDisposable disposable)
					disposable.Dispose();
			}

#pragma warning disable CA1031 // Do not catch general exception types
			try
			{
				_lock.Release();
			}
			catch { }
#pragma warning restore CA1031 // Do not catch general exception types

			_lock.Dispose();
			_disposedValue = true;
		}

		GC.SuppressFinalize(this);
	}
}
