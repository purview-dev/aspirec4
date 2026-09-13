using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_StateTagMap_RunningOverride_PrependsStateTagToElementTags()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		Dictionary<string, string?> states = new() { { "api", KnownResourceStates.Running } };
		Dictionary<string, string?> stateTagMap = new() { [KnownResourceStates.Running] = "custom-running-tag" };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).Contains("custom-running-tag");
	}

	[Test]
	public async Task Build_StateTagMap_NullOverride_DoesNotPrependTag()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		Dictionary<string, string?> states = new() { { "api", KnownResourceStates.Running } };
		Dictionary<string, string?> stateTagMap = new() { [KnownResourceStates.Running] = null };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).IsEmpty();
	}

	[Test]
	public async Task Build_StateTagMap_NullMap_AutoDerivesStateTagOnElement()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		Dictionary<string, string?> states = new() { { "api", KnownResourceStates.Running } };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: null);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).Contains("aspire-run-state-running");
	}

	[Test]
	public async Task Build_StateTagMap_StateTagPrependsBeforeUserTags()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithTag("backend").WithTag("v2"));

		Dictionary<string, string?> states = new() { { "api", KnownResourceStates.FailedToStart } };
		Dictionary<string, string?> stateTagMap = new() { [KnownResourceStates.FailedToStart] = "custom-error-tag" };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var tags = model.Elements[0].Tags;
		// Assert
		await Assert.That(tags[0]).IsEqualTo("custom-error-tag");
		await Assert.That(tags).Contains("backend");
		await Assert.That(tags).Contains("v2");
	}

	[Test]
	public async Task Build_StateTagMap_CustomTagName_UsedAsStateTag()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		Dictionary<string, string?> states = new() { { "api", KnownResourceStates.RuntimeUnhealthy } };
		Dictionary<string, string?> stateTagMap = new()
		{
			[KnownResourceStates.RuntimeUnhealthy] = "my-custom-failed-tag",
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		// Assert
		await Assert.That(model.Elements[0].Tags).Contains("my-custom-failed-tag");
	}
}
