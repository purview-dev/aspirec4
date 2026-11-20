using System.Diagnostics;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Generators;
using Aspire.Hosting.AspireC4.LikeC4.Models;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	// Project paths, runtime versions and container images, keyed by resource name. The app model is fixed
	// once the application starts, and reading it touches project files, so it is read on the first
	// generation and reused by every regeneration a state change triggers. Two concurrent first reads
	// produce the same value, so the race is harmless.
	IReadOnlyDictionary<string, IReadOnlyList<LikeC4Metadata>>? _resourceSourceMetadata;

	internal async Task WriteC4FileAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		var opts = options.Value;
		var appHostDirectory = workspaceOptions.Value.ResolveBaseDirectory();

		if (opts.AutoIncludeAspireMetadata.HasFlag(AspireMetadataInclusion.Metadata))
		{
			_resourceSourceMetadata ??= ResourceSourceMetadata.ReadAll(appModel.Resources, appHostDirectory);
		}

		telemetry.GeneratingLikeC4Model(appModel.Resources.Count, [.. appModel.Resources.Select(m => m.Name)]);

		// Build a snapshot of external endpoint URLs (resource name → [(url, name)]) so that
		// the model builder uses the correct public-port URLs from resource snapshots.
		IReadOnlyDictionary<string, IReadOnlyList<(string Url, string Name)>>? resourceSnapshotUrls = null;
		if (!_resourceExternalUrls.IsEmpty)
		{
			resourceSnapshotUrls = _resourceExternalUrls.ToDictionary(
				kvp => kvp.Key,
				kvp => (IReadOnlyList<(string Url, string Name)>)[.. kvp.Value],
				StringComparer.OrdinalIgnoreCase
			);
		}

		var dashboardBrowserToken = ResolveAspireBrowserToken(configuration, options.Value);
		var model = ModelBuilder.Build(
			[.. appModel.Resources],
			_resourceStates,
			opts.AutoIconsEnabled,
			opts.AutoIncludeAspireMetadata,
			opts.NormaliseMetadataBehaviour,
			opts.IconResolvers,
			opts.IncludeAspireDashboardLinks,
			_dashboardBaseUrl,
			dashboardBrowserToken,
			opts.StateTagMap,
			resourceSnapshotUrls,
			opts.ExcludedResourceTypes.Count > 0 ? opts.ExcludedResourceTypes : null,
			opts.ExcludedResourceTypeNames.Count > 0 ? opts.ExcludedResourceTypeNames : null,
			_resourceSourceMetadata,
			opts.TechnologyOverrides.Count > 0 ? opts.TechnologyOverrides : null
		);
		var dsl = LikeC4DSLGenerator.Generate(model, opts);

		var (outputDir, serveDir) = LikeC4ProjectLayout.Resolve(appHostDirectory, opts);
		Directory.CreateDirectory(outputDir);

		var filename = opts.FileName;
		if (!filename.EndsWith(".c4", StringComparison.OrdinalIgnoreCase))
			filename += ".c4";

		var outputPath = Path.Combine(outputDir, filename);

		// Two checks, for two different reasons. The in-memory one decides whether this is a new model at
		// all — it gates formatting and image export, and absorbs state changes that bounce a resource
		// through a transitional state and back. The on-disk one decides whether anything is written: the
		// in-memory value starts empty on every run, so without it each restart of an unchanged
		// application would rewrite the file and touch the working tree.
		var newRawBody = StripHeader(dsl);
		var changed = !string.Equals(_lastRawBody, newRawBody, StringComparison.Ordinal);
		var written = false;
		if (changed)
		{
			_lastRawBody = newRawBody;

			written = await GeneratedOutput.WriteIfChangedAsync(outputPath, dsl, cancellationToken);

			// Format the generated file in-place (best-effort) so the on-disk file is human-readable.
			if (written && opts.FormatGeneratedFile)
			{
				await RunFormatAsync(outputPath, outputDir, cancellationToken);
			}
		}

		// Copy additional user-provided DSL files into the output directory so LikeC4
		// picks them up as part of the workspace.
		List<string> generatedFileNames = [filename];
		foreach (var sourcePath in opts.AdditionalDSLFiles)
		{
			var absoluteSource = Path.GetFullPath(sourcePath);
			if (!File.Exists(absoluteSource))
			{
				// Most likely a typo in the path, which would otherwise look exactly like a file that
				// LikeC4 read and found nothing in.
				telemetry.AdditionalDSLFileMissing(absoluteSource);
				ReportToDashboard($"AspireC4 could not find the additional DSL file '{absoluteSource}'");
				continue;
			}

			var destFileName = Path.GetFileName(absoluteSource);
			generatedFileNames.Add(destFileName);

			if (GeneratedOutput.CopyIfChanged(absoluteSource, Path.Combine(outputDir, destFileName)))
			{
				telemetry.AdditionalDSLFileSynced(Path.GetFileNameWithoutExtension(absoluteSource));
			}
		}

		// Generate likec4.config.json when opted in (default) and AspireC4 owns the served project.
		if (opts.GenerateConfigFile && !LikeC4ProjectLayout.IsHandAuthoredProject(serveDir, outputDir))
		{
			await WriteConfigFileAsync(opts, outputDir, cancellationToken);
			generatedFileNames.Add(LikeC4ProjectLayout.ConfigFileName);
		}

		if (opts.GenerateGitIgnoreFile)
		{
			await WriteGitIgnoreAsync(outputDir, generatedFileNames, cancellationToken);
		}

		if (written)
		{
			telemetry.LikeC4ModelWritten(outputPath);
		}

		// Gated on the same comparison as the write itself: exporting is expensive enough that it is
		// worth doing only when there is a new model to render. Fire-and-forget, because the export
		// starts a LikeC4 server and a browser and must not sit between the application and its startup.
		// Never on publish, which generates the model and exits — see _isPublishMode.
		if (ShouldExportImages(changed, opts.ExportImages, _isPublishMode))
		{
			// appHostDirectory: the same base the output and serve directories were resolved from, so the
			// images land beside the ER diagram's rather than wherever this AppHost happens to have been started.
			_ = Task.Run(
				() => ExportImagesAsync(outputDir, serveDir, appHostDirectory, newRawBody, cancellationToken),
				cancellationToken
			);
		}
	}

	/// <summary>
	/// Returns everything after the closing <c>// ===…===</c> header delimiter, i.e. the
	/// stable body of the generated file with the timestamp stripped out.
	/// </summary>
	internal static string StripHeader(string content)
	{
		// The header is bounded by two "// ===" delimiter lines.
		// Skip past the end of the second delimiter to obtain the timestamp-free body.
		const string delimiter = "// ===";
		var first = content.IndexOf(delimiter, StringComparison.Ordinal);
		if (first < 0)
			return content;

		var firstLineEnd = content.IndexOf('\n', first);
		if (firstLineEnd < 0)
			return string.Empty;

		var second = content.IndexOf(delimiter, firstLineEnd, StringComparison.Ordinal);
		if (second < 0)
			return content;

		var lineEnd = content.IndexOf('\n', second);
		return lineEnd < 0 ? string.Empty : content[(lineEnd + 1)..];
	}

	/// <summary>
	/// Returns the executable and argument prefix for invoking <c>likec4</c> via the configured
	/// runtime. In Docker mode (<see cref="ContainerWorkspaceOptions.LocalCLIRuntime"/> is
	/// <see langword="null"/>), falls back to <c>npx</c> since the host still needs a JS runner
	/// for host-side operations such as format.
	/// </summary>
	(string Command, string[] Prefix) BuildCLIPrefix() =>
		workspaceOptions.Value.LocalCLIRuntime is { } runtime
			? AspireC4Builder.BuildLikeC4CLIPrefix(runtime)
			: ("npx", ["likec4"]);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Formatting is best-effort; failures are reported via telemetry and never block regeneration"
	)]
	async Task RunFormatAsync(string outputPath, string outputDir, CancellationToken cancellationToken)
	{
		try
		{
			var opts = options.Value;
			var (command, prefix) = BuildCLIPrefix();
			ProcessStartInfo startInfo = new()
			{
				FileName = command,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = outputDir,
			};

			foreach (var arg in prefix)
				startInfo.ArgumentList.Add(arg);

			startInfo.ArgumentList.Add("format");
			startInfo.ArgumentList.Add("--files");
			startInfo.ArgumentList.Add(outputPath);
			startInfo.ArgumentList.Add(outputDir);

			using var process = Process.Start(startInfo);
			if (process is null)
				return;

			// Both streams are redirected, so both are drained: one left unread fills its pipe and blocks
			// the formatter until the timeout kills it.
			var (_, error) = await ProcessOutput.WaitAsync(
				process,
				TimeSpan.FromSeconds(opts.ExternalProcessTimeoutSeconds),
				cancellationToken
			);

			if (process.ExitCode == 0)
			{
				telemetry.LikeC4FormatApplied();
			}
			else
			{
				// The unformatted file is still valid LikeC4, so this is worth a warning, not a failure.
				telemetry.FormatterExited(process.ExitCode, error.Trim());
				ReportToDashboard(
					$"AspireC4 could not format the generated model (exit code {process.ExitCode}): {error.Trim()}"
				);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown.
		}
		catch (Exception ex)
		{
			// Formatting is best-effort; never block startup or regeneration.
			telemetry.FailedToRunFormatter(ex);
			ReportToDashboard("AspireC4 could not run the formatter on the generated model", ex);
		}
	}

	static async Task WriteConfigFileAsync(
		AspireC4DiagramOptions opts,
		string outputDir,
		CancellationToken cancellationToken
	)
	{
		// Paths in the config are relative to the output directory. Because all referenced folders
		// (DSL folders, image-alias folders) are accessible via the single bind mount, these same
		// relative paths work correctly inside the container without any translation.
		var includePaths = opts
			.AdditionalDSLFolders.Select(absoluteFolder =>
				Path.GetRelativePath(outputDir, absoluteFolder).Replace('\\', '/')
			)
			.ToList();

		var aliases = opts.ImageAliases.ToDictionary(
			kvp => kvp.Key,
			kvp => Path.GetRelativePath(outputDir, kvp.Value).Replace('\\', '/'),
			StringComparer.OrdinalIgnoreCase
		);

		var config = LikeC4ConfigGenerator.Generate(
			"aspirec4",
			opts.Title,
			includePaths,
			aliases,
			opts.ConfigFileMetadata
		);
		var configPath = Path.Combine(outputDir, LikeC4ProjectLayout.ConfigFileName);
		await GeneratedOutput.WriteIfChangedAsync(configPath, config, cancellationToken);
	}

	async Task WriteGitIgnoreAsync(
		string outputDir,
		IEnumerable<string> generatedFileNames,
		CancellationToken cancellationToken
	)
	{
		var result = await GeneratedOutput.WriteGitIgnoreAsync(
			outputDir,
			"The architecture diagram's generated LikeC4 model. Hand-written .c4 files are not listed.",
			generatedFileNames,
			cancellationToken
		);

		if (result == GitIgnoreWrite.KeptUserFile)
		{
			telemetry.GitIgnoreFileKept(Path.Combine(outputDir, GeneratedOutput.GitIgnoreFileName));
		}
	}
}
