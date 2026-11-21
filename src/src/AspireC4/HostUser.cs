using System.Runtime.InteropServices;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// The identity of the AppHost process, so a one-shot export container leaves its images owned by the
/// person who ran the AppHost rather than by the container's user.
/// </summary>
/// <remarks>
/// <para>
/// With a rootful Docker daemon on Linux a container's uid is the host's uid, so each export image
/// writes with whatever user it runs as, and each needs a different fix:
/// </para>
/// <list type="bullet">
/// <item><description>
/// The d2 image runs as its own <c>debian</c> user (uid 1001), which cannot write into a folder owned by
/// anyone else, and uses <c>fixuid</c> to remap that user to whatever <c>--user</c> passes. It runs with
/// <c>--user</c> <see cref="ContainerUser"/>.
/// </description></item>
/// <item><description>
/// The LikeC4 and Playwright images run as root, and cannot run as anyone else: LikeC4's Vite writes a
/// cache into the image's own <c>node_modules</c>. They stay root, and
/// <see cref="HandBackOwnership"/> gives the files they wrote back to <see cref="ContainerUser"/> before
/// the container exits. Otherwise the user is left with root-owned images they cannot overwrite.
/// </description></item>
/// </list>
/// <para>
/// <see langword="null"/> on Windows, where Docker Desktop maps file ownership itself. macOS Docker
/// Desktop does the same, so there it is harmless.
/// </para>
/// </remarks>
static class HostUser
{
	/// <summary>The <c>uid:gid</c> of the root user, which needs no hand-back.</summary>
	internal const string Root = "0:0";

	/// <summary>
	/// The AppHost's <c>uid:gid</c>, or <see langword="null"/> when the host has none.
	/// </summary>
	internal static string? ContainerUser { get; } = Resolve();

	/// <summary>
	/// Wraps a command that runs as root inside a container so that every file root wrote under
	/// <paramref name="outputPath"/> belongs to <paramref name="containerUser"/> afterwards.
	/// </summary>
	/// <param name="containerUser">The <c>uid:gid</c> to hand the files to, normally
	/// <see cref="ContainerUser"/>.</param>
	/// <param name="outputPath">The container path the command writes into.</param>
	/// <param name="command">The command and its arguments, starting with the executable.</param>
	/// <returns>
	/// The arguments that go before the image reference, and those that go after it. Without a user to
	/// hand back to, or when that user is root, the command is returned unchanged with nothing before
	/// the image, so the image's own entrypoint is not overridden.
	/// </returns>
	/// <remarks>
	/// The command's exit status is kept, so a failed export is still reported as one. Only files owned
	/// by root are changed, so anything the user already owns in a shared images folder is left alone.
	/// </remarks>
	internal static (IReadOnlyList<string> BeforeImage, IReadOnlyList<string> AfterImage) HandBackOwnership(
		string? containerUser,
		string outputPath,
		IReadOnlyList<string> command
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
		ArgumentNullException.ThrowIfNull(command);

		if (containerUser is null or Root)
		{
			return ([], command);
		}

		var script = $"\"$@\"; status=$?; find '{outputPath}' -user 0 -exec chown {containerUser} {{}} +; exit $status";

		return (["--entrypoint", "sh"], ["-c", script, "sh", .. command]);
	}

	static string? Resolve()
	{
		if (OperatingSystem.IsWindows())
		{
			return null;
		}

		try
		{
			return $"{NativeMethods.geteuid()}:{NativeMethods.getegid()}";
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
		{
			return null;
		}
	}

	// DllImport rather than LibraryImport: both signatures are blittable, so there is nothing to
	// marshal, and LibraryImport would turn on unsafe code for the whole shipped library.
#pragma warning disable SYSLIB1054
	static class NativeMethods
	{
		[DllImport("libc", ExactSpelling = true)]
		[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
		internal static extern uint geteuid();

		[DllImport("libc", ExactSpelling = true)]
		[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
		internal static extern uint getegid();
	}
#pragma warning restore SYSLIB1054
}
