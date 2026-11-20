using System.Diagnostics;
using System.Text;

namespace Aspire.Hosting.AspireC4.AppHosts;

/// <summary>
/// One polyglot sample AppHost under <c>samples/</c>, driven through the Aspire CLI from its own folder,
/// which is where the CLI reads the sample's <c>aspire.config.json</c> (and, for Python, its
/// experimental-feature switch) from.
/// </summary>
/// <remarks>
/// The diagram is redirected to a temporary folder through <c>AspireC4__OutputDirectory</c>: AspireC4
/// binds its options from the <c>AspireC4</c> configuration section, so the samples need no test-only
/// code. Image export is switched off the same way, because it drives a headless browser for every view.
/// </remarks>
sealed class SampleAppHost : IAsyncDisposable
{
	static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(10);
	static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(10);
	static readonly TimeSpan ModelTimeout = TimeSpan.FromMinutes(2);

	readonly string _directory;
	readonly string _outputDirectory;
	bool _started;

	SampleAppHost(string sample, string directory, string outputDirectory)
	{
		Sample = sample;
		_directory = directory;
		_outputDirectory = outputDirectory;
	}

	public string Sample { get; }

	string ModelPath => Path.Combine(_outputDirectory, "model.gen.c4");

	string ErOutputDirectory => Path.Combine(_outputDirectory, "erd");

	string ErModelPath => Path.Combine(ErOutputDirectory, "schema.gen.json");

	/// <summary>Restores the sample's dependencies and generates its AspireC4 bindings.</summary>
	public static async Task<SampleAppHost> PrepareAsync(string sample, CancellationToken cancellationToken)
	{
		var root = FindRepositoryRoot();
		var directory = Path.Combine(root, "samples", sample);
		if (!Directory.Exists(directory))
			throw new DirectoryNotFoundException($"The sample AppHost '{sample}' was not found at '{directory}'.");

		// Inside the repository (artifacts/ is gitignored), not the system temp folder: AspireC4 bind-mounts
		// the common ancestor of the output folder and the samples' shared assets into the LikeC4
		// container, and a temp folder on another drive has none.
		var outputDirectory = Path.Combine(root, "artifacts", "apphost-contract", $"{sample}-{Guid.NewGuid():N}"[..40]);
		Directory.CreateDirectory(outputDirectory);

		SampleAppHost appHost = new(sample, directory, outputDirectory);

		if (File.Exists(Path.Combine(directory, "package.json")))
			await appHost.RunAsync("bun", ["install"], RestoreTimeout, cancellationToken);

		await appHost.RunAsync(
			"aspire",
			["restore", "--non-interactive", "--nologo"],
			RestoreTimeout,
			cancellationToken
		);

		return appHost;
	}

	/// <summary>Starts the AppHost in the background and waits for the AspireC4 resource to be up.</summary>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_started = true;
		await RunAsync("aspire", ["start", "--non-interactive", "--nologo"], StartTimeout, cancellationToken);
		await RunAsync(
			"aspire",
			["wait", "aspirec4", "--status", "up", "--timeout", $"{(int)StartTimeout.TotalSeconds}", "--nologo"],
			StartTimeout + TimeSpan.FromMinutes(1),
			cancellationToken
		);
	}

	/// <summary>
	/// Waits until the generated model satisfies the contract, returning the last violations if it never does.
	/// </summary>
	/// <remarks>
	/// AspireC4 rewrites the model as resources change state, so a model read straight after startup can
	/// still be catching up; one that never converges is a real failure.
	/// </remarks>
	public async Task<IReadOnlyList<string>> WaitForContractAsync(CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(ModelTimeout);

		IReadOnlyList<string> violations = [$"{Sample} wrote no model to '{ModelPath}'"];

		try
		{
			while (true)
			{
				if (File.Exists(ModelPath))
				{
					violations = SampleAppHostContract.Verify(await ReadSharedAsync(ModelPath, timeout.Token));

					// The ER diagram is part of the contract too: it is how the bindings' ER options are
					// proven to reach the model, not merely to compile.
					violations = File.Exists(ErModelPath)
						?
						[
							.. violations,
							.. SampleAppHostContract.VerifyErModel(await ReadSharedAsync(ErModelPath, timeout.Token)),
						]
						: [.. violations, $"{Sample} wrote no ER model to '{ErModelPath}'"];

					if (violations.Count == 0)
						return violations;
				}

				await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return [.. violations.Select(violation => $"{Sample}: {violation}")];
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_started)
		{
			try
			{
				await RunAsync(
					"aspire",
					["stop", "--non-interactive", "--nologo"],
					StartTimeout,
					CancellationToken.None
				);
			}
			catch (InvalidOperationException)
			{
				// Already stopped, or never started; nothing to clean up.
			}
		}

		try
		{
			Directory.Delete(_outputDirectory, recursive: true);
		}
		catch (IOException)
		{
			// The LikeC4 container can hold the folder briefly after the AppHost stops; it is temporary.
		}
		catch (UnauthorizedAccessException)
		{
			// As above.
		}
	}

	async Task RunAsync(
		string tool,
		IReadOnlyList<string> arguments,
		TimeSpan timeout,
		CancellationToken cancellationToken
	)
	{
		ProcessStartInfo startInfo = new(ToolLocator.Resolve(tool) ?? tool)
		{
			WorkingDirectory = _directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var argument in arguments)
			startInfo.ArgumentList.Add(argument);

		startInfo.Environment["AspireC4__OutputDirectory"] = _outputDirectory;
		startInfo.Environment["AspireC4__ExportImages"] = "false";
		startInfo.Environment["AspireC4__EntityFramework__ExportImages"] = "false";
		startInfo.Environment["AspireC4__EntityFramework__OutputDirectory"] = ErOutputDirectory;
		startInfo.Environment["ASPIRE_CLI_TELEMETRY_OPTOUT"] = "true";

		using Process process = new() { StartInfo = startInfo };
		StringBuilder output = new();
		process.OutputDataReceived += (_, e) => Append(output, e.Data);
		process.ErrorDataReceived += (_, e) => Append(output, e.Data);

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(timeout);

		try
		{
			await process.WaitForExitAsync(timeoutSource.Token);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			throw new InvalidOperationException(
				$"'{tool} {string.Join(' ', arguments)}' in {Sample} did not finish within {timeout.TotalMinutes:0} minutes.{Environment.NewLine}{Tail(output)}"
			);
		}

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"'{tool} {string.Join(' ', arguments)}' in {Sample} exited with {process.ExitCode}.{Environment.NewLine}{Tail(output)}"
			);
		}
	}

	static void Append(StringBuilder output, string? line)
	{
		if (line is null)
			return;

		lock (output)
			output.AppendLine(line);
	}

	static string Tail(StringBuilder output)
	{
		lock (output)
			return string.Join(Environment.NewLine, output.ToString().Split(Environment.NewLine).TakeLast(40));
	}

	static async Task<string> ReadSharedAsync(string path, CancellationToken cancellationToken)
	{
		// The model is rewritten in place while the AppHost runs; read it without blocking the writer.
		await using FileStream stream = new(
			path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete
		);
		using StreamReader reader = new(stream);
		return await reader.ReadToEndAsync(cancellationToken);
	}

	static string FindRepositoryRoot()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			if (
				File.Exists(Path.Combine(directory.FullName, "global.json"))
				&& Directory.Exists(Path.Combine(directory.FullName, "samples"))
			)
				return directory.FullName;
		}

		throw new DirectoryNotFoundException(
			$"No repository root (global.json beside samples/) above '{AppContext.BaseDirectory}'."
		);
	}
}
