using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_AutoMetadata_Default_InjectsAspireName()
	{
		// Arrange
		var resource = CreateProjectResource("my-api");

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "aspire-name" && m.Value == "my-api")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_Default_InjectsAspireType()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Project", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "aspire-type" && m.Value == "Project")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_None_DoesNotInjectMetadata()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.None);

		// Assert
		await Assert.That(model.Elements[0].Metadata).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_MetadataOnly_NoAutoLinks()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http")
		);

		// Act
		var model = ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.Metadata);

		// Assert
		await Assert.That(model.Elements[0].Links).IsEmpty();
		await Assert.That(model.Elements[0].Metadata.Any(m => m.Key == "aspire-name")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_LinksOnly_NoAspireNameMetadata()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.Links);

		// Assert
		await Assert.That(model.Elements[0].Metadata.Any(m => m.Key == "aspire-name")).IsFalse();
	}

	[Test]
	public async Task Build_AutoMetadata_AllocatedHttpEndpoint_InjectsLink()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		EndpointAnnotation endpoint = new(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);

		// Act
		var model = ModelBuilder.Build([resource]);

		var links = model.Elements[0].Links;
		var injected = links.FirstOrDefault(l => l.Uri == "http://localhost:5000");
		// Assert
		await Assert.That(injected).IsNotNull();
		await Assert.That(injected!.Title).IsEqualTo("Endpoint: http");
	}

	[Test]
	public async Task Build_AutoMetadata_UnallocatedEndpoint_DoesNotInjectLink()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Links).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_NonHttpEndpoint_DoesNotInjectLink()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		EndpointAnnotation endpoint = new(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "grpc", name: "grpc");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5001);
		resource.Annotations.Add(endpoint);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Links).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_UserMetadataPreservedAndNotDuplicated()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("aspire-name", "custom-override"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var aspireNameEntries = model.Elements[0].Metadata.Where(m => m.Key == "aspire-name").ToList();
		// User-provided entry present, auto-generated one should not duplicate it
		// Assert
		await Assert.That(aspireNameEntries).Count().IsEqualTo(1);
		await Assert.That(aspireNameEntries[0].Value).IsEqualTo("custom-override");
	}

	[Test]
	public async Task Build_AutoMetadata_UserLinksPreservedAndEndpointNotDuplicated()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		EndpointAnnotation endpoint = new(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API").WithLink("http://localhost:5000", "My Service")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		var httpLinks = model.Elements[0].Links.Where(l => l.Uri == "http://localhost:5000").ToList();
		// Should appear only once — auto-injected duplicate is suppressed
		// Assert
		await Assert.That(httpLinks).Count().IsEqualTo(1);
		await Assert.That(httpLinks[0].Title).IsEqualTo("My Service");
	}
}
