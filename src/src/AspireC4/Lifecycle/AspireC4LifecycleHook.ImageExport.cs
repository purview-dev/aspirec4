using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>Serialises export runs so two invocations never write the same image at once.</summary>
	readonly SemaphoreSlim _exportGate = new(1, 1);

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
