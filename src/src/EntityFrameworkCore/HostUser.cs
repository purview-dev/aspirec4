using System.Runtime.InteropServices;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// The identity of the AppHost process, for a one-shot container that writes into the host's file
/// system.
/// </summary>
/// <remarks>
/// <para>
/// The d2 image runs as its own <c>debian</c> user (uid 1001) and uses <c>fixuid</c> to remap that user
/// to whatever <c>--user</c> passes. Without it the container cannot write into an images folder owned
/// by anyone else, which on Linux with a rootful Docker daemon is every folder: d2 fails with
/// <c>permission denied</c>. Running as root instead would succeed but leave a <c>0600</c> image owned
/// by root that the user cannot open, so the container runs as the AppHost's own uid and gid.
/// </para>
/// <para>
/// <see langword="null"/> on Windows, where Docker Desktop maps file ownership itself and the image's
/// default user already works.
/// </para>
/// </remarks>
static class HostUser
{
	/// <summary>
	/// The <c>uid:gid</c> to pass to <c>--user</c>, or <see langword="null"/> when the host has none.
	/// </summary>
	internal static string? ContainerUser { get; } = Resolve();

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
