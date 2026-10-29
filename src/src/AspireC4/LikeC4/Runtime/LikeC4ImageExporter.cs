using System.Diagnostics;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.Lifecycle;
using Purview.Containers.Environment;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Renders the LikeC4 views to image files with <c>likec4 export</c>.
/// </summary>
/// <remarks>
/// <para>
/// Export drives a headless browser, which is why it runs in the <c>ghcr.io/likec4/likec4</c> image by
/// default: that image already carries the Chromium build and the Graphviz binaries LikeC4 lays out
/// with. In local CLI mode the host's own <c>likec4</c> is used instead, which needs a Playwright
/// Chromium installed on the host.
/// </para>
/// <para>
/// Every view in the project is exported, not only the generated one — a hand-authored project's views
/// are part of the same model and are laid out by the same pass, so excluding them would mean asking
/// LikeC4 to do the work and then discarding most of it.
/// </para>
/// </remarks>
static class LikeC4ImageExporter
{
	/// <summary>The directory the images mount is exposed at inside the export container.</summary>
	internal const string ContainerOutputPath = "/out";

	/// <summary>
	/// How long, in seconds, the headless browser is given per view. The CLI's own default of 15 is
	/// tight for a large generated view on a cold start.
	/// </summary>
	internal const int PerViewTimeoutSeconds = 60;

	/// <summary>The <c>likec4 export</c> format name for a given image format.</summary>
	internal static string FormatName(LikeC4ImageFormat format) => format == LikeC4ImageFormat.Jpg ? "jpg" : "png";

	/// <summary>
	/// The formats to export, in a stable order.
	/// </summary>
	/// <remarks>
	/// <see cref="AspireC4DiagramOptions.ImageFormats"/> is a set, whose enumeration order depends on
	/// insertion and on whether configuration binding appended to it.
	/// </remarks>
	internal static IReadOnlyList<LikeC4ImageFormat> FormatsToExport(AspireC4DiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return [.. options.ImageFormats.Distinct().Order()];
	}

	/// <summary>Resolves the directory images are written to, as an absolute path.</summary>
	internal static string ResolveImageDirectory(AspireC4DiagramOptions options, string appHostDirectory)
	{
		ArgumentNullException.ThrowIfNull(options);

		return AspireC4ImageExport.Resolve(options.ImageExportDirectory, appHostDirectory);
	}

	/// <summary>
	/// Builds the command, arguments and working directory for one <c>likec4 export</c> invocation.
	/// </summary>
	/// <param name="options">The diagram options.</param>
	/// <param name="outputDirectory">Where the generated model was written.</param>
	/// <param name="serveDirectory">The LikeC4 project root, which is what gets exported.</param>
	/// <param name="imageDirectory">Where the images are written.</param>
	/// <param name="format">The format to export.</param>
	/// <param name="localCLI">
	/// The host command and argument prefix when running the local CLI, or <see langword="null"/> to run
	/// the one-shot container.
	/// </param>
	internal static (string Command, IReadOnlyList<string> Arguments, string WorkingDirectory) BuildInvocation(
		AspireC4DiagramOptions options,
		string outputDirectory,
		string serveDirectory,
		string imageDirectory,
		LikeC4ImageFormat format,
		(string Command, string[] Prefix)? localCLI
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(serveDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);

		if (localCLI is { } cli)
		{
			return (
				cli.Command,
				[.. cli.Prefix, .. ExportArguments(FormatName(format), imageDirectory, serveDirectory)],
				// LikeC4 resolves the relative include paths and image aliases in likec4.config.json
				// against the process working directory, so this has to match what the server and the
				// formatter use or those paths climb out of the project.
				outputDirectory
			);
		}

		// The container sees the host tree through a single bind mount, exactly as the server does, so
		// the config's relative paths resolve to the same files either way.
		var workspaceRoot = AspireC4LifecycleHook.ComputeCommonAncestor([
			outputDirectory,
			serveDirectory,
			.. options.AdditionalDSLFolders.Select(Path.GetFullPath),
			.. options.ImageAliases.Values.Select(Path.GetFullPath),
		]);

		var containerServePath = ToContainerPath(workspaceRoot, serveDirectory);
		var tag = options.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;

		return (
			options.ContainerRuntimeCommand,
			[
				"run",
				"--rm",
				// Read-only, like the server's mount: nothing in the source tree should be written by an
				// export. The images land on their own writable mount instead.
				//
				// --mount rather than -v: a Windows host path already contains a colon, so
				// "C:\…\likec4:/data:ro" leaves the runtime guessing where the drive letter ends and the
				// mode begins, and it guesses wrong. --mount parses key=value and cannot be ambiguous.
				"--mount",
				new ContainerBindMount(workspaceRoot, LikeC4ServerResource.WorkspacePath, IsReadOnly: true).ToString(),
				"--mount",
				new ContainerBindMount(imageDirectory, ContainerOutputPath).ToString(),
				"--workdir",
				ToContainerPath(workspaceRoot, outputDirectory),
				LikeC4ServerResource.GetImageReference(tag),
				.. ExportArguments(FormatName(format), ContainerOutputPath, containerServePath),
			],
			workspaceRoot
		);
	}

