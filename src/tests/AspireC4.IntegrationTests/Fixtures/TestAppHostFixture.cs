using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting.AspireC4.Fixtures;

/// <summary>
/// Shared fixture that owns the lifecycle of the <c>TestAppHost</c> Aspire application used by the
/// integration tests. The application is started once per test session and torn down automatically.
/// </summary>
/// <remarks>
/// <para>
/// Test-specific <see cref="AspireC4DiagramOptions"/> are injected through <see cref="ConfigureBuilder"/>
/// so the generated model, config file and image aliases land in a dedicated output directory alongside
/// the test host output. That keeps every relevant path (extensions, image aliases, output) on the same
/// drive and under a common ancestor, which the single-bind-mount architecture requires.
/// </para>
/// <para>
/// When no container runtime is available the fixture records a <see cref="SkipReason"/> instead of
/// starting the app, so consuming tests can report <c>Skipped</c> rather than failing.
/// </para>
/// </remarks>
public sealed class TestAppHostFixture : TUnit.Aspire.AspireFixture<Projects.TestAppHost>
{
	readonly List<DistributedApplication> _publishApps = [];
	readonly List<string> _publishOutputDirs = [];

	public TestAppHostFixture()
	{
		OutputDirectory = Path.Combine(AppContext.BaseDirectory, "test-output-" + Guid.NewGuid().ToString("N")[..8]);
	}

	/// <summary>Directory where the generated <c>.c4</c> file and LikeC4 config are written.</summary>
	public string OutputDirectory { get; }

	/// <summary>Path of the generated LikeC4 model file (<c>model.gen.c4</c>).</summary>
	public string ModelPath => Path.Combine(OutputDirectory, "model.gen.c4");

	/// <summary>Path of the generated LikeC4 config file (<c>likec4.config.json</c>).</summary>
	public string ConfigPath => Path.Combine(OutputDirectory, "likec4.config.json");

	/// <summary>
	/// Where the ER diagram was written, read back off the resource rather than configured, so it
	/// always matches what the viewer server is serving.
	/// </summary>
	public string ErOutputDirectory =>
		(GetResource("aspirec4-er") as EntityFrameworkCore.ApplicationModel.AspireC4EfResource)?.OutputDirectory
		?? OutputDirectory;

	/// <summary>Path of the generated ER model. The default renderer emits JSON.</summary>
	public string ErDiagramPath => Path.Combine(ErOutputDirectory, "schema.gen.json");

	/// <summary>Path of the interactive viewer emitted beside the model.</summary>
	public string ErViewerPath => Path.Combine(ErOutputDirectory, "index.html");

	DistributedApplication? _app;

