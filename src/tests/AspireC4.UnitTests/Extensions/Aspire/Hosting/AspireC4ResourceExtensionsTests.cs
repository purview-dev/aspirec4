using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

public sealed class AspireC4ResourceExtensionsTests
{
	[Test]
	public async Task WithLocalCLI_RegistersHmrHttpEndpoint()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4().WithLocalCLI();
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		var hmrEndpoint = localResource
			.Annotations.OfType<EndpointAnnotation>()
			.FirstOrDefault(e => e.Name == AspireC4Resource.HMREndpointName);

		// Assert
		await Assert.That(hmrEndpoint).IsNotNull();
		await Assert.That(hmrEndpoint!.TargetPort).IsEqualTo(AspireC4Resource.DefaultHMRPort);
	}

	[Test]
	public async Task WithLocalCLI_RegistersHttpEndpoint()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		var visualization = appBuilder.AddAspireC4().WithLocalCLI();
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		var httpEndpoint = localResource
			.Annotations.OfType<EndpointAnnotation>()
			.FirstOrDefault(e => e.Name == AspireC4Resource.HttpEndpointName);

		// Assert
		await Assert.That(httpEndpoint).IsNotNull();
		await Assert.That(httpEndpoint!.TargetPort).IsEqualTo(AspireC4Resource.DefaultPort);
	}

	[Test]
	public async Task WithLocalCLI_Args_IncludeHmrPort_WhenHmrEnabled()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		using CancellationTokenSource cts = new();
		var cancellationToken = cts.Token;

		// Act
		var visualization = appBuilder.AddAspireC4().WithLocalCLI();
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateLocalArgsAsync(appBuilder, localResource, cancellationToken);

		// Assert
		var hmrIdx = args.IndexOf("--hmr-port");
		await Assert.That(hmrIdx).IsGreaterThan(-1);
		await Assert.That(args[hmrIdx + 1]).IsEqualTo($"{AspireC4Resource.DefaultHMRPort}");
	}

	[Test]
	public async Task WithLocalCLI_Args_ExcludeHmrPort_WhenHmrDisabled()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		using CancellationTokenSource cts = new();
		var cancellationToken = cts.Token;

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.DisableHMR = true).WithLocalCLI();
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateLocalArgsAsync(appBuilder, localResource, cancellationToken);

		// Assert
		await Assert.That(args).DoesNotContain("--hmr-port");
	}

	[Test]
	public async Task WithLocalCLI_Args_UseConfiguredHmrPort()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		using CancellationTokenSource cts = new();
		var cancellationToken = cts.Token;
		const int customHmrPort = 19876;

		// Act
		var visualization = appBuilder.AddAspireC4(configure: opts => opts.HMRPort = customHmrPort).WithLocalCLI();
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateLocalArgsAsync(appBuilder, localResource, cancellationToken);

		// Assert
		var hmrIdx = args.IndexOf("--hmr-port");
		await Assert.That(hmrIdx).IsGreaterThan(-1);
		await Assert.That(args[hmrIdx + 1]).IsEqualTo($"{customHmrPort}");
	}

	[Test]
	public async Task WithLocalCLI_Args_IncludeServeAndPortArgs()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		using CancellationTokenSource cts = new();
		var cancellationToken = cts.Token;

		// Act
		var visualization = appBuilder.AddAspireC4().WithLocalCLI();
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		var args = await EvaluateLocalArgsAsync(appBuilder, localResource, cancellationToken);

		// Assert
		await Assert.That(args).Contains("serve");
		await Assert.That(args).Contains("--port");
		await Assert.That(args).Contains($"{AspireC4Resource.DefaultPort}");
	}

	[Test]
	public async Task WithServerLikeC4Details_AppliesTheDetailsToTheServerResource()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var visualization = appBuilder.AddAspireC4();

		// Act
		visualization.WithServerLikeC4Details(static details =>
			details.WithLabel("AspireC4").WithSummary("Live diagram")
		);

		// Assert
		var details = ServerDetails(visualization.Resource.InnerResource!);
		await Assert.That(details).IsNotNull();
		await Assert.That(details!.Label).IsEqualTo("AspireC4");
		await Assert.That(details.Summary).IsEqualTo("Live diagram");
	}

	[Test]
	public async Task WithServerLikeC4Details_LeavesTheOuterResourceWithoutDetails()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var visualization = appBuilder.AddAspireC4();

		// Act
		visualization.WithServerLikeC4Details(static details => details.WithLabel("AspireC4"));

		// Assert
		await Assert.That(ServerDetails(visualization.Resource)).IsNull();
	}

	[Test]
	public async Task WithServerLikeC4Details_BeforeWithLocalCLI_MovesTheDetailsToTheCLIResource()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var visualization = appBuilder.AddAspireC4();

		// Act
		visualization
			.WithServerLikeC4Details(static details => details.WithLabel("AspireC4"))
			.WithLocalCLI(LocalCLIRuntime.Npx);

		// Assert
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		await Assert.That(ServerDetails(localResource)?.Label).IsEqualTo("AspireC4");
	}

	[Test]
	public async Task WithServerLikeC4Details_AfterWithLocalCLI_AppliesTheDetailsToTheCLIResource()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var visualization = appBuilder.AddAspireC4().WithLocalCLI(LocalCLIRuntime.Npx);

		// Act
		visualization.WithServerLikeC4Details(static details => details.WithLabel("AspireC4"));

		// Assert
		var localResource = (LikeC4LocalServerResource)visualization.Resource.InnerResource!;
		await Assert.That(ServerDetails(localResource)?.Label).IsEqualTo("AspireC4");
	}

	static AspireC4.LikeC4.Annotations.LikeC4NodeDetailsAnnotation? ServerDetails(IResource resource) =>
		resource.Annotations.OfType<AspireC4.LikeC4.Annotations.LikeC4NodeDetailsAnnotation>().LastOrDefault();

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859",
		Justification = "IResource used for test helper reuse across resource types"
	)]
	static async Task<List<string>> EvaluateLocalArgsAsync(
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
}
