using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.Lifecycle;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

sealed class ContainerWorkspaceOptions
{
	/// <summary>
	/// The LikeC4 container image tag used to determine HMR port compatibility.
	/// Set during <c>AddAspireC4</c> from <c>AspireC4DiagramOptions.ContainerImageTag</c>.
	/// </summary>
	public string? ImageTag { get; set; }

	/// <summary>
	/// The LikeC4 version actually running, when it differs from what <see cref="ImageTag"/> says —
	/// resolved at startup for the <c>latest</c> tag, or from the local CLI. <see langword="null"/> until
	/// then, or when it could not be determined.
	/// </summary>
	public string? ResolvedVersion { get; set; }

	/// <summary>
	/// The version that version-gated features are decided against: <see cref="ResolvedVersion"/> when
	/// known, else <see cref="ImageTag"/>.
	/// </summary>
	public string? EffectiveVersion => ResolvedVersion ?? ImageTag;

	/// <summary>
	/// The resolved HMR port to use for the LikeC4 server, both as the container target port
	/// and as the value passed via <c>--hmr-port</c> in Configurable mode.
	/// Defaults to <see cref="AspireC4Resource.DefaultHMRPort"/> (24678).
	/// Set during <c>AddAspireC4</c> from <c>AspireC4DiagramOptions.HMRPort</c> if configured.
	/// </summary>
	public int ResolvedHMRPort { get; set; } = AspireC4Resource.DefaultHMRPort;

	/// <summary>
	/// The resolved local CLI runtime when <c>WithLocalCLI()</c> was called.
	/// <see langword="null"/> means Docker container mode — host-side CLI invocations
	/// (format, validate) fall back to <c>npx</c>.
	/// </summary>
	public LocalCLIRuntime? LocalCLIRuntime { get; set; }

	/// <summary>
	/// The absolute path inside the container at which <c>likec4 start</c> is pointed.
	/// Equals <c><see cref="LikeC4ServerResource.WorkspacePath"/>/{relative-output-dir}</c>
	/// and is set by <see cref="AspireC4LifecycleHook"/> during <c>BeforeStartEvent</c>.
	/// </summary>
	public string ContainerServePath { get; set; } = LikeC4ServerResource.WorkspacePath;

	/// <summary>
	/// The AppHost directory (<c>IDistributedApplicationBuilder.AppHostDirectory</c>), which relative
	/// output, serve and image paths are resolved against at runtime, as they are at build time and by the
	/// ER diagram. Set during <c>AddAspireC4</c>.
	/// </summary>
	/// <remarks>
	/// Not the process's working directory: a TypeScript, Go or Python AppHost runs AspireC4 inside the
	/// Aspire CLI's AppHost server, whose working directory is under the AppHost's <c>.aspire/</c> folder,
	/// so resolving against it put the architecture diagram there and the ER diagram beside the AppHost.
	/// </remarks>
	public string? AppHostDirectory { get; set; }

	/// <summary>
	/// <see cref="AppHostDirectory"/>, or the current directory when it was never set (a lifecycle hook
	/// constructed without <c>AddAspireC4</c>, as in some tests).
	/// </summary>
	public string ResolveBaseDirectory() =>
		string.IsNullOrWhiteSpace(AppHostDirectory) ? Environment.CurrentDirectory : AppHostDirectory;
}