	/// <summary>
	/// Reads the public URLs a resource has published, waiting until it has at least one.
	/// </summary>
	public async Task<IReadOnlyList<string>> GetResourceUrlsAsync(
		string resourceName,
		CancellationToken cancellationToken
	)
	{
		if (_app is null)
			return [];

		var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(ResourceTimeout);

		try
		{
			await foreach (var evt in notifications.WatchAsync(cts.Token))
			{
				if (!evt.Resource.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
					continue;

				var urls = evt.Snapshot.Urls.Where(u => !u.IsInternal).Select(u => u.Url).ToArray();

				if (urls.Length > 0)
					return urls;
			}
		}
		catch (OperationCanceledException)
		{
			// Fall through: the resource never published a URL.
		}

		return [];
	}

	/// <summary>Finds a resource in the running application model, so tests can inspect annotations.</summary>
	public IResource? GetResource(string resourceName) =>
		_app
			?.Services.GetRequiredService<DistributedApplicationModel>()
			.Resources.FirstOrDefault(r => r.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Reads a resource's current dashboard state, so tests can assert what the dashboard would show.
	/// </summary>
	/// <returns>The state text, or <see langword="null"/> if the resource never reported one.</returns>
	public async Task<string?> GetResourceStateAsync(string resourceName, CancellationToken cancellationToken)
	{
		if (_app is null)
			return null;

		var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(TimeSpan.FromSeconds(30));

		try
		{
			await foreach (var evt in notifications.WatchAsync(cts.Token))
			{
				if (
					evt.Resource.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase)
					&& evt.Snapshot.State?.Text is { } state
				)
				{
					return state;
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Fall through: the resource never reported a state.
		}

		return null;
	}

	/// <summary>
	/// Waits for a resource to be hidden from the dashboard.
	/// </summary>
	/// <remarks>
	/// Hiding is applied by a lifecycle hook watching notifications, not at build time, so this has to
	/// wait rather than read one snapshot: the resource is briefly visible before the hook reacts.
	/// </remarks>
	/// <returns>
	/// <see langword="true"/> once the resource reports a hidden snapshot, <see langword="false"/> if it
	/// never does.
	/// </returns>
	public async Task<bool> WaitForHiddenAsync(string resourceName, CancellationToken cancellationToken)
	{
		if (_app is null)
			return false;

		var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(TimeSpan.FromSeconds(60));

		try
		{
			await foreach (var evt in notifications.WatchAsync(cts.Token))
			{
				if (evt.Resource.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase) && evt.Snapshot.IsHidden)
					return true;
			}
		}
		catch (OperationCanceledException)
		{
			// Fall through: the resource stayed visible.
		}

		return false;
	}

	/// <summary>
	/// Reads the parent the dashboard would nest a resource under.
	/// </summary>
	/// <remarks>
	/// Read from the published snapshot rather than from the resource's annotations: the dashboard
	/// renders the snapshot, and a later update that replaces the property collection wholesale drops
	/// the parent without touching the annotation the builder added.
	/// </remarks>
	/// <returns>The parent resource name, or <see langword="null"/> when the snapshot carries none.</returns>
	public async Task<string?> GetDashboardParentAsync(string resourceName, CancellationToken cancellationToken)
	{
		// Aspire's own KnownProperties.Resource.ParentName, which is internal to Aspire.Hosting.
		const string parentNameProperty = "resource.parentName";

		if (_app is null)
			return null;

		var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(TimeSpan.FromSeconds(30));

		string? parent = null;

		try
		{
			await foreach (var evt in notifications.WatchAsync(cts.Token))
			{
				if (!evt.Resource.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
					continue;

				parent =
					evt.Snapshot.Properties.FirstOrDefault(p =>
						p.Name.Equals(parentNameProperty, StringComparison.Ordinal)
					)?.Value as string;

				// The resource publishes more than once — initial state, then whatever the lifecycle
				// hook forwards onto it — and it is the last word that the dashboard draws.
				if (evt.Snapshot.State?.Text == KnownResourceStates.Running)
					return parent;
			}
		}
		catch (OperationCanceledException)
		{
			// Fall through: the resource never reached Running.
		}

		return parent;
	}

	/// <summary>Directory containing the copied image aliases (svg/png assets).</summary>
	public string ImagesDirectory => Path.Combine(AppContext.BaseDirectory, "likec4", "images");

	/// <summary>
	/// Set when the container runtime is unavailable; consuming tests should skip when non-null.
	/// </summary>
	public string? SkipReason { get; private set; }

	/// <summary>The result of a publish-mode run.</summary>
	public sealed record PublishResult(
		DistributedApplication App,
		string OutputDirectory,
		string ModelPath,
		string ManifestPath,
		bool IsPublishMode
	);

	protected override bool EnableTelemetryCollection => false;

	/// <summary>
	/// Waits for the LikeC4 server to be running. The default <c>AllHealthy</c> wait is unsuitable for
	/// this AppHost: the auto-generated <c>node-app-installer</c> resource completes into a terminal
	/// <c>Finished</c> state (which Aspire's fail-fast treats as a startup failure), and the outer
	/// <c>aspirec4</c> resource never receives a forwarded health status (only state and URLs are
	/// forwarded from the inner server). Waiting for the outer <c>aspirec4</c> resource to reach
	/// <c>Running</c> mirrors the historical integration-test behavior.
	/// </summary>
	protected override async Task WaitForResourcesAsync(DistributedApplication app, CancellationToken cancellationToken)
	{
		_app = app;

		// TestAppHost registers the ER diagram too, so the architecture diagram is nested under the
		// shared parent row and carries the grouping suffix.
		const string aspireC4ResourceName =
			AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
			+ ApplicationModel.AspireC4DiagramGrouping.LikeC4ResourceSuffix;

		var notificationService = app.Services.GetRequiredService<ResourceNotificationService>();

		string? lastObservedState = null;
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(ResourceTimeout);

		try
		{
			await foreach (var evt in notificationService.WatchAsync(cts.Token))
			{
				if (!evt.Resource.Name.Equals(aspireC4ResourceName, StringComparison.OrdinalIgnoreCase))
					continue;

				lastObservedState = evt.Snapshot.State?.Text;

				if (lastObservedState == KnownResourceStates.Running)
					return;

				if (
					lastObservedState == KnownResourceStates.FailedToStart
					|| lastObservedState == KnownResourceStates.RuntimeUnhealthy
					|| lastObservedState == KnownResourceStates.Exited
				)
				{
					throw new InvalidOperationException(
						$"LikeC4 container reached terminal state '{lastObservedState}' instead of Running."
					);
				}
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException(
				$"LikeC4 container did not reach Running within {ResourceTimeout.TotalSeconds}s. "
					+ $"Last observed state: '{lastObservedState ?? "(none)"}'"
			);
		}
	}

	public override async Task InitializeAsync()
	{
		if (!await IsContainerRuntimeAvailableAsync(RunCancellationToken))
		{
			var runtime = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") ?? "docker";
			SkipReason = $"Container runtime '{runtime}' is not available on this machine.";
			return;
		}

		await base.InitializeAsync();
	}

	/// <summary>
	/// Configures the shared application before it is built: routes AspireC4 output into
	/// <see cref="OutputDirectory"/> and disables HMR and formatter invocation during tests.
	/// </summary>
	protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
	{
		var configOptions = OptionsNameHelper
			.CreateOptionsBuilder<AspireC4DiagramOptions>()
			.WithConfigurationSeperator()
			.WithProperty(opts => opts.OutputDirectory, OutputDirectory)
			.WithProperty(opts => opts.FileName, "model.gen")
			.WithProperty(opts => opts.Title, "Integration Test Architecture")
			.WithProperty(opts => opts.DisableHMR, true)
			.Build();

		builder.Configuration.AddInMemoryCollection(configOptions);

		// The ER diagram's output directory is deliberately not overridden. Its viewer server binds a
		// mount when the builder runs, which cannot follow a configuration change applied afterwards —
		// overriding it here would leave the server serving a directory the diagram is not written to.
		// Tests read the directory back off the resource instead.

		// PostConfigure wins over all Configure callbacks, including the default FormatGeneratedFile=true.
		// The format step invokes `npx likec4 …` which traverses up the directory tree and scans the entire
		// repository workspace when run from within the repo — hanging the BeforeStartEvent handler.
		builder.Services.PostConfigure<AspireC4DiagramOptions>(static opts => opts.FormatGeneratedFile = false);
	}

	/// <summary>
	/// Runs the AppHost in publish mode in-process (equivalent to <c>dotnet run -- publish</c>), which
	/// generates the <c>.c4</c> model and the Aspire manifest without starting the live LikeC4 server.
	/// </summary>
	public async Task<PublishResult> PublishAsync(CancellationToken cancellationToken)
	{
		var outputDir = Path.Combine(AppContext.BaseDirectory, "publish-output-" + Guid.NewGuid().ToString("N")[..8]);
		var modelOutputDir = Path.Combine(outputDir, "likec4");
		Directory.CreateDirectory(modelOutputDir);

		var configOptions = OptionsNameHelper
			.CreateOptionsBuilder<AspireC4DiagramOptions>()
			.WithConfigurationSeperator()
			.WithProperty(opts => opts.OutputDirectory, modelOutputDir)
			.WithProperty(opts => opts.FileName, "publish-model")
			.WithProperty(opts => opts.Title, "Publish Mode Test")
			.Build();

		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TestAppHost>(
			["publish", "--publisher", "manifest", "--output-path", outputDir],
			static (_, _) => { },
			cancellationToken
		);

		builder.Configuration.AddInMemoryCollection(configOptions);
		builder.Services.PostConfigure<AspireC4DiagramOptions>(static opts => opts.FormatGeneratedFile = false);

		var app = await builder.BuildAsync(cancellationToken);
		_publishApps.Add(app);

		var isPublishMode = app.Services.GetRequiredService<DistributedApplicationExecutionContext>().IsPublishMode;

		// BuildAsync returns as soon as the application is built; the publish pipeline
		// (BeforeStartEvent → .c4 generation, then the manifest publisher) runs asynchronously
		// on the AppHost thread. ApplicationStopped fires once the publish has fully completed
		// and every output file has been flushed — only then are the files safe to read.
		TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
		var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
		using var registration = lifetime.ApplicationStopped.Register(() => stopped.TrySetResult());
		await stopped.Task.WaitAsync(cancellationToken);

		_publishOutputDirs.Add(outputDir);

		var modelPath = Path.Combine(modelOutputDir, "publish-model.c4");
		var manifestPath = Path.Combine(outputDir, "aspire-manifest.json");

		return new PublishResult(app, outputDir, modelPath, manifestPath, isPublishMode);
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();

		foreach (var app in _publishApps)
		{
			await app.DisposeAsync();
		}
		_publishApps.Clear();

		foreach (var dir in _publishOutputDirs.Append(OutputDirectory))
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, recursive: true);
			}
		}
		_publishOutputDirs.Clear();
	}

	static async Task<bool> IsContainerRuntimeAvailableAsync(CancellationToken cancellationToken)
	{
		var runtime = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") ?? "docker";
		try
		{
			using var proc = Process.Start(
				new ProcessStartInfo
				{
					FileName = runtime,
					Arguments = "info",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);
			if (proc is null)
				return false;

			await proc.WaitForExitAsync(cancellationToken);
			return proc.ExitCode == 0;
		}
		// Intentionally swallow all exceptions — this method probes availability.
#pragma warning disable CA1031
		catch
		{
			return false;
		}
#pragma warning restore CA1031
	}
}
