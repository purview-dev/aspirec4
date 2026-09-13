using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IResourceBuilder{T}"/> of <see cref="AspireC4Resource"/>
/// that configure the LikeC4 visualization sidecar.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4ResourceExtensions
{
	/// <summary>
	/// Switches the LikeC4 server from the default Docker container to a local JavaScript
	/// package manager CLI (<c>npx</c>, <c>pnpm exec</c>, <c>yarn dlx</c>, or <c>bunx</c>).
	/// </summary>
	/// <remarks>
	/// Use this when Docker is not available or you prefer a local Node.js-based workflow.
	/// The selected runtime must be installed and accessible on the system PATH.
	/// </remarks>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="runtime">
	/// The CLI runtime to use. Defaults to <see cref="LocalCLIRuntime.Auto"/>,
	/// which detects the first available runtime in the order: npx → pnpm → yarn → bun.
	/// </param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(MethodName = "withLocalCLI")]
	public static IResourceBuilder<AspireC4Resource> WithLocalCLI(
		[NotNull] this IResourceBuilder<AspireC4Resource> builder,
		LocalCLIRuntime runtime = LocalCLIRuntime.Auto
	)
	{
		var aspirec4 = builder.Resource;

		// Remove the existing server resource (container by default) from the app model.
		if (aspirec4.InnerResource is not null)
			builder.ApplicationBuilder.Resources.Remove(aspirec4.InnerResource);

		// The container server's WithHttpHealthCheck already registered a service-level health check.
		// Removing the resource from the collection above does not un-register the DI service entry,
		// so we must clean it up here to prevent a duplicate-name error when the local resource adds
		// its own identically-named health check.
		var containerHcName =
			$"{aspirec4.Name}{AspireC4DistributedApplicationBuilderExtensions.AspireC4ServerResourceSuffix}_{AspireC4Resource.HttpEndpointName}_/_200_check";

		builder.ApplicationBuilder.Services.PostConfigure<HealthCheckServiceOptions>(opts =>
		{
			var stale = opts.Registrations.FirstOrDefault(r =>
				string.Equals(r.Name, containerHcName, StringComparison.OrdinalIgnoreCase)
			);
			if (stale is not null)
				opts.Registrations.Remove(stale);
		});

		var resolvedRuntime = runtime == LocalCLIRuntime.Auto ? AspireC4Builder.DetectRuntime() : runtime;

		// Build the base args without the HMR port — the async WithArgs callback below appends
		// --hmr-port at startup time once IOptions<AspireC4DiagramOptions> is resolvable.
		var (command, baseArgs) = AspireC4Builder.BuildLocalCLICommand(
			resolvedRuntime,
			aspirec4.OutputDirectory,
			AspireC4Resource.DefaultPort
		);

		// Use the system temp directory as the working directory for the serve process.
		// The output directory is passed as an absolute path argument, so the working
		// directory only affects how pnpm/yarn/deno resolve workspace roots. Using /tmp
		// (or the system temp dir on Windows) prevents package managers from walking up
		// and detecting the AppHost's parent package.json as a workspace root, which would
		// cause them to bypass the pre-warmed dlx cache and re-download on every start.
		LikeC4LocalServerResource localResource = new(
			aspirec4.Name + AspireC4DistributedApplicationBuilderExtensions.AspireC4ServerResourceSuffix,
			command,
			Path.GetTempPath()
		);

		AddLocalServerResource(builder, aspirec4, baseArgs, localResource);

		aspirec4.InnerResource = localResource;

		builder.ApplicationBuilder.Services.Configure<ContainerWorkspaceOptions>(wsOpts =>
			wsOpts.LocalCLIRuntime = resolvedRuntime
		);

		return builder;
	}

	static void AddLocalServerResource(
		IResourceBuilder<AspireC4Resource> builder,
		AspireC4Resource aspirec4,
		string[] baseArgs,
		LikeC4LocalServerResource localResource
	) =>
		builder
			.ApplicationBuilder.AddResource(localResource)
			.WithArgs(context =>
			{
				var diagOpts = context.ExecutionContext.Services.GetRequiredService<IOptions<AspireC4DiagramOptions>>();

				foreach (var arg in baseArgs)
					context.Args.Add(arg);

				if (!diagOpts.Value.DisableHMR)
				{
					var hmrPort = diagOpts.Value.HMRPort ?? AspireC4Resource.DefaultHMRPort;
					context.Args.Add("--hmr-port");
					context.Args.Add($"{hmrPort}");
				}

				if (!diagOpts.Value.IncludeAspireC4InternalResource)
				{
					// Exclude from the diagram and manifest. Set a stable DSL identifier so that,
					// if a consumer explicitly includes this resource (e.g. via ConfigureTestHost),
					// it is emitted as "aspirec4" — the same name as the Docker-container variant.
					// We don't have access to the extensions here, so needed to do this manually.
					context.Resource.Annotations.Add(new ExcludeFromLikeC4Annotation());
				}

				return Task.CompletedTask;
			})
			.WithHttpEndpoint(name: AspireC4Resource.HttpEndpointName, targetPort: AspireC4Resource.DefaultPort)
			.WithHttpEndpoint(name: AspireC4Resource.HMREndpointName, targetPort: AspireC4Resource.DefaultHMRPort)
			.WithHttpHealthCheck("/", statusCode: 200, endpointName: AspireC4Resource.HttpEndpointName)
			.WithAnnotation(new LikeC4DSLIdAnnotation(aspirec4.Name))
			.ExcludeFromManifest()
			.WithInitialState(
				new CustomResourceSnapshot
				{
					ResourceType = nameof(LikeC4LocalServerResource),
					IsHidden = false,
					Properties = [],
				}
			);

	/// <summary>
	/// Provides access to the underlying LikeC4 server resource builder for advanced configuration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this to apply annotations or other configuration directly to the inner server resource
	/// (e.g. <c>WithLikeC4Details</c>, custom annotations). The server resource is the Docker
	/// container (<see cref="LikeC4ServerResource"/>) unless <c>.WithLocalCLI()</c> has been called,
	/// in which case it is the CLI executable (<see cref="LikeC4LocalServerResource"/>).
	/// </para>
	/// <para>
	/// This must be called <b>after</b> any <c>.WithLocalCLI()</c> call if you want to configure the
	/// CLI resource; calling <c>ConfigureServer</c> first and then <c>WithLocalCLI</c> will configure
	/// the Docker container that is subsequently replaced.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="AspireC4Resource"/> builder.</param>
	/// <param name="configure">A callback that receives the inner server resource builder.</param>
	/// <returns>The same <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(RunSyncOnBackgroundThread = true)]
	public static IResourceBuilder<AspireC4Resource> ConfigureServer(
		[NotNull] this IResourceBuilder<AspireC4Resource> builder,
		Action<IResourceBuilder<IResource>> configure
	)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var innerResource =
			builder.Resource.InnerResource
			?? throw new InvalidOperationException(
				$"The inner server resource has not been initialised yet. Ensure {nameof(AspireC4DistributedApplicationBuilderExtensions.AddAspireC4)}() has completed before calling {nameof(ConfigureServer)}()."
			);

		var innerBuilder = builder.ApplicationBuilder.CreateResourceBuilder(innerResource);
		configure(innerBuilder);

		return builder;
	}
}
