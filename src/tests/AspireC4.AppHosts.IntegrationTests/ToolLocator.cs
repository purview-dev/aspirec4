namespace Aspire.Hosting.AspireC4.AppHosts;

/// <summary>Finds command-line tools on <c>PATH</c> without running them.</summary>
static class ToolLocator
{
	/// <summary>
	/// <see langword="true"/> when <c>ASPIREC4_REQUIRE_APPHOSTS</c> is <c>true</c> or <c>1</c>, as in the
	/// CI job that gates on the polyglot AppHosts: a missing tool then fails the test instead of skipping it.
	/// </summary>
	public static bool ToolsRequired => RequiredEnvironment.AppHostsRequired;

	/// <summary>
	/// Whether <paramref name="tool"/> resolves from <c>PATH</c>, honouring <c>PATHEXT</c> on Windows, where
	/// <c>npm</c> is <c>npm.cmd</c>.
	/// </summary>
	public static bool IsOnPath(string tool) => Resolve(tool) is not null;

	/// <summary>The full path <paramref name="tool"/> resolves to, or <see langword="null"/>.</summary>
	public static string? Resolve(string tool)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tool);

		string[] extensions = OperatingSystem.IsWindows()
			?
			[
				.. (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(
					';',
					StringSplitOptions.RemoveEmptyEntries
				),
			]
			: [""];

		foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
		{
			if (directory.Length == 0)
				continue;

			foreach (var extension in extensions)
			{
				var candidate = Path.Combine(directory, tool + extension);
				if (File.Exists(candidate))
					return candidate;
			}
		}

		return null;
	}
}
