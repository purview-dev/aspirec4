using System.ComponentModel;
using System.Globalization;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // Namespace matches Aspire's own, so the API is ambient in AppHosts.
namespace Aspire.Hosting;

#pragma warning restore IDE0130

/// <summary>
/// AppHost extension methods for the AspireC4 Entity Framework Core ER diagram.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4EfResourceBuilderExtensions
{
	const string DefaultResourceName = "aspirec4-er";
	const string ServerResourceSuffix = "-server";

	/// <summary>
	/// Adds a live d2 ER diagram generated from the Entity Framework Core models of the projects opted
	/// in with <see cref="WithEntityFrameworkModel{T}(IResourceBuilder{T}, string?)"/>.
	/// </summary>
	/// <param name="builder">The AspireC4 resource builder.</param>
	/// <param name="port">Host port for the diagram. <see langword="null"/> allocates dynamically.</param>
	/// <param name="configure">Optional options callback.</param>
	[AspireExport(RunSyncOnBackgroundThread = true)]
	public static IResourceBuilder<AspireC4Resource> WithEntityFrameworkDiagram(
		this IResourceBuilder<AspireC4Resource> builder,
		int? port = null,
		Action<AspireC4EfDiagramOptions>? configure = null
	)
	{
		ArgumentNullException.ThrowIfNull(builder);

		AspireC4EfDiagramOptions snapshot = new();
		configure?.Invoke(snapshot);

		// Registered through the options framework rather than Options.Create so later Configure
		// callbacks are not suppressed — the same constraint the LikeC4 side documents.
		var services = builder.ApplicationBuilder.Services;

		services
			.AddOptions<AspireC4EfDiagramOptions>()
			.Configure(options => configure?.Invoke(options))
			.BindConfiguration(AspireC4EfDiagramOptions.SectionName);

		services.AddEventingSubscriber<AspireC4EfLifecycleHook>();

		var outputDirectory = ResolveOutputDirectory(
			builder.ApplicationBuilder.AppHostDirectory,
			snapshot.OutputDirectory
		);
		Directory.CreateDirectory(outputDirectory);

		// Only the d2 renderer has a live server to run. The other renderers write files the browser
		// opens directly, so adding a sidecar for them would be a long-running resource doing nothing.
		var inner =
			snapshot.Renderer == ErdRenderer.D2
				? CreateServerResource(builder.ApplicationBuilder, snapshot, outputDirectory, port).Resource
				: null;

		builder
			.ApplicationBuilder.AddResource(
				new AspireC4EfResource(DefaultResourceName)
				{
					OutputDirectory = outputDirectory,
					AppHostDirectory = builder.ApplicationBuilder.AppHostDirectory,
					InnerResource = inner,
				}
			)
			.ExcludeFromLikeC4()
			.ExcludeFromManifest()
			// Without an initial state the dashboard shows the resource as Unknown forever: it runs no
			// process of its own, so nothing else would ever publish one. The lifecycle hook moves it
			// to Running once the diagram has been written.
			.WithInitialState(
				new CustomResourceSnapshot
				{
					ResourceType = "ERDiagram",
					IsHidden = false,
					State = KnownResourceStates.Starting,
					Properties = [],
				}
			);

		return builder;
	}

	/// <summary>Includes this project's Entity Framework Core model in the ER diagram.</summary>
	/// <param name="builder">The project resource builder.</param>
	/// <param name="label">Optional label for this project's group in the diagram.</param>
	[AspireExport]
	public static IResourceBuilder<T> WithEntityFrameworkModel<T>(
		this IResourceBuilder<T> builder,
		string? label = null
	)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithAnnotation(
			new AspireC4EfProjectAnnotation { Label = label },
			ResourceAnnotationMutationBehavior.Replace
		);
	}

	/// <summary>
	/// Switches the ER diagram sidecar from the default container to a locally installed <c>d2</c>
	/// binary.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Unlike the LikeC4 sidecar there is no JavaScript-runtime fallback: d2 ships as a Go binary, and
	/// the <c>@d2lang/d2</c> npm package is a WASM library with no executable. <c>d2</c> must be on
	/// <c>PATH</c> — installed via scoop, Homebrew, <c>install.sh</c> or <c>go install</c>.
	/// </para>
	/// <para>
	/// Call this <b>after</b> <see cref="WithEntityFrameworkDiagram"/>, which creates the container this
	/// replaces.
	/// </para>
	/// </remarks>
	/// <param name="builder">The AspireC4 resource builder.</param>
	/// <param name="command">The executable to run. Defaults to <c>d2</c> resolved from <c>PATH</c>.</param>
	[AspireExport(MethodName = "withLocalD2CLI")]
	public static IResourceBuilder<AspireC4Resource> WithLocalD2CLI(
		this IResourceBuilder<AspireC4Resource> builder,
		string? command = null
	)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var diagram =
			builder.ApplicationBuilder.Resources.OfType<AspireC4EfResource>().FirstOrDefault()
			?? throw new InvalidOperationException(
				$"No ER diagram resource was found. Call {nameof(WithEntityFrameworkDiagram)}() before {nameof(WithLocalD2CLI)}()."
			);

		if (diagram.InnerResource is null)
		{
			throw new InvalidOperationException(
				$"{nameof(WithLocalD2CLI)}() only applies to the {nameof(ErdRenderer.D2)} renderer. "
					+ $"Set {nameof(AspireC4EfDiagramOptions.Renderer)} to {nameof(ErdRenderer.D2)} in "
					+ $"{nameof(WithEntityFrameworkDiagram)}()."
			);
		}

		builder.ApplicationBuilder.Resources.Remove(diagram.InnerResource);

		// The container variant registers no health check, so unlike the LikeC4 sidecar there is no
		// stale service-level registration to clean up here.
		D2LocalServerResource local = new(
			DefaultResourceName + ServerResourceSuffix,
			command ?? D2LocalServerResource.DefaultCommand,
			diagram.OutputDirectory
		);

		builder
			.ApplicationBuilder.AddResource(local)
			.WithArgs(context =>
			{
				var options = context
					.ExecutionContext.Services.GetRequiredService<IOptions<AspireC4EfDiagramOptions>>()
					.Value;

				context.Args.Add("--watch");
				context.Args.Add("--host");
				context.Args.Add("0.0.0.0");
				context.Args.Add("--port");
				context.Args.Add(D2ServerResource.DefaultPort.ToString(CultureInfo.InvariantCulture));
				context.Args.Add("--browser");
				context.Args.Add("0");
				context.Args.Add("--layout");
				context.Args.Add(ToEngineName(options.LayoutEngine));
				context.Args.Add(Path.Combine(diagram.OutputDirectory, $"{options.FileName}.d2"));

				// d2 writes the compiled SVG on every pass; keeping it out of the generated-diagram
				// directory stops it appearing as untracked build output in the consumer's repository.
				context.Args.Add(Path.Combine(Path.GetTempPath(), $"{options.FileName}.svg"));

				return Task.CompletedTask;
			})
			.WithHttpEndpoint(targetPort: D2ServerResource.DefaultPort, name: AspireC4EfResource.HttpEndpointName)
			.WithUrlForEndpoint(
				AspireC4EfResource.HttpEndpointName,
				url =>
				{
					url.DisplayText = "View ER Diagram";
					url.DisplayLocation = UrlDisplayLocation.SummaryAndDetails;
				}
			)
			.ExcludeFromLikeC4()
			.ExcludeFromManifest();

		diagram.InnerResource = local;

		return builder;
	}

	static IResourceBuilder<D2ServerResource> CreateServerResource(
		IDistributedApplicationBuilder builder,
		AspireC4EfDiagramOptions options,
		string outputDirectory,
		int? port
	)
	{
		var file = $"{D2ServerResource.WorkspacePath}/{options.FileName}.d2";

		return builder
			.AddResource(new D2ServerResource(DefaultResourceName + ServerResourceSuffix))
			.WithImage(D2ServerResource.DefaultImage)
			.WithImageTag(options.ContainerImageTag ?? D2ServerResource.DefaultTag)
			.WithImageRegistry(D2ServerResource.DefaultRegistry)
			.WithBindMount(outputDirectory, D2ServerResource.WorkspacePath, isReadOnly: true)
			.WithHttpEndpoint(
				port: port,
				targetPort: D2ServerResource.DefaultPort,
				name: AspireC4EfResource.HttpEndpointName
			)
			.WithUrlForEndpoint(
				AspireC4EfResource.HttpEndpointName,
				url =>
				{
					url.DisplayText = "View ER Diagram";
					url.DisplayLocation = UrlDisplayLocation.SummaryAndDetails;
				}
			)
			.WithArgs(
				"--watch",
				// Without an explicit host d2 binds to localhost inside the container and the
				// published port never reaches it.
				"--host",
				"0.0.0.0",
				"--port",
				D2ServerResource.DefaultPort.ToString(CultureInfo.InvariantCulture),
				// d2 opens a browser by default, which has nothing to open in a container.
				"--browser",
				"0",
				// dagre, the default, cannot target individual sql_table rows.
				"--layout",
				ToEngineName(options.LayoutEngine),
				file,
				// d2 --watch writes the compiled SVG to disk on every compile, defaulting to a path
				// beside the input. The workspace is mounted read-only, so without an explicit
				// writable output path every compile fails with "read-only file system" — the server
				// still answers 200 but never renders anything. /tmp is writable by the image's
				// non-root user; a tmpfs mount is not, as it lands root-owned.
				$"/tmp/{options.FileName}.svg"
			)
			.ExcludeFromManifest();
	}

#pragma warning disable IDE0072 // Add missing cases
	static string ToEngineName(D2LayoutEngine engine) =>
		engine switch
		{
			D2LayoutEngine.Dagre => "dagre",
			D2LayoutEngine.Tala => "tala",
			_ => "elk",
		};
#pragma warning restore IDE0072 // Add missing cases

	static string ResolveOutputDirectory(string appHostDirectory, string outputDirectory) =>
		Path.IsPathRooted(outputDirectory)
			? outputDirectory
			: Path.GetFullPath(Path.Combine(appHostDirectory, outputDirectory));
}
