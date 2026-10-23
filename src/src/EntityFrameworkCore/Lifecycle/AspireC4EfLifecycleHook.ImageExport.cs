using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

sealed partial class AspireC4EfLifecycleHook
{
	/// <summary>Serialises export runs so two d2 invocations never write the same file at once.</summary>
	readonly SemaphoreSlim _exportGate = new(1, 1);

	/// <summary>
	/// Incremented for every requested export, so a run that is still queued when a newer model arrives
	/// can stand down rather than render a diagram that has already been superseded.
	/// </summary>
	long _exportVersion;

	/// <summary>
	/// Renders the diagram to images, unless a newer model arrived while this run was waiting its turn.
	/// </summary>
	/// <remarks>
	/// Called fire-and-forget from <see cref="WriteDiagramAsync"/>, which has already established that
	/// the diagram changed: an export is worth the d2 invocation only when there is something new to
	/// render.
	/// </remarks>
	async Task ExportImagesAsync(EfModel model, string directory, ILogger logger, CancellationToken cancellationToken)
	{
		var version = Interlocked.Increment(ref _exportVersion);

		try
		{
			await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Shutdown.
			return;
		}

		try
		{
			// A regeneration that landed while this run was queued will export the later model itself,
			// so rendering this one would only overwrite it with something older.
			if (Interlocked.Read(ref _exportVersion) != version)
			{
				return;
			}

			await ErdImageExporter
				.ExportAsync(model, _options, directory, logger, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			_exportGate.Release();
		}
	}
}
