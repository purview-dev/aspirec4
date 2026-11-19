using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_WithExcludedResourceTypeNames_ExcludesAResourceOfThatType()
	{
		// Arrange
		var container = CreateContainerResource("redis");
		var api = CreateProjectResource("api");
		HashSet<string> excludedNames = [typeof(ContainerResource).FullName!];

		// Act
		var model = ModelBuilder.Build([container, api], excludedResourceTypeNames: excludedNames);

		// Assert
		await Assert.That(model.Elements.Select(e => e.Name)).DoesNotContain("redis");
		await Assert.That(model.Elements.Select(e => e.Name)).Contains("api");
	}

	[Test]
	public async Task Build_WithExcludedBaseTypeName_ExcludesSubclassResources()
	{
		// Arrange
		// ExecutableResource is a base type of the test's own subclass, matched by name only.
		CustomExecutableResource worker = new("worker");
		HashSet<string> excludedNames = [typeof(ExecutableResource).FullName!];

		// Act
		var model = ModelBuilder.Build([worker], excludedResourceTypeNames: excludedNames);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_WithAnUnrelatedExcludedTypeName_RetainsTheResource()
	{
		// Arrange
		var container = CreateContainerResource("redis");
		HashSet<string> excludedNames = [AspireC4DiagramOptions.AzureEnvironmentResourceTypeName];

		// Act
		var model = ModelBuilder.Build([container], excludedResourceTypeNames: excludedNames);

		// Assert
		await Assert.That(model.Elements.Select(e => e.Name)).Contains("redis");
	}

	[Test]
	public async Task GetVisibleResourceNames_WithExcludedResourceTypeNames_OmitsMatchingResourceNames()
	{
		// Arrange
		var container = CreateContainerResource("redis");
		var api = CreateProjectResource("api");
		HashSet<string> excludedNames = [typeof(ContainerResource).FullName!];

		// Act
		var names = ModelBuilder.GetVisibleResourceNames([container, api], excludedResourceTypeNames: excludedNames);

		// Assert
		await Assert.That(names).DoesNotContain("redis");
		await Assert.That(names).Contains("api");
	}

	sealed class CustomExecutableResource(string name) : ExecutableResource(name, "worker", ".");
}
