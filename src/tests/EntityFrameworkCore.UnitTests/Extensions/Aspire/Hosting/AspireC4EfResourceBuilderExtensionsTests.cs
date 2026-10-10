using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;

namespace Aspire.Hosting;

public sealed class AspireC4EfResourceBuilderExtensionsTests
{
	// Helpers

	static IDistributedApplicationBuilder CreateAppBuilder() => DistributedApplication.CreateBuilder([]);

	static AspireC4EfResource GetDiagram(IDistributedApplicationBuilder builder) =>
		builder.Resources.OfType<AspireC4EfResource>().Single();

	// Tests

	[Test]
	public async Task WithEntityFrameworkDiagram_AddsTheDiagramAndContainerResources()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram();

		// Assert
		await Assert.That(appBuilder.Resources.OfType<AspireC4EfResource>().Count()).IsEqualTo(1);
		await Assert.That(appBuilder.Resources.OfType<D2ServerResource>().Count()).IsEqualTo(1);
	}

	[Test]
	public async Task WithEntityFrameworkDiagram_ContainerUsesTheCanonicalImage()
	{
		// terrastruct/d2 is an out-of-date fork and must never be the default.
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram();
		var image = appBuilder
			.Resources.OfType<D2ServerResource>()
			.Single()
			.Annotations.OfType<ContainerImageAnnotation>()
			.Single();

		// Assert
		await Assert.That(image.Image).IsEqualTo("d2lang/d2");
		await Assert.That(image.Tag).IsEqualTo(D2ServerResource.DefaultTag);
	}

	[Test]
	public async Task WithEntityFrameworkModel_AddsTheOptInAnnotation()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var service = appBuilder.AddExecutable("svc", "dotnet", ".");

		// Act
		service.WithEntityFrameworkModel("Sample");

		// Assert
		var annotation = service.Resource.Annotations.OfType<AspireC4EfProjectAnnotation>().Single();
		await Assert.That(annotation.Label).IsEqualTo("Sample");
	}

	[Test]
	public async Task WithLocalD2CLI_ReplacesTheContainerWithAnExecutable()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram().WithLocalD2CLI();

		// Assert
		await Assert.That(appBuilder.Resources.OfType<D2ServerResource>()).IsEmpty();
		await Assert.That(GetDiagram(appBuilder).InnerResource).IsTypeOf<D2LocalServerResource>();
	}

	[Test]
	public async Task WithLocalD2CLI_DefaultsToTheD2BinaryOnPath()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram().WithLocalD2CLI();
		var local = (D2LocalServerResource)GetDiagram(appBuilder).InnerResource;

		// Assert
		await Assert.That(local.Command).IsEqualTo("d2");
	}

	[Test]
	public async Task WithLocalD2CLI_HonoursAnExplicitCommand()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram().WithLocalD2CLI("/opt/d2/bin/d2");
		var local = (D2LocalServerResource)GetDiagram(appBuilder).InnerResource;

		// Assert
		await Assert.That(local.Command).IsEqualTo("/opt/d2/bin/d2");
	}

	[Test]
	public async Task WithLocalD2CLI_RegistersTheHttpEndpoint()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram().WithLocalD2CLI();
		var local = (D2LocalServerResource)GetDiagram(appBuilder).InnerResource;
		var endpoint = local
			.Annotations.OfType<EndpointAnnotation>()
			.Single(e => e.Name == AspireC4EfResource.HttpEndpointName);

		// Assert
		await Assert.That(endpoint.TargetPort).IsEqualTo(D2ServerResource.DefaultPort);
	}

	[Test]
	public async Task WithLocalD2CLI_WithoutTheDiagram_Throws()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var c4 = appBuilder.AddAspireC4();

		// Act & Assert
		await Assert.That(() => c4.WithLocalD2CLI()).Throws<InvalidOperationException>();
	}
}
