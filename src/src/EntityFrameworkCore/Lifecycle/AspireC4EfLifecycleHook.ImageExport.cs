using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

sealed partial class AspireC4EfLifecycleHook
{
	/// <summary>Serialises export runs so two d2 invocations never write the same file at once.</summary>
	readonly SemaphoreSlim _exportGate = new(1, 1);

	/// <summary>
	/// Whether this run is a publish rather than a run, in which case nothing is exported.
	/// </summary>
	/// <remarks>
	/// Publish generates its output and exits. An export starts a container and is fire-and-forget, so
	/// on publish it would either be killed part-way through — leaving a truncated image where a good
	/// one used to be — or hold the process open well past the work it was asked to do. Publish is also
	/// expected to be hermetic: reaching for a container runtime that a build agent may not have turns a
	/// working publish into a failing one.
	/// </remarks>
	volatile bool _isPublishMode;

	/// <summary>
	/// Whether a diagram that has just been written should be followed by an image export.
	/// </summary>
	/// <remarks>
	/// Extracted so the rule can be asserted directly. Driving it through a publish proves nothing: the
	/// export is fire-and-forget and publish exits immediately afterwards, so the images are missing at
	/// the end of a publish whether this returns false or the export simply had not finished — which is
	/// the very race that makes exporting on publish the wrong thing to do.
	/// </remarks>
	/// <param name="exportImages">The configured <see cref="AspireC4EfDiagramOptions.ExportImages"/>.</param>
	/// <param name="contexts">How many contexts the diagram holds; nothing is exported for an empty one.</param>
	/// <param name="isPublishMode">Whether this run is a publish rather than a run.</param>
	internal static bool ShouldExportImages(bool exportImages, int contexts, bool isPublishMode) =>
		exportImages && contexts > 0 && !isPublishMode;

	/// <summary>
	/// Where images are being written, once export has resolved it.
	/// </summary>
	/// <remarks>
	/// Published as a dashboard property and used by the open-folder command, so a consumer never has to
	/// know the convention to find the images. Stays <see langword="null"/> while export is off, which is
	/// what keeps the property and the command off a row that would never populate them.
	/// </remarks>
	volatile string? _imageDirectory;

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
	async Task ExportImagesAsync(
		EfModel model,
		string directory,
		string appHostDirectory,
		ILogger logger,
		CancellationToken cancellationToken
	)
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
				.ExportAsync(model, _options, directory, appHostDirectory, logger, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			_exportGate.Release();
		}
	}
}
