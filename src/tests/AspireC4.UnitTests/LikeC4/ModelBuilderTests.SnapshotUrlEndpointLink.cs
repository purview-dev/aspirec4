using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_SnapshotEndpointUrls_UsedInsteadOfAllocatedEndpoint()
	{
		// Arrange
		// When resourceSnapshotUrls are provided they should take precedence over
		// EndpointAnnotation.AllocatedEndpoint (which may reflect an internal/wrong port).
		var resource = CreateProjectResource("api");
		EndpointAnnotation endpoint = new(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 9999); // wrong port
		resource.Annotations.Add(endpoint);

		Dictionary<string, IReadOnlyList<(string Url, string Name)>> snapshotUrls = new(
			StringComparer.OrdinalIgnoreCase
		)
		{
			["api"] = [("http://localhost:5000", "http")],
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: snapshotUrls);

		var links = model.Elements[0].Links;
		// Assert
		await Assert.That(links.Any(l => l.Uri == "http://localhost:5000")).IsTrue();
		await Assert.That(links.Any(l => l.Uri == "http://localhost:9999")).IsFalse();
		await Assert.That(links.First(l => l.Uri == "http://localhost:5000").Title).IsEqualTo("Endpoint: http");
	}

	[Test]
	public async Task Build_SnapshotEndpointUrls_MultipleEndpoints_AllInjected()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		Dictionary<string, IReadOnlyList<(string Url, string Name)>> snapshotUrls = new(
			StringComparer.OrdinalIgnoreCase
		)
		{
			["api"] = [("http://localhost:5000", "http"), ("https://localhost:5001", "https")],
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: snapshotUrls);

		var links = model.Elements[0].Links;
		// Assert
		await Assert.That(links.Any(l => l.Uri == "http://localhost:5000" && l.Title == "Endpoint: http")).IsTrue();
		await Assert.That(links.Any(l => l.Uri == "https://localhost:5001" && l.Title == "Endpoint: https")).IsTrue();
	}

	[Test]
	public async Task Build_SnapshotEndpointUrls_WhenNull_FallsBackToAllocatedEndpoint()
	{
		// Arrange
		// No snapshot URLs → should fall back to EndpointAnnotation as before.
		var resource = CreateProjectResource("api");
		EndpointAnnotation endpoint = new(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: null);

		var links = model.Elements[0].Links;
		// Assert
		await Assert.That(links.Any(l => l.Uri == "http://localhost:5000")).IsTrue();
	}

	[Test]
	public async Task Build_SnapshotEndpointUrls_DeduplicatesWithUserLinks()
	{
		// Arrange
		// If the user manually added the same URL via WithLink, it should not appear twice.
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithLink("http://localhost:5000", "My link"));

		Dictionary<string, IReadOnlyList<(string Url, string Name)>> snapshotUrls = new(
			StringComparer.OrdinalIgnoreCase
		)
		{
			["api"] = [("http://localhost:5000", "http")],
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: snapshotUrls);

		var matchingLinks = model.Elements[0].Links.Where(l => l.Uri == "http://localhost:5000").ToList();
		// Assert
		await Assert.That(matchingLinks).Count().IsEqualTo(1);
		await Assert.That(matchingLinks[0].Title).IsEqualTo("My link");
	}
}
