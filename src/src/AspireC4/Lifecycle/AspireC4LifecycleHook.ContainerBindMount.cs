using Aspire.Hosting.AspireC4.ApplicationModel;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// Computes the single common-ancestor bind mount for the container, adds it to the
	/// <see cref="LikeC4ServerResource"/>, and appends the <c>likec4 start</c> command-line arguments.
	/// </summary>
	internal void SetupContainerBindMount(DistributedApplicationModel _, LikeC4ServerResource serverResource)
	{
		// Log before resolving options so that a future deadlock in this area (e.g. if the
		// lazy IOptions.Configure callback ever blocks on the NonConcurrentSynchronizationContext)
		// leaves a clear breadcrumb in the log rather than silent 60-second timeout.
		// See: https://github.com/microsoft/aspire/issues/17487
		telemetry.ApplyingDiagramOptionsSnapshot();

		var opts = options.Value;

		telemetry.DiagramOptionsSnapshotApplied(
			Path.GetFileName(opts.OutputDirectory),
			opts.FormatGeneratedFile,
			opts.DisableHMR
		);
		var outputDir = Path.GetFullPath(opts.OutputDirectory);

		// Collect all host-side directory paths that must be visible inside the container.
		List<string> allPaths =
		[
			outputDir,
			.. opts.AdditionalDSLFolders.Select(Path.GetFullPath),
			.. opts.ImageAliases.Values.Select(Path.GetFullPath),
		];

		var commonAncestor = ComputeCommonAncestor(allPaths);
		var normalizedSource = commonAncestor;

		serverResource.Annotations.Add(
			new ContainerMountAnnotation(
				normalizedSource,
				LikeC4ServerResource.WorkspacePath,
				ContainerMountType.BindMount,
				isReadOnly: true
			)
		);

		// Compute the container path for the output directory: /data/{rel-from-ancestor-to-outputDir}
		var relOutputDir = Path.GetRelativePath(commonAncestor, outputDir).Replace('\\', '/');
		var servePath = $"{LikeC4ServerResource.WorkspacePath}/{relOutputDir}";
		// ContainerServePath is read by the WithArgs callback registered at configure time in AddAspireC4.
		workspaceOptions.Value.ContainerServePath = servePath;

		// The LikeC4 container image sets WORKDIR to /data and LikeC4 resolves relative paths in
		// likec4.config.json (imageAliases, include.paths) against the process working directory.
		// The host-side invocations (format, validate) run with the output directory as cwd, so the
		// container must run `likec4 start` with that same directory as its working directory too —
		// otherwise those relative paths climb above the bind-mount root (/data) and fail with
		// ENOENT (e.g. "/assets/images/..."). Override the image WORKDIR via --workdir so the
		// container cwd matches the output directory, mirroring the host.
		serverResource.Annotations.Add(
			new ContainerRuntimeArgsCallbackAnnotation(args =>
			{
				args.Add("--workdir");
				args.Add(servePath);
			})
		);
	}

	/// <summary>
	/// Returns the common ancestor directory of all provided absolute paths.
	/// All paths must reside on the same drive (Windows) or under the same root (Unix).
	/// </summary>
	internal static string ComputeCommonAncestor(IReadOnlyList<string> absolutePaths)
	{
		if (absolutePaths.Count == 0)
			throw new ArgumentException("At least one path is required.", nameof(absolutePaths));

		var sep = Path.DirectorySeparatorChar;

		// Normalize each path: absolute + trailing separator for reliable prefix matching.
		static string WithTrailingSep(string p) =>
			Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;

		var first = WithTrailingSep(absolutePaths[0]);
		var commonPrefix = first;
		var pathRoot = Path.GetPathRoot(first)!;

		foreach (var path in absolutePaths.Skip(1))
		{
			var normalized = WithTrailingSep(path);

			while (!normalized.StartsWith(commonPrefix, StringComparison.OrdinalIgnoreCase))
			{
				if (commonPrefix == pathRoot)
					throw new InvalidOperationException(
						"The provided paths share no common ancestor directory. "
							+ "Ensure all paths used by AddAspireC4 reside on the same drive."
					);

				// Back up one directory level.
				var trimmed = commonPrefix[..^1]; // Remove trailing separator
				var idx = trimmed.LastIndexOf(sep);
				commonPrefix = idx >= 0 ? trimmed[..(idx + 1)] : pathRoot;
			}
		}

		// Return without trailing separator, unless that would yield an invalid rooted path.
		var result = commonPrefix.TrimEnd(sep);
		return Path.IsPathRooted(result) ? result : commonPrefix;
	}
}
