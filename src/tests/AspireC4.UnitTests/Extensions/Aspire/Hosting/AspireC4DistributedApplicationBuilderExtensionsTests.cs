using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting;

public sealed class AspireC4DistributedApplicationBuilderExtensionsTests
{
	[Test]
	public async Task AddAspireC4_ExposesHttpAndHmrEndpoints()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		// "latest" tag resolves to a recent version (>= 1.57) at startup and uses --hmr-port.
		// Host and container use the same port so the browser's HMR WebSocket connection works.

		// Act
		var visualization = appBuilder.AddAspireC4();
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var endpoints = serverResource
			.Annotations.OfType<EndpointAnnotation>()
			.OrderBy(endpoint => endpoint.Name, StringComparer.Ordinal)
			.ToArray();

		// Assert
		await Assert.That(endpoints).Count().IsEqualTo(2);
		await Assert.That(endpoints[0].Name).IsEqualTo(AspireC4Resource.HttpEndpointName);
		await Assert.That(endpoints[0].TargetPort).IsEqualTo(AspireC4Resource.DefaultPort);
		await Assert.That(endpoints[1].Name).IsEqualTo(AspireC4Resource.HMREndpointName);
		await Assert.That(endpoints[1].TargetPort).IsEqualTo(AspireC4Resource.DefaultHMRPort);
		await Assert.That(endpoints[1].Port).IsEqualTo(AspireC4Resource.DefaultHMRPort);
	}

	[Test]
	public async Task AddAspireC4_HmrEndpoint_UsesSymmetricPortForConfigurableVersions()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "100.57.0");
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var hmrEndpoint = serverResource
			.Annotations.OfType<EndpointAnnotation>()
			.Single(e => e.Name == AspireC4Resource.HMREndpointName);

		// Assert — host and container use the same port so the browser's HMR WebSocket connects
		await Assert.That(hmrEndpoint.Port).IsEqualTo(AspireC4Resource.DefaultHMRPort);
	}

	[Test]
	public async Task AddAspireC4_HmrEndpoint_UsesFixedPortForLegacyPinnedVersion()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var hmrEndpoint = serverResource
			.Annotations.OfType<EndpointAnnotation>()
			.Single(e => e.Name == AspireC4Resource.HMREndpointName);

		// Assert — must be fixed so the browser-side Vite JS (hardcoded port 24678) connects correctly
		await Assert.That(hmrEndpoint.Port).IsEqualTo(AspireC4Resource.DefaultHMRPort);
	}

	[Test]
	public async Task AddAspireC4_StoresImageTagForHmrPortModeResolution()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions = provider.GetRequiredService<IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.ImageTag).IsEqualTo("1.55.0");
	}

	[Test]
	public async Task AddAspireC4_StoresImageTagForConfigurableVersion()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "100.57.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions = provider.GetRequiredService<IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.ImageTag).IsEqualTo("100.57.0");
	}

	[Test]
	public async Task AddAspireC4_HasNoContainerMountAnnotationsAtConfigureTime()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4();
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var mounts = serverResource.Annotations.OfType<ContainerMountAnnotation>().ToArray();

		// Assert
		await Assert.That(mounts).IsEmpty();
	}

	[Test]
	public async Task AddAspireC4_CreatesConfiguredOutputDirectory()
	{
		// Arrange
		var outputDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);

		try
		{
			var appBuilder = CreateAppBuilder();

			// Act
			appBuilder.AddAspireC4(configure: opts => opts.OutputDirectory = outputDir);

			// Assert
			await Assert.That(Directory.Exists(outputDir)).IsTrue();
		}
		finally
		{
			if (Directory.Exists(outputDir))
			{
				Directory.Delete(outputDir, recursive: true);
			}
		}
	}

	[Test]
	public async Task AddAspireC4_ContainerArgs_IncludesHMRPortForConfigurableMode(CancellationToken cancellationToken)
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "100.57.0");
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateContainerArgsAsync(appBuilder, serverResource, cancellationToken);

		// Assert
		var hmrIdx = args.IndexOf("--hmr-port");
		await Assert.That(hmrIdx).IsGreaterThan(-1);
		await Assert.That(args[hmrIdx + 1]).IsEqualTo($"{AspireC4Resource.DefaultHMRPort}");
	}

	[Test]
	public async Task AddAspireC4_ContainerArgs_ExcludesHMRPortForFixedPortMode(CancellationToken cancellationToken)
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateContainerArgsAsync(appBuilder, serverResource, cancellationToken);

		// Assert
		await Assert.That(args).DoesNotContain("--hmr-port");
	}

	[Test]
	public async Task AddAspireC4_ContainerArgs_ExcludesHMRPortWhenHmrDisabled(CancellationToken cancellationToken)
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts =>
		{
			opts.ContainerImageTag = "100.57.0";
			opts.DisableHMR = true;
		});
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateContainerArgsAsync(appBuilder, serverResource, cancellationToken);

		// Assert
		await Assert.That(args).DoesNotContain("--hmr-port");
	}

	[Test]
	public async Task AddAspireC4_ContainerArgs_UsesConfiguredHmrPortValue(CancellationToken cancellationToken)
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		const int customHmrPort = 19876;

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts =>
		{
			opts.ContainerImageTag = "100.57.0";
			opts.HMRPort = customHmrPort;
		});
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateContainerArgsAsync(appBuilder, serverResource, cancellationToken);

		// Assert
		var hmrIdx = args.IndexOf("--hmr-port");
		await Assert.That(hmrIdx).IsGreaterThan(-1);
		await Assert.That(args[hmrIdx + 1]).IsEqualTo($"{customHmrPort}");
	}

	[Test]
	public async Task AddAspireC4_ContainerHmrEndpoint_TargetPortUsesDefaultHmrPort()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "100.57.0");
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var hmrEndpoint = serverResource
			.Annotations.OfType<EndpointAnnotation>()
			.Single(e => e.Name == AspireC4Resource.HMREndpointName);

		// Assert
		await Assert.That(hmrEndpoint.TargetPort).IsEqualTo(AspireC4Resource.DefaultHMRPort);
	}

	[Test]
	public async Task AddAspireC4_ContainerHmrEndpoint_TargetPortUsesConfiguredHmrPort()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		const int customHmrPort = 19876;

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts =>
		{
			opts.ContainerImageTag = "100.57.0";
			opts.HMRPort = customHmrPort;
		});
		var serverResource = (LikeC4ServerResource)visualization.Resource.InnerResource!;
		var hmrEndpoint = serverResource
			.Annotations.OfType<EndpointAnnotation>()
			.Single(e => e.Name == AspireC4Resource.HMREndpointName);

		// Assert
		await Assert.That(hmrEndpoint.TargetPort).IsEqualTo(customHmrPort);
	}

	[Test]
	public async Task AddAspireC4_StoresResolvedHmrPortInWorkspaceOptions()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		const int customHmrPort = 19876;

		// Act
		appBuilder.AddAspireC4(configure: opts =>
		{
			opts.ContainerImageTag = "100.57.0";
			opts.HMRPort = customHmrPort;
		});
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions = provider.GetRequiredService<IOptions<ContainerWorkspaceOptions>>();

		// Assert
		await Assert.That(workspaceOptions.Value.ResolvedHMRPort).IsEqualTo(customHmrPort);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859",
		Justification = "IResource used for test helper reuse across resource types"
	)]
	static async Task<List<string>> EvaluateContainerArgsAsync(
		IDistributedApplicationBuilder appBuilder,
		IResource resource,
		CancellationToken cancellationToken
	)
	{
		using var sp = appBuilder.Services.BuildServiceProvider();
		var annotations = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
		List<object> args = [];
		DistributedApplicationExecutionContext executionContext = new(
			new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run) { Services = sp }
		);
		CommandLineArgsCallbackContext context = new(args, resource, cancellationToken)
		{
			ExecutionContext = executionContext,
		};
		foreach (var annotation in annotations)
			await annotation.Callback(context);

		return [.. args.Select(static a => a?.ToString() ?? "")];
	}

	[Test]
	public async Task AddAspireC4_IOptions_AppliesCallbackValues()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts =>
		{
			opts.FormatGeneratedFile = false;
			opts.ViewTitle = "My Diagram";
		});
		using var provider = appBuilder.Services.BuildServiceProvider();
		var diagramOptions = provider.GetRequiredService<IOptions<AspireC4DiagramOptions>>();

		// Assert
		await Assert.That(diagramOptions.Value.FormatGeneratedFile).IsFalse();
		await Assert.That(diagramOptions.Value.ViewTitle).IsEqualTo("My Diagram");
	}

	// The callback can explicitly set a nullable property to null, overriding the default value.
	[Test]
	public async Task AddAspireC4_IOptions_CallbackCanSetNullExplicitly()
	{
		// Arrange — DefaultViewId has a default value of "index"; callback sets it to null
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4(configure: opts => opts.DefaultViewId = null);
		using var provider = appBuilder.Services.BuildServiceProvider();
		var diagramOptions = provider.GetRequiredService<IOptions<AspireC4DiagramOptions>>();

		// Assert — null from callback overrides the built-in default ("index")
		await Assert.That(diagramOptions.Value.DefaultViewId).IsNull();
	}
}
