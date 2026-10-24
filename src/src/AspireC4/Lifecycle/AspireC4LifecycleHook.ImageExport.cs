using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>Serialises export runs so two invocations never write the same image at once.</summary>
	readonly SemaphoreSlim _exportGate = new(1, 1);

	/// <summary>
	/// Whether this run is a publish rather than a run, in which case nothing is exported.
	/// </summary>
	/// <remarks>
	/// Publish generates the model and the manifest and then exits. An export starts a container and is
	/// fire-and-forget, so on publish it would either be killed part-way through — leaving a truncated
	/// image where a good one used to be — or hold the process open well past the work it was asked to
	/// do. Publish is also expected to be hermetic: reaching for a container runtime that a build agent
	/// may not have turns a working publish into a failing one.
	/// </remarks>
	volatile bool _isPublishMode;

	/// <summary>
	/// Whether a write that has just happened should be followed by an image export.
	/// </summary>
	/// <remarks>
	/// Extracted so the rule can be asserted directly. Driving it through a publish proves nothing: the
	/// export is fire-and-forget and publish exits immediately afterwards, so the images are missing at
	/// the end of a publish whether this returns false or the export simply had not finished — which is
	/// the very race that makes exporting on publish the wrong thing to do.
	/// </remarks>
	/// <param name="changed">Whether the generated model differs from the last one written.</param>
	/// <param name="exportImages">The configured <see cref="AspireC4DiagramOptions.ExportImages"/>.</param>
	/// <param name="isPublishMode">Whether this run is a publish rather than a run.</param>
	internal static bool ShouldExportImages(bool changed, bool exportImages, bool isPublishMode) =>
		changed && exportImages && !isPublishMode;

	/// <summary>
	/// Incremented for every requested export, so a run that is still queued when a newer model arrives
	/// can stand down rather than render a model that has already been superseded.
	/// </summary>
	long _exportVersion;

	/// <summary>
	/// Renders the views to images, unless a newer model arrived while this run was waiting its turn.
	/// </summary>
	/// <remarks>
	/// Called fire-and-forget from <see cref="WriteC4FileAsync"/>, which has already established that the
	/// generated model changed. The resource-state watcher regenerates the model on every state
	/// transition, so a long export can easily be overtaken; superseded runs stand down rather than
	/// queueing up behind each other and writing older output last.
	/// </remarks>
	async Task ExportImagesAsync(
		string outputDirectory,
		string serveDirectory,
		string appHostDirectory,
		string modelBody,
		CancellationToken cancellationToken
	)
	{
		var version = Interlocked.Increment(ref _exportVersion);

		try
		{
			await _exportGate.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			// Shutdown.
			return;
		}

		try
		{
			if (Interlocked.Read(ref _exportVersion) != version)
			{
				return;
			}

			// In container mode the export runs in the LikeC4 image, which already carries the Chromium
			// and Graphviz binaries it needs; only WithLocalCLI() shifts it onto the host's own CLI.
			var localCLI = workspaceOptions.Value.LocalCLIRuntime is { } runtime
				? AspireC4Builder.BuildLikeC4CLIPrefix(runtime)
				: ((string Command, string[] Prefix)?)null;

			await LikeC4ImageExporter.ExportAsync(
				options.Value,
				outputDirectory,
				serveDirectory,
				appHostDirectory,
				modelBody,
				localCLI,
				telemetry,
				cancellationToken
			);
		}
		finally
		{
			_exportGate.Release();
		}
	}
}