	static IReadOnlyList<string> ExportArguments(string format, string imageDirectory, string serveDirectory) =>
		[
			"export",
			format,
			"-o",
			imageDirectory,
			// Without this the output mirrors the source tree, so an image's path depends on where the
			// .c4 file happened to live rather than on the view it came from.
			"--flat",
			// One view that fails to render should not discard the ones that succeeded.
			"-i",
			"-t",
			PerViewTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
			serveDirectory,
		];

	/// <summary>Maps a host path beneath the workspace root onto its path inside the container.</summary>
	static string ToContainerPath(string workspaceRoot, string path)
	{
		var relative = Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/');

		return relative is "." or ""
			? LikeC4ServerResource.WorkspacePath
			: $"{LikeC4ServerResource.WorkspacePath}/{relative}";
	}

	/// <summary>Renders every configured format, returning how many invocations succeeded.</summary>
	/// <remarks>
	/// Best-effort throughout, like the formatter: a failed export leaves the generated model untouched,
	/// so the only correct response is to report it and carry on.
	/// </remarks>
	/// <param name="options">The diagram options.</param>
	/// <param name="outputDirectory">Where the generated model was written.</param>
	/// <param name="serveDirectory">The LikeC4 project root, which is what gets exported.</param>
	/// <param name="appHostDirectory">The AppHost directory the images directory is resolved against.</param>
	/// <param name="modelBody">
	/// The generated model, with its timestamp header stripped, recorded so an unchanged model renders
	/// nothing on the next run.
	/// </param>
	/// <param name="localCLI">The host CLI to run, or <see langword="null"/> for the container.</param>
	/// <param name="telemetry">Where export progress and failures are reported.</param>
	/// <param name="cancellationToken">Cancelled on shutdown.</param>
	public static async Task<int> ExportAsync(
		AspireC4DiagramOptions options,
		string outputDirectory,
		string serveDirectory,
		string appHostDirectory,
		string modelBody,
		(string Command, string[] Prefix)? localCLI,
		IAspireC4LifecycleHookTelemetry telemetry,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(telemetry);

		var formats = FormatsToExport(options);

		if (formats.Count == 0)
		{
			return 0;
		}

		var imageDirectory = ResolveImageDirectory(options, appHostDirectory);

		// Checked before anything is run. The AppHost's own change detection resets every time it
		// restarts, so without this a restart re-renders views that are already correct on disk — and
		// an export starts a LikeC4 server and a headless browser to do it.
		var stamp = ImageExportStamp.Compute(modelBody, string.Join(",", formats.Select(FormatName)));

		if (
			await ImageExportStamp
				.IsCurrentAsync(imageDirectory, StampName, stamp, ExistingImages(imageDirectory), cancellationToken)
				.ConfigureAwait(false)
		)
		{
			telemetry.ImagesUnchanged(imageDirectory);

			return 0;
		}

		// Created here rather than left to the container runtime: a missing bind-mount source is created
		// by the daemon as root, which a later host-side write could not then touch.
		Directory.CreateDirectory(imageDirectory);

		var exported = 0;

		foreach (var format in formats)
		{
			var name = FormatName(format);

			// Exported into a staging directory and moved in afterwards, because the CLI names each file
			// after its view id with no say in the matter. In a folder shared with the ER diagram a bare
			// "index.png" says nothing about which diagram it came from, and could collide outright.
			var staging = Path.Combine(Path.GetTempPath(), $"aspirec4-export-{Guid.NewGuid():N}");

			Directory.CreateDirectory(staging);

			try
			{
				var (command, arguments, workingDirectory) = BuildInvocation(
					options,
					outputDirectory,
					serveDirectory,
					staging,
					format,
					localCLI
				);

				telemetry.ExportingImages(name, imageDirectory);

				if (
					await RunAsync(command, arguments, workingDirectory, options, name, telemetry, cancellationToken)
						.ConfigureAwait(false)
				)
				{
					exported++;

					MoveWithPrefix(staging, imageDirectory);

					telemetry.ImagesExported(name, imageDirectory);
				}
			}
			finally
			{
				TryDelete(staging);
			}
		}

		// Only once every requested format landed: a partial export must not be mistaken for a current
		// one, or the formats that failed would never be retried.
		if (exported == formats.Count)
		{
			await ImageExportStamp
				.WriteAsync(imageDirectory, StampName, stamp, cancellationToken)
				.ConfigureAwait(false);
		}

		return exported;
	}

