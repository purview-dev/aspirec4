using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_WithExcludedResourceTypes_ExcludesMatchingResource()
	{
		// Arrange
		var api = CreateProjectResource("api");
		ParameterResource param = new("db-password", _ => "secret", secret: true);
		HashSet<Type> excludedTypes = [typeof(ParameterResource)];

		// Act
		var model = ModelBuilder.Build([api, param], excludedResourceTypes: excludedTypes);

		// Assert
		await Assert.That(model.Elements.Select(e => e.Name)).DoesNotContain("db-password");
	}

	[Test]
	public async Task Build_WithExcludedResourceTypes_RetainsNonMatchingResources()
	{
		// Arrange
		var api = CreateProjectResource("api");
		ParameterResource param = new("db-password", _ => "secret", secret: true);
		HashSet<Type> excludedTypes = [typeof(ParameterResource)];

		// Act
		var model = ModelBuilder.Build([api, param], excludedResourceTypes: excludedTypes);

		// Assert
		await Assert.That(model.Elements.Select(e => e.Name)).Contains("api");
	}

	[Test]
	public async Task Build_WithExcludedBaseType_ExcludesSubclassResources()
	{
		// Arrange
		// ContainerResource is a base type; ProjectResource is NOT a subclass of ContainerResource.
		var container = CreateContainerResource("redis");
		var project = CreateProjectResource("api");
		HashSet<Type> excludedTypes = [typeof(ContainerResource)];

		// Act
		var model = ModelBuilder.Build([container, project], excludedResourceTypes: excludedTypes);

		// Assert
		await Assert.That(model.Elements.Select(e => e.Name)).DoesNotContain("redis");
		await Assert.That(model.Elements.Select(e => e.Name)).Contains("api");
	}

	[Test]
	public async Task Build_WithNullExcludedResourceTypes_IncludesParameterResources()
	{
		// Arrange
		ParameterResource param = new("db-password", _ => "secret", secret: true);

		// Act
		var model = ModelBuilder.Build([param], excludedResourceTypes: null);

		// Assert
		await Assert.That(model.Elements.Select(e => e.Name)).Contains("db-password");
	}

	[Test]
	public async Task GetVisibleResourceNames_WithExcludedResourceTypes_OmitsMatchingResourceNames()
	{
		// Arrange
		var api = CreateProjectResource("api");
		ParameterResource param = new("db-password", _ => "secret", secret: true);
		HashSet<Type> excludedTypes = [typeof(ParameterResource)];

		// Act
		var names = ModelBuilder.GetVisibleResourceNames([api, param], excludedTypes);

		// Assert
		await Assert.That(names).DoesNotContain("db-password");
		await Assert.That(names).Contains("api");
	}

	[Test]
	public async Task GetVisibleResourceNames_WithNullExcludedResourceTypes_IncludesAllVisibleResources()
	{
		// Arrange
		var api = CreateProjectResource("api");
		ParameterResource param = new("db-password", _ => "secret", secret: true);

		// Act
		var names = ModelBuilder.GetVisibleResourceNames([api, param], excludedResourceTypes: null);

		// Assert
		await Assert.That(names).Contains("db-password");
		await Assert.That(names).Contains("api");
	}
}
