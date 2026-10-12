using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// Covers resources mapped onto a hand-authored LikeC4 project with <c>WithLikeC4Element</c>.
/// </summary>
public sealed class ModelBuilderExistingElementTests
{
	// Helpers

	static ContainerResource CreateResource(string name = "api", string? elementId = "cloud.backend.api")
	{
		ContainerResource resource = new(name);

		if (elementId is not null)
		{
			resource.Annotations.Add(new LikeC4ExistingElementAnnotation(elementId));
		}

		return resource;
	}

	static Models.LikeC4Element BuildSingleElement(params IResource[] resources) =>
		ModelBuilder.Build(resources, resourceStates: null, autoIconsEnabled: true).Elements[0];

	// Tests

	[Test]
	public async Task Build_MappedResource_UsesTheExistingElementIdentifier()
	{
		// Arrange
		var resource = CreateResource();

		// Act
		var element = BuildSingleElement(resource);

		// Assert
		await Assert.That(element.Name).IsEqualTo("cloud.backend.api");
		await Assert.That(element.ExtendsExistingElement).IsTrue();
	}

	[Test]
	public async Task Build_UnmappedResource_IsNotMarkedAsExtending()
	{
		// Arrange
		var resource = CreateResource(elementId: null);

		// Act
		var element = BuildSingleElement(resource);

		// Assert
		await Assert.That(element.Name).IsEqualTo("api");
		await Assert.That(element.ExtendsExistingElement).IsFalse();
	}

	[Test]
	public async Task Build_MappedResource_SuppressesInferredTechnologyAndIcon()
	{
		// A guess would silently replace what the project's author chose.
		// Arrange
		var resource = CreateResource(name: "redis");

		// Act
		var element = BuildSingleElement(resource);

		// Assert
		await Assert.That(element.Technology).IsNull();
		await Assert.That(element.Icon).IsNull();
	}

	[Test]
	public async Task Build_UnmappedResource_StillInfersAnIcon()
	{
		// Guards the suppression above against disabling inference for everything else.
		// Arrange
		var resource = CreateResource(name: "redis", elementId: null);

		// Act
		var element = BuildSingleElement(resource);

		// Assert
		await Assert.That(element.Icon).IsNotNull();
	}

	[Test]
	public async Task Build_MappedResourceWithExplicitProse_DropsWhatExtendCannotCarry()
	{
		// LikeC4's extend body accepts tags, links, metadata and nested declarations only, so prose and
		// technology have nowhere to go and the project they come from keeps its own.
		// Arrange
		var resource = CreateResource();
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(resource.Name)
				.WithTechnology("ASP.NET Core")
				.WithDescription("Set through WithLikeC4Details")
				.WithSummary("A summary")
				.WithIcon("tech:dotnet")
		);

		// Act
		var element = BuildSingleElement(resource);

		// Assert
		await Assert.That(element.Technology).IsNull();
		await Assert.That(element.Description).IsNull();
		await Assert.That(element.Summary).IsNull();
		await Assert.That(element.Icon).IsNull();
	}

	[Test]
	public async Task Build_MappedResourceWithExplicitTagsAndLinks_KeepsThem()
	{
		// These are the properties an extend block can carry, so they must survive.
		// Arrange
		var resource = CreateResource();
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(resource.Name)
				.WithTag("critical")
				.WithLink("https://example.com/runbook", "Runbook")
				.WithMetadata("team", "Backend")
		);

		// Act
		var element = BuildSingleElement(resource);

		// Assert
		await Assert.That(element.Tags).Contains("critical");
		await Assert.That(element.Links.Select(l => l.Uri)).Contains("https://example.com/runbook");
		await Assert.That(element.Metadata.Select(m => m.Key)).Contains("team");
	}

	[Test]
	public async Task Build_RelationshipToAMappedResource_TargetsTheExistingElementIdentifier()
	{
		// Every reference has to follow the mapping, or it points at an element that does not exist.
		// Arrange
		var api = CreateResource();
		var caller = CreateResource(name: "web", elementId: null);
		caller.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("api"));

		// Act
		var model = ModelBuilder.Build([api, caller], resourceStates: null, autoIconsEnabled: true);

		// Assert
		var relationship = model.Relationships.Single();
		await Assert.That(relationship.SourceName).IsEqualTo("web");
		await Assert.That(relationship.TargetName).IsEqualTo("cloud.backend.api");
	}
}
