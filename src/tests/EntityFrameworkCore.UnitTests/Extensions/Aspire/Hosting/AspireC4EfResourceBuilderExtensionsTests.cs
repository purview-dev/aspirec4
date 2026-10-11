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
	public async Task WithEntityFrameworkDiagram_WithD2Renderer_AddsTheDiagramAndContainerResources()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.D2);

		// Assert
		await Assert.That(appBuilder.Resources.OfType<AspireC4EfResource>().Count()).IsEqualTo(1);
		await Assert.That(appBuilder.Resources.OfType<D2ServerResource>().Count()).IsEqualTo(1);
	}

	[Test]
	public async Task WithEntityFrameworkDiagram_WithDefaultRenderer_AddsAViewerServer()
	{
		// The interactive renderer writes static files, so a server is needed for the dashboard to
		// have a URL to link to — a file:// link cannot be navigated to from the dashboard's origin.
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram();

		// Assert
		await Assert.That(appBuilder.Resources.OfType<AspireC4EfResource>().Count()).IsEqualTo(1);
		await Assert.That(appBuilder.Resources.OfType<D2ServerResource>()).IsEmpty();
		await Assert.That(appBuilder.Resources.OfType<ErdViewerServerResource>().Count()).IsEqualTo(1);
		await Assert
			.That(appBuilder.Resources.OfType<AspireC4EfResource>().Single().InnerResource)
			.IsTypeOf<ErdViewerServerResource>();
	}

	[Test]
	public async Task WithEntityFrameworkDiagram_WithViewerServerDisabled_AddsNoSidecar()
	{
		// The escape hatch for machines with no container runtime: the diagram is still generated and
		// the dashboard offers a command that opens it locally.
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram(configure: o => o.UseViewerServer = false);

		// Assert
		await Assert.That(appBuilder.Resources.OfType<ErdViewerServerResource>()).IsEmpty();
		await Assert.That(appBuilder.Resources.OfType<AspireC4EfResource>().Single().InnerResource).IsNull();
	}

	[Test]
	public async Task WithEntityFrameworkDiagram_WithDbmlRenderer_AddsNoSidecar()
	{
		// DBML is an interchange format with no viewer, so there is nothing to serve.
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.Dbml);

		// Assert
		await Assert.That(appBuilder.Resources.OfType<ErdViewerServerResource>()).IsEmpty();
		await Assert.That(appBuilder.Resources.OfType<AspireC4EfResource>().Single().InnerResource).IsNull();
	}

	[Test]
	public async Task WithEntityFrameworkDiagram_ViewerServer_UsesAPinnedImage()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram();
		var image = appBuilder
			.Resources.OfType<ErdViewerServerResource>()
			.Single()
			.Annotations.OfType<ContainerImageAnnotation>()
			.Single();

		// Assert
		await Assert.That(image.Image).IsEqualTo(ErdViewerServerResource.DefaultImage);
		await Assert.That(image.Tag).IsEqualTo(ErdViewerServerResource.DefaultTag);
	}

	[Test]
	public async Task WithEntityFrameworkDiagram_ContainerUsesTheCanonicalImage()
	{
		// terrastruct/d2 is an out-of-date fork and must never be the default.
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4().WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.D2);
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
		appBuilder
			.AddAspireC4()
			.WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.D2)
			.WithLocalD2CLI();

		// Assert
		await Assert.That(appBuilder.Resources.OfType<D2ServerResource>()).IsEmpty();
		await Assert.That(GetDiagram(appBuilder).InnerResource!).IsTypeOf<D2LocalServerResource>();
	}

	[Test]
	public async Task WithLocalD2CLI_DefaultsToTheD2BinaryOnPath()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder
			.AddAspireC4()
			.WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.D2)
			.WithLocalD2CLI();
		var local = (D2LocalServerResource)GetDiagram(appBuilder).InnerResource!;

		// Assert
		await Assert.That(local.Command).IsEqualTo("d2");
	}

	[Test]
	public async Task WithLocalD2CLI_HonoursAnExplicitCommand()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder
			.AddAspireC4()
			.WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.D2)
			.WithLocalD2CLI("/opt/d2/bin/d2");
		var local = (D2LocalServerResource)GetDiagram(appBuilder).InnerResource!;

		// Assert
		await Assert.That(local.Command).IsEqualTo("/opt/d2/bin/d2");
	}

	[Test]
	public async Task WithLocalD2CLI_RegistersTheHttpEndpoint()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder
			.AddAspireC4()
			.WithEntityFrameworkDiagram(configure: o => o.Renderer = ErdRenderer.D2)
			.WithLocalD2CLI();
		var local = (D2LocalServerResource)GetDiagram(appBuilder).InnerResource!;
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
