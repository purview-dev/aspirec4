using System.Diagnostics;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Generators;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	async Task WriteC4FileAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		var opts = options.Value;

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
			opts.ExcludedResourceTypes.Count > 0 ? opts.ExcludedResourceTypes : null
		);
		var dsl = LikeC4DSLGenerator.Generate(model, opts);

		var outputDir = Path.GetFullPath(opts.OutputDirectory);
		Directory.CreateDirectory(outputDir);

		var filename = opts.FileName;
		if (!filename.EndsWith(".c4", StringComparison.OrdinalIgnoreCase))
			filename += ".c4";

		var outputPath = Path.Combine(outputDir, filename);

		// Skip writing (and formatting) when only the timestamp changed — i.e. the stable body
		// is identical to what was last written.  This prevents needless git noise when state
		// changes bounce resources through transitional states that end up back where they started.
		var newRawBody = StripHeader(dsl);
		if (!string.Equals(_lastRawBody, newRawBody, StringComparison.Ordinal))
		{
			_lastRawBody = newRawBody;
			await File.WriteAllTextAsync(outputPath, dsl, cancellationToken);

			// Format the generated file in-place (best-effort) so the on-disk file is human-readable.
			if (opts.FormatGeneratedFile)
			{
				await RunFormatAsync(outputPath, outputDir, cancellationToken);
			}
		}

		// Copy additional user-provided DSL files into the output directory so LikeC4
		// picks them up as part of the workspace.
		List<string> additionalDestPaths = [];
		foreach (var sourcePath in opts.AdditionalDSLFiles)
		{
			var absoluteSource = Path.GetFullPath(sourcePath);
			if (!File.Exists(absoluteSource))
			{
				continue;
			}

			var destFileName = Path.GetFileName(absoluteSource);
			var destPath = Path.Combine(outputDir, destFileName);
			File.Copy(absoluteSource, destPath, overwrite: true);
			additionalDestPaths.Add(destPath);

			telemetry.AdditionalDSLFileSynced(Path.GetFileNameWithoutExtension(absoluteSource));
		}

		// Generate likec4.config.json when opted in (default).
		if (opts.GenerateConfigFile)
		{
			await WriteConfigFileAsync(opts, outputDir, cancellationToken);
		}

		telemetry.LikeC4ModelWritten(outputPath);
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
		Justification = "Formatting is non-blocking; failures are silently ignored"
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

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromSeconds(opts.ExternalProcessTimeoutSeconds));

			try
			{
				await process.WaitForExitAsync(cts.Token);
			}
			catch (OperationCanceledException)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch { }

				throw;
			}

			if (process.ExitCode == 0)
				telemetry.LikeC4FormatApplied();
		}
		catch (Exception ex)
		{
			// Formatting is best-effort; never block startup or regeneration.
			telemetry.FailedToRunFormatter(ex);
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
		var configPath = Path.Combine(outputDir, "likec4.config.json");
		await File.WriteAllTextAsync(configPath, config, cancellationToken);
	}
}
