namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;

/// <summary>
/// The d2 sidecar run from a locally installed <c>d2</c> binary instead of a container.
/// </summary>
/// <param name="name">The resource name.</param>
/// <param name="command">The command to run.</param>
/// <param name="workingDirectory">The working directory to run it in.</param>
/// <remarks>
/// Unlike the LikeC4 sidecar, this cannot fall back to a JavaScript runtime: d2 ships as a Go binary and
/// the <c>@d2lang/d2</c> npm package is a WASM library with no CLI entry point. The binary must be on
/// <c>PATH</c> — installed via scoop, Homebrew, <c>install.sh</c>, or <c>go install</c>.
/// </remarks>
public sealed class D2LocalServerResource(string name, string command, string workingDirectory)
	: ExecutableResource(name, command, workingDirectory)
{
	/// <summary>The executable expected on <c>PATH</c>.</summary>
	public const string DefaultCommand = "d2";
}
