using System.Diagnostics;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Renders the ER diagram to image files with d2, either a local binary or a one-shot container.
/// </summary>
/// <remarks>
/// Always goes through d2, whichever <see cref="ErdRenderer"/> produced the diagram. The interactive
/// renderer is a browser application and has no headless path, and d2 is the only format that both
/// renders images itself and keeps column-level edges, so routing every renderer through it gives one
/// toolchain and one appearance rather than one per renderer.
/// </remarks>
static class ErdImageExporter
{
	/// <summary>The file extension, and the d2 output format, for a given image format.</summary>
	internal static string Extension(ErdImageFormat format) => format == ErdImageFormat.Png ? "png" : "svg";

	/// <summary>
	/// The formats to export, in a stable order.
	/// </summary>
	/// <remarks>
	/// <see cref="AspireC4EfDiagramOptions.ImageFormats"/> is a set, whose enumeration order depends on
	/// insertion and on whether configuration binding appended to it. Ordering here keeps the log output
	/// and the test expectations from depending on that.
	/// </remarks>
	internal static IReadOnlyList<ErdImageFormat> FormatsToExport(AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return [.. options.ImageFormats.Distinct().Order()];
	}

	/// <summary>
	/// Builds the command and arguments that render <paramref name="format"/> from the d2 source in
	/// <paramref name="outputDirectory"/>.
	/// </summary>
	/// <remarks>
	/// The container variant mounts the output directory read-write rather than read-only, unlike the
	/// sidecar: the whole point of this invocation is to write the image back out beside the source.
	/// </remarks>
	internal static (string Command, IReadOnlyList<string> Arguments) BuildInvocation(
		AspireC4EfDiagramOptions options,
		string outputDirectory,
		ErdImageFormat format
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

		var engine = options.LayoutEngine.ToEngineName();
		var source = $"{options.FileName}.d2";
		var image = $"{options.FileName}.{Extension(format)}";

		if (!string.IsNullOrWhiteSpace(options.D2Command))
		{
			return (
				options.D2Command,
				["--layout", engine, Path.Combine(outputDirectory, source), Path.Combine(outputDirectory, image)]
			);
		}

		var tag = options.ContainerImageTag ?? D2ServerResource.DefaultTag;

		return (
			options.ContainerRuntimeCommand,
			[
				"run",
				"--rm",
				"-v",
				$"{outputDirectory}:{D2ServerResource.WorkspacePath}",
				$"{D2ServerResource.DefaultRegistry}/{D2ServerResource.DefaultImage}:{tag}",
				"--layout",
				engine,
				$"{D2ServerResource.WorkspacePath}/{source}",
				$"{D2ServerResource.WorkspacePath}/{image}",
			]
		);
	}

	/// <summary>
	/// Writes the d2 source if the renderer did not already, then renders every configured format.
	/// </summary>
	/// <remarks>
	/// Best-effort throughout, like the LikeC4 formatter: an export that fails leaves the diagram itself
	/// untouched, so the only correct response is to say so and carry on.
	/// </remarks>
	/// <returns>The image files written.</returns>
	public static async Task<IReadOnlyList<string>> ExportAsync(
		EfModel model,
		AspireC4EfDiagramOptions options,
		string outputDirectory,
		ILogger logger,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		var formats = FormatsToExport(options);

		if (formats.Count == 0)
		{
			return [];
		}

		// Every renderer but d2 itself produces something a headless tool cannot read, so the d2 source
		// is written alongside it purely to be rendered. Identical content and file name to what the d2
		// renderer writes, so enabling export never changes what that renderer already produced.
		if (options.Renderer != ErdRenderer.D2)
		{
			await File.WriteAllTextAsync(
					Path.Combine(outputDirectory, $"{options.FileName}.d2"),
					D2DSLGenerator.Generate(D2ModelBuilder.Build(model, options), options),
					cancellationToken
				)
				.ConfigureAwait(false);
		}

		List<string> written = [];

		foreach (var format in formats)
		{
			var image = Path.Combine(outputDirectory, $"{options.FileName}.{Extension(format)}");
			var (command, arguments) = BuildInvocation(options, outputDirectory, format);

			if (await RunAsync(command, arguments, options, logger, cancellationToken).ConfigureAwait(false))
			{
				written.Add(image);
				ErdLog.ImageExported(logger, image);
			}
		}

		return written;
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
			cts.CancelAfter(TimeSpan.FromSeconds(options.ExportTimeoutSeconds));

			// Read before waiting: d2 writes little, but a full pipe buffer would deadlock the wait.
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
}