	/// <summary>Distinguishes this diagram's stamp from the ER diagram's in the shared folder.</summary>
	internal const string StampName = "architecture";

	/// <summary>
	/// The images this diagram has already written, which the stamp alone cannot stand in for.
	/// </summary>
	/// <remarks>
	/// The view names are only known once the CLI has run, so unlike the ER diagram there is no list of
	/// expected files to check against — the test is whether this diagram left anything behind at all.
	/// Deleting one view's image and keeping the rest is therefore not noticed; deleting the folder is.
	/// </remarks>
	static string[] ExistingImages(string imageDirectory)
	{
		var missing = Path.Combine(imageDirectory, "(none)");

		if (!Directory.Exists(imageDirectory))
		{
			return [missing];
		}

		var existing = Directory
			.EnumerateFiles(imageDirectory, $"{AspireC4ImageExport.ArchitecturePrefix}*")
			.Take(1)
			.ToArray();

		return existing.Length == 0 ? [missing] : existing;
	}

	/// <summary>Moves every staged image into the shared folder under its prefixed name.</summary>
	/// <remarks>
	/// Overwrites, so a view that has been re-exported replaces its previous image rather than
	/// accumulating. A view removed from the model leaves its last image behind, which is the lesser
	/// evil: emptying the folder would also delete the ER diagram's images sitting beside them.
	/// </remarks>
	static void MoveWithPrefix(string staging, string imageDirectory)
	{
		foreach (var file in Directory.EnumerateFiles(staging))
		{
			var destination = Path.Combine(
				imageDirectory,
				AspireC4ImageExport.ArchitecturePrefix + Path.GetFileName(file)
			);

			File.Move(file, destination, overwrite: true);
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

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Image export is best-effort; failures are reported via telemetry and never block startup"
	)]
	static async Task<bool> RunAsync(
		string command,
		IReadOnlyList<string> arguments,
		string workingDirectory,
		AspireC4DiagramOptions options,
		string format,
		IAspireC4LifecycleHookTelemetry telemetry,
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
				WorkingDirectory = workingDirectory,
			};

			foreach (var argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}

			if (command == options.ContainerRuntimeCommand)
			{
				await ContainerEnvironmentResolver
					.ForRuntime(command)
					.ResolveBindMountsAsync(startInfo, cancellationToken)
					.ConfigureAwait(false);
			}

			using var process = Process.Start(startInfo);

			if (process is null)
			{
				telemetry.FailedToExportImages(format, "the process could not be started");

				return false;
			}

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromSeconds(options.ImageExportTimeoutSeconds));

			// Drained before waiting: likec4 export is verbose, and a full pipe would deadlock the wait.
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

			telemetry.FailedToExportImages(
				format,
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
			telemetry.FailedToExportImages(format, ex.Message);

			return false;
		}
	}
}
