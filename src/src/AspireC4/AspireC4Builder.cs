namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Internal utility methods shared between <see cref="AspireC4ResourceExtensions"/> and the lifecycle hook.
/// </summary>
static class AspireC4Builder
{
	public static LocalCLIRuntime DetectRuntime()
	{
		// Try runtimes in order of preference.
		(LocalCLIRuntime Runtime, string Executable)[] candidates =
		[
			(LocalCLIRuntime.Npx, "npx"),
			(LocalCLIRuntime.Pnpm, "pnpm"),
			(LocalCLIRuntime.Yarn, "yarn"),
			(LocalCLIRuntime.Bun, "bun"),
			(LocalCLIRuntime.Deno, "deno"),
		];

		foreach (var (candidate, executable) in candidates)
		{
			if (IsExecutableOnPath(executable))
				return candidate;
		}

		throw new DistributedApplicationException(
			"No supported JavaScript package manager was found on the system PATH. "
				+ "Install one of: Node.js (npx), pnpm, yarn, bun, or Deno, then retry. "
				+ "Alternatively, remove WithLocalCLI() to use the Docker container (default)."
		);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "If the exe isn't there or isn't correctly installed, it's not appropriate for use"
	)]
	public static bool IsExecutableOnPath(string executable)
	{
		try
		{
			using var process = System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = executable,
					Arguments = "--version",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);

			if (process is null)
				return false;

			// A missing executable fails Process.Start straight away, so the wait only bounds an
			// installed one that is slow to answer: npx is a Node script behind a shell shim, and on a
			// loaded machine it took longer than the 3s this used to allow. Reading ExitCode before the
			// process exits throws, which the catch below turned into "not installed".
			if (!process.WaitForExit(VersionProbeTimeout))
			{
				process.Kill(entireProcessTree: true);
				return false;
			}

			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>How long a runtime's <c>--version</c> probe may take before it counts as unusable.</summary>
	static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(15);

	/// <summary>
	/// Returns the executable and the argument prefix required to invoke <c>likec4</c> via
	/// the given runtime — i.e. everything that comes BEFORE the likec4 subcommand.
	/// Internal and visible for testing.
	/// </summary>
	/// <example>
	/// Npx  → <c>("npx",  ["likec4"])</c> so the full call is <c>npx likec4 format ...</c>
	/// Pnpm → <c>("pnpm", ["dlx", "likec4"])</c>
	/// Yarn → <c>("yarn", ["dlx", "likec4"])</c>
	/// Bun  → <c>("bunx", ["--bun", "likec4"])</c>
	/// Deno → <c>("deno", ["run", "--allow-all", "npm:likec4"])</c>
	/// </example>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0072:Add missing cases",
		Justification = "Already handled"
	)]
	public static (string Command, string[] Prefix) BuildLikeC4CLIPrefix(LocalCLIRuntime runtime) =>
		runtime switch
		{
			LocalCLIRuntime.Npx => ("npx", ["likec4"]),
			LocalCLIRuntime.Pnpm => ("pnpm", ["dlx", "--ignore-workspace", "likec4"]),
			// Yarn Berry's dlx does not install optional peer dependencies (react, react-dom) by
			// default. Explicitly pass them as --package arguments so they are present in the
			// isolated environment and likec4 can resolve them at startup.
			LocalCLIRuntime.Yarn => (
				"yarn",
				["dlx", "--package", "likec4", "--package", "react", "--package", "react-dom", "likec4"]
			),
			LocalCLIRuntime.Bun => ("bunx", ["likec4"]),
			LocalCLIRuntime.Deno => ("deno", ["run", "--allow-all", "npm:likec4"]),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};

	/// <summary>
	/// Resolves the executable command and arguments for the given local CLI runtime.
	/// Internal and visible for testing.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0072:Add missing cases",
		Justification = "Already handled"
	)]
	public static (string Command, string[] Args) BuildLocalCLICommand(
		LocalCLIRuntime runtime,
		string outputDirectory,
		int port,
		int? hmrPort = null
	)
	{
		var portStr = $"{port}";
		string[] HmrArgs() => hmrPort is int p ? ["--hmr-port", $"{p}"] : [];
		return runtime switch
		{
			LocalCLIRuntime.Npx => ("npx", ["likec4", "serve", outputDirectory, "--port", portStr, .. HmrArgs()]),
			LocalCLIRuntime.Pnpm => (
				"pnpm",
				["dlx", "--ignore-workspace", "likec4", "serve", outputDirectory, "--port", portStr, .. HmrArgs()]
			),
			// Explicitly include react and react-dom so yarn dlx adds them to the isolated
			// environment alongside likec4 (peer deps are not installed automatically in Berry).
			LocalCLIRuntime.Yarn => (
				"yarn",
				[
					"dlx",
					"--package",
					"likec4",
					"--package",
					"react",
					"--package",
					"react-dom",
					"likec4",
					"serve",
					outputDirectory,
					"--port",
					portStr,
					.. HmrArgs(),
				]
			),
			LocalCLIRuntime.Bun => (
				"bunx",
				["--bun", "likec4", "serve", outputDirectory, "--port", portStr, .. HmrArgs()]
			),
			// --node-modules-dir=none tells deno to use its virtual module cache rather than
			// creating a physical node_modules tree in the working directory (slow for 130+ pkgs).
			LocalCLIRuntime.Deno => (
				"deno",
				[
					"run",
					"--allow-all",
					"--node-modules-dir=none",
					"npm:likec4",
					"serve",
					outputDirectory,
					"--port",
					portStr,
					.. HmrArgs(),
				]
			),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};
	}
}
