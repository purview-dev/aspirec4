using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// When running in local CLI mode (<c>WithLocalCLI()</c>), resolves the exact installed
	/// LikeC4 version by running <c>&lt;command&gt; [prefix…] --version</c> via the package
	/// manager. The version is always determinable for local CLI installations because the
	/// package manager resolves it from the local or cached package metadata.
	/// </summary>
	async Task TryResolveLocalCLIVersionAsync(CancellationToken cancellationToken)
	{
		var runtime = workspaceOptions.Value.LocalCLIRuntime;
		if (runtime is null)
			return;

		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(runtime.Value);

		var resolution = await LatestVersionResolver.TryResolveFromLocalCLIAsync(
			command,
			prefix,
			options.Value.ExternalProcessTimeoutSeconds,
			cancellationToken
		);

		RecordVersion(resolution, fallback: null);
	}

	/// <summary>
	/// Records the version the LikeC4 container will run: the pinned tag as given, or — for
	/// <c>latest</c> with <see cref="AspireC4DiagramOptions.CheckLatestImageVersion"/> on — the version a
	/// one-shot container of that image reports.
	/// </summary>
	/// <remarks>
	/// Runs during <c>BeforeStartEvent</c>, before the server's arguments are evaluated, so the resolved
	/// version also selects the HMR mode. <c>latest</c> parses as no version at all, which would otherwise
	/// keep every unpinned server on the fixed-port fallback.
	/// </remarks>
	async Task ResolveContainerVersionAsync(CancellationToken cancellationToken)
	{
		var opts = options.Value;
		var tag = opts.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;

		if (!ShouldProbeContainerVersion(tag, opts.CheckLatestImageVersion))
		{
			_resolvedLikeC4Version = tag;
			return;
		}

		var resolution = await LatestVersionResolver.TryResolveFromContainerAsync(
			opts.ContainerRuntimeCommand,
			LikeC4ServerResource.GetImageReference(tag),
			ContainerProbeTimeoutSeconds(opts.ExternalProcessTimeoutSeconds),
			cancellationToken
		);

		RecordVersion(resolution, fallback: tag);
	}

	/// <summary>
	/// The smallest budget the container probe gets, because it includes the image pull.
	/// </summary>
	/// <remarks>
	/// <see cref="AspireC4DiagramOptions.ExternalProcessTimeoutSeconds"/> is sized for the formatter, but
	/// a first pull of the LikeC4 image alone can outlast it, which left a fresh machine showing
	/// <c>latest</c>. The pull is not wasted: the server would wait for the same image before starting.
	/// </remarks>
	internal const int ContainerProbeMinimumTimeoutSeconds = 300;

	/// <summary>The probe's timeout: the configured one, but never less than the pull budget.</summary>
	internal static int ContainerProbeTimeoutSeconds(int externalProcessTimeoutSeconds) =>
		Math.Max(externalProcessTimeoutSeconds, ContainerProbeMinimumTimeoutSeconds);

	/// <summary>Whether the container has to be asked for its version rather than trusting the tag.</summary>
	internal static bool ShouldProbeContainerVersion(string tag, bool checkLatestImageVersion) =>
		checkLatestImageVersion
		&& string.Equals(tag, LikeC4ServerResource.DefaultTag, StringComparison.OrdinalIgnoreCase);

	void RecordVersion(VersionResolution resolution, string? fallback)
	{
		if (resolution.Version is { } version)
		{
			telemetry.ResolvedLikeC4Version(version);
			_resolvedLikeC4Version = version;
			workspaceOptions.Value.ResolvedVersion = version;
			return;
		}

		var command = resolution.Command ?? "(unknown)";
		var reason = resolution.Reason ?? "unknown";

		telemetry.FailedToResolveLikeC4Version(command, reason);
		ReportToDashboard($"AspireC4 could not determine the LikeC4 version with '{command}': {reason}");

		if (fallback is not null)
		{
			_resolvedLikeC4Version = fallback;
		}
	}
}
