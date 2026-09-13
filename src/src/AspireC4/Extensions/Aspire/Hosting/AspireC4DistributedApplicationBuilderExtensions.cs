using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.Lifecycle;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IDistributedApplicationBuilder"/> to add LikeC4 live architecture diagram capabilities to an Aspire application.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4DistributedApplicationBuilderExtensions
{
	internal const string AspireC4ResourceName = "aspirec4";

	internal const string AspireC4ServerResourceSuffix = "-server";

	/// <summary>
	/// Adds a LikeC4 live architecture diagram to the Aspire application.
	/// </summary>
	/// <remarks>
	/// This registers a lifecycle hook that generates a <c>.c4</c> model file from the Aspire
	/// resource graph, and starts the official <c>ghcr.io/likec4/likec4</c> container as a
	/// sidecar that renders an interactive, hot-reloading diagram in the browser.
	/// <para>
	/// <b>Prerequisite:</b> Docker must be available (standard Aspire requirement). To use a
	/// local Node.js CLI instead, call <c>.WithLocalCli()</c> on the returned builder.
	/// </para>
	/// </remarks>
	/// <param name="builder">The Aspire distributed application builder.</param>
	/// <param name="name">Optional name of the LikeC4 visualization resource (used for the server container and diagram file).</param>
	/// <param name="port">Optional host port to bind the LikeC4 server's HTTP endpoint to. By default, no fixed host port is used and Docker assigns a dynamic port.</param>
	/// <param name="configure">Optional callback to configure <see cref="AspireC4DiagramOptions"/>.</param>
	/// <returns>An <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(RunSyncOnBackgroundThread = true)]
	public static IResourceBuilder<AspireC4Resource> AddAspireC4(
		[NotNull] this IDistributedApplicationBuilder builder,
		[ResourceName] string? name = null,
		int? port = null,
		Action<AspireC4DiagramOptions>? configure = null
	)
	{
		AspireC4DiagramOptions options = new();
		configure?.Invoke(options);

		if (string.IsNullOrWhiteSpace(name))
			name = AspireC4ResourceName;

		// Seed the DI-registered options from the builder-time snapshot so that all
		// properties set via the configure callback (or a pre-built options object) are
		// reflected when IOptions<T> is resolved at runtime. Register via the standard
		// Options framework rather than Options.Create so that:
		//   - Extension-method Configure<T> callbacks (WithAdditionalDSLFolder, WithImageAliasFolder,
		//     WithHideFromDashboard, etc.) are applied on top of the snapshot.
		//   - BindConfiguration allows appsettings / environment-variable overrides.
		// Options.Create would register a concrete singleton wrapper as IOptions<T>, causing
		// TryAddSingleton for OptionsManager<T> to be skipped and all Configure<T> lambdas
		// to be silently ignored.
		builder
			.Services.AddOptions<AspireC4DiagramOptions>()
			.Configure(options => configure?.Invoke(options))
			.BindConfiguration(AspireC4DiagramOptions.SectionName);

		var outputDir = ResolveOutputDirectory(builder.AppHostDirectory, options.OutputDirectory);
		Directory.CreateDirectory(outputDir);

		var imageTag = options.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;
		var resolvedHmrPort = options.HMRPort ?? AspireC4Resource.DefaultHMRPort;
		var defaultViewId = string.IsNullOrWhiteSpace(options.DefaultViewId) ? null : options.DefaultViewId;

		// Always use the same port on both the host and inside the container for HMR.
		// In LikeC4 v1.57 or higher, --hmr-port sets server.hmr.port — the port Vite BINDS to inside
		// the container. Vite also advertises this same port to browsers as the HMR WebSocket
		// target (no separate clientPort option exists). Docker must therefore map the SAME port
		// on the host so the browser's connection to host:PORT reaches container:PORT correctly.
		// Dynamic (null) host ports cannot work here: if Docker maps host:DYNAMIC → container:24678
		// but Vite is told --hmr-port DYNAMIC it binds to container:DYNAMIC, which Docker doesn't
		// forward, breaking the HMR WebSocket connection entirely.
		int? hmrHostPort = options.HMRPort ?? resolvedHmrPort;

		builder
			.Services.AddOptions<ContainerWorkspaceOptions>()
			.Configure(runtime =>
			{
				runtime.ImageTag = imageTag;
				runtime.ResolvedHMRPort = resolvedHmrPort;
			});

		builder.Services.AddEventingSubscriber<AspireC4LifecycleHook>();
		builder.Services.AddAspireC4LifecycleHookTelemetry();

		LikeC4ServerResource serverResource = new(name + AspireC4ServerResourceSuffix);
		builder.Eventing.Subscribe<BeforeStartEvent>(
			(_, _) =>
			{
				var otlpExporterAnnotations = serverResource.Annotations.OfType<OtlpExporterAnnotation>().ToArray();
				foreach (var annotation in otlpExporterAnnotations)
					serverResource.Annotations.Remove(annotation);

				return Task.CompletedTask;
			}
		);
		var serverBuilder = CreateLikeC4ServerResource(
			builder,
			name,
			port,
			options,
			imageTag,
			defaultViewId,
			serverResource
		);

		//if (!options.IncludeAspireC4InternalResource)
		//{
		//	// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
		//	// Set a stable DSL identifier equal to the base name so that the element, when explicitly
		//	// included by a consumer (e.g. via ConfigureTestHost), is always emitted as "aspirec4"
		//	// regardless of the "-server" suffix on the Aspire resource name.
		//	serverBuilder.ExcludeFromLikeC4();
		//}

		if (!options.DisableHMR)
			EnableHotModuleReloading(resolvedHmrPort, hmrHostPort, serverBuilder);

		AspireC4Resource aspirec4Resource = new(name, outputDir) { InnerResource = serverResource };

		return builder
			.AddResource(aspirec4Resource)
			.ExcludeFromLikeC4()
			.ExcludeFromManifest()
			.WithInitialState(
				new CustomResourceSnapshot
				{
					// Shown as a container type since it IS backed by a container (or local CLI).
					// URLs, state, and properties are forwarded from the inner resource at runtime
					// by ForwardInnerResourceStateAsync so this entry stays accurate.
					ResourceType = "Container",
					IsHidden = false,
					Properties = [],
				}
			);
	}

	static IResourceBuilder<LikeC4ServerResource> CreateLikeC4ServerResource(
		IDistributedApplicationBuilder builder,
		string name,
		int? port,
		AspireC4DiagramOptions options,
		string imageTag,
		string? defaultViewId,
		LikeC4ServerResource serverResource
	)
	{
		var serverBuilder = builder
			.AddResource(serverResource)
			.WithImage(LikeC4ServerResource.DefaultImage)
			.WithImageTag(imageTag)
			.WithImageRegistry(LikeC4ServerResource.DefaultRegistry)
			.WithImagePullPolicy(ImagePullPolicy.Always)
			.WithHttpEndpoint(
				port: port,
				targetPort: AspireC4Resource.DefaultPort,
				name: AspireC4Resource.HttpEndpointName
			)
			.WithUrlForEndpoint(
				AspireC4Resource.HttpEndpointName,
				opts =>
				{
					opts.DisplayText = "View LikeC4 Diagram";
					//opts.DisplayOrder = 0;
					opts.DisplayLocation = UrlDisplayLocation.SummaryAndDetails;
					opts.Url = string.IsNullOrWhiteSpace(defaultViewId) ? "/" : $"/view/{defaultViewId}";
				}
			)
			.WithHttpHealthCheck("/", statusCode: 200, endpointName: AspireC4Resource.HttpEndpointName)
			// Register container args as a callback so they are evaluated at container-start
			// time (after BeforeStartEvent has set ContainerServePath). DisableHMR is read
			// from AspireC4DiagramOptions so it respects configuration overrides at runtime.
			.WithArgs(async context =>
			{
				var wsOpts = context.ExecutionContext.Services.GetRequiredService<
					IOptions<ContainerWorkspaceOptions>
				>();
				var diagOpts = context.ExecutionContext.Services.GetRequiredService<IOptions<AspireC4DiagramOptions>>();

				context.Args.Add("start");
				context.Args.Add(wsOpts.Value.ContainerServePath);

				if (!string.IsNullOrWhiteSpace(options.Title))
				{
					context.Args.Add("--title");
					context.Args.Add($"\"{options.Title}\"");
				}

				var useDot =
					diagOpts.Value.UseDotIfAvailable && await Helpers.IsDotAvailableAsync(context.CancellationToken);
				if (useDot)
					context.Args.Add("--use-dot");

				context.Args.Add("--port");
				context.Args.Add(AspireC4Resource.DefaultPort);

				// Determine HMR mode based on the image tag at container-start time.
				var hmrMode = HMRPortCompatibility.Resolve(wsOpts.Value.ImageTag);
				if (!diagOpts.Value.DisableHMR && hmrMode == HMRPortMode.Configurable)
				{
					// Pass the container-internal HMR port. Because host and container use the
					// same port (symmetric mapping), this value is also what the browser connects to.
					context.Args.Add("--hmr-port");
					context.Args.Add(wsOpts.Value.ResolvedHMRPort);
				}

				if (diagOpts.Value.DisableHMR)
					context.Args.Add("--no-react-hmr");

				if (!diagOpts.Value.IncludeAspireC4InternalResource)
				{
					// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
					// Set a stable DSL identifier equal to the base name so that the element, when explicitly
					// included by a consumer (e.g. via ConfigureTestHost), is always emitted as "aspirec4"
					// regardless of the "-server" suffix on the Aspire resource name.
					context.Resource.Annotations.Add(new ExcludeFromLikeC4Annotation());
				}
			})
			.WithAnnotation(new LikeC4DSLIdAnnotation(name))
			.ExcludeFromManifest();
		return serverBuilder;
	}

	static void EnableHotModuleReloading(
		int resolvedHmrPort,
		int? hmrHostPort,
		IResourceBuilder<LikeC4ServerResource> serverBuilder
	)
	{
		serverBuilder
			.WithHttpEndpoint(port: hmrHostPort, targetPort: resolvedHmrPort, name: AspireC4Resource.HMREndpointName)
			.WithUrlForEndpoint(
				AspireC4Resource.HMREndpointName,
				opts =>
				{
					opts.DisplayText = "LikeC4 HMR Endpoint";
					//opts.DisplayOrder = 1;
					opts.DisplayLocation = UrlDisplayLocation.DetailsOnly;
				}
			);

		if (OperatingSystem.IsWindows())
		{
			serverBuilder
				// Required on Windows/Docker Desktop: inotify events do not propagate from the host
				// filesystem into the container, so chokidar must fall back to polling to detect
				// changes to the generated .c4 file.
				.WithEnvironment("CHOKIDAR_USEPOLLING", "1")
				.WithEnvironment("CHOKIDAR_INTERVAL", "200");
		}
	}

	static string ResolveOutputDirectory(string appHostDirectory, string outputDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

		return Path.GetFullPath(
			Path.IsPathRooted(outputDirectory) ? outputDirectory : Path.Combine(appHostDirectory, outputDirectory)
		);
	}
}
