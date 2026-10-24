using System.Diagnostics;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Renders the ER diagram by driving the generated interactive viewer in a headless browser, so the
/// exported image is the one the viewer itself produces.
/// </summary>
/// <remarks>
/// <para>
/// The d2 export is a different diagram, not a differently-styled one: d2 draws <c>sql_table</c> shapes
/// with its own theme and layout, while the viewer draws React Flow cards with context colours, key
/// badges, complex-type groups and crow's-foot markers. Only the viewer can produce the viewer's
/// picture, and the viewer only runs in a browser — which is the whole cost of this path, and why it is
/// opt-in.
/// </para>
/// <para>
/// The script clicks the viewer's own Save as SVG/PNG control rather than screenshotting the window:
/// that control exports the diagram at its natural size, so a large schema is not cropped to a viewport.
/// </para>
/// </remarks>
static class ErdViewerImageExporter
{
	/// <summary>The image carrying both node and a matching chromium.</summary>
	/// <remarks>
	/// Pinned to the Playwright version this repository develops the viewer checks against, so the
	/// browser running an export is the browser the viewer was verified in.
	/// </remarks>
	public const string DefaultImage = "mcr.microsoft.com/playwright";

	/// <summary>The pinned image tag.</summary>
	public const string DefaultTag = "v1.56.0-noble";

	/// <summary>Where the generated viewer is mounted inside the container.</summary>
	internal const string ContainerViewerPath = "/data";

	/// <summary>Where the images are written inside the container.</summary>
	internal const string ContainerOutputPath = "/out";

	/// <summary>Where the export script is mounted inside the container.</summary>
	internal const string ContainerScriptPath = "/srv/viewer-export.mjs";

	/// <summary>The image reference used when no override is configured.</summary>
	internal static string ImageReference(AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return string.IsNullOrWhiteSpace(options.ViewerExportImage)
			? $"{DefaultImage}:{DefaultTag}"
			: options.ViewerExportImage;
	}

	/// <summary>
	/// Builds the command and arguments that render every requested format in one browser session.
	/// </summary>
	/// <remarks>
	/// One invocation for all formats rather than one each: starting a browser and laying the diagram
	/// out again dominates the cost, and the viewer can produce both files from a single render.
	/// </remarks>
	internal static (string Command, IReadOnlyList<string> Arguments) BuildInvocation(
		AspireC4EfDiagramOptions options,
		string viewerDirectory,
		string imageDirectory,
		string scriptPath,
		IReadOnlyList<ErdImageFormat> formats
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewerDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
		ArgumentNullException.ThrowIfNull(formats);

		return (
			options.ContainerRuntimeCommand,
			[
				"run",
				"--rm",
				// --mount rather than -v for the same reason as the d2 export: a Windows host path
				// already contains a colon, which leaves -v guessing where the drive letter ends.
				"--mount",
				$"type=bind,source={viewerDirectory},target={ContainerViewerPath},readonly",
				"--mount",
				$"type=bind,source={imageDirectory},target={ContainerOutputPath}",
				"--mount",
				$"type=bind,source={scriptPath},target={ContainerScriptPath},readonly",
				// Chromium's sandbox needs privileges the container does not have, and the page being
				// rendered is one this package generated from the consumer's own model.
				"--ipc=host",
				ImageReference(options),
				"node",
				ContainerScriptPath,
				string.Join(",", formats.Select(ErdImageExporter.Extension)),
				ErdImageExporter.ImageBaseName(options.FileName),
			]
		);
	}

	/// <summary>Renders every requested format through the viewer.</summary>
	/// <remarks>
	/// Best-effort, like every other export path: a failure is reported and the diagram itself is
	/// untouched. It deliberately does not fall back to the d2 render — a consumer who asked for the
	/// viewer's picture is not served by quietly being given a different one.
	/// </remarks>
	/// <returns>The image files written.</returns>
	public static async Task<IReadOnlyList<string>> ExportAsync(
		AspireC4EfDiagramOptions options,
		string viewerDirectory,
		string imageDirectory,
		IReadOnlyList<ErdImageFormat> formats,
		ILogger logger,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (formats.Count == 0)
		{
			return [];
		}

		// Mounted as a single file so the container sees exactly the script and nothing else from the
		// host; a directory of its own keeps that mount from colliding with anything already there.
		var scriptDirectory = Path.Combine(Path.GetTempPath(), $"aspirec4-viewer-export-{Guid.NewGuid():N}");

		Directory.CreateDirectory(scriptDirectory);

		var scriptPath = Path.Combine(scriptDirectory, "viewer-export.mjs");

		try
		{
			await File.WriteAllTextAsync(scriptPath, InteractiveViewer.ViewerExportScript(), cancellationToken)
				.ConfigureAwait(false);

			var (command, arguments) = BuildInvocation(options, viewerDirectory, imageDirectory, scriptPath, formats);
			var browserImage = ImageReference(options);

			ErdLog.ViewerImageExportStarted(logger, browserImage);

			if (!await RunAsync(command, arguments, options, logger, cancellationToken).ConfigureAwait(false))
			{
				return [];
			}

			List<string> written = [];

			foreach (var format in formats)
			{
				var image = Path.Combine(
					imageDirectory,
					$"{ErdImageExporter.ImageBaseName(options.FileName)}.{ErdImageExporter.Extension(format)}"
				);

				// The script reports its own failures, so a file that is simply absent here means the
				// viewer never handed one back rather than that the run failed outright.
				if (File.Exists(image))
				{
					written.Add(image);
					ErdLog.ImageExported(logger, image);
				}
			}

			return written;
		}
		finally
		{
			TryDelete(scriptDirectory);
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Image export is best-effort; a failure is logged and the diagram itself is unaffected"
	)]
	static async Task<bool> RunAsync(
		string command,
		IReadOnlyList<string> arguments,
		AspireC4EfDiagramOptions options,
		ILogger logger,
		CancellationToken cancellationToken
	)
	{
		try
		{
			ProcessStartInfo startInfo = new()
			{
				FileName = command,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			foreach (var argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}

			using var process = Process.Start(startInfo);

			if (process is null)
			{
				ErdLog.ImageExportFailed(logger, command, "the process could not be started");

				return false;
			}

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromSeconds(options.ViewerExportTimeoutSeconds));

			// Drained before waiting: pulling the image is verbose, and a full pipe would deadlock.
			var error = await process.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);

			try
			{
				await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
					// Best-effort kill.
				}

				throw;
			}

			if (process.ExitCode == 0)
			{
				return true;
			}

			ErdLog.ImageExportFailed(
				logger,
				command,
				string.IsNullOrWhiteSpace(error) ? $"it exited with code {process.ExitCode}" : error.Trim()
			);

			return false;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown.
			return false;
		}
		catch (Exception ex)
		{
			ErdLog.ImageExportFailed(logger, command, ex.Message);

			return false;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Cleaning up a temporary directory must never fail an export that otherwise succeeded"
	)]
	static void TryDelete(string directory)
	{
		try
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
		catch
		{
			// A leftover temp directory is harmless.
		}
	}
}
