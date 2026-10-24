namespace Aspire.Hosting.AspireC4.Lifecycle;

public sealed partial class AspireC4LifecycleHookTests
{
	[Test]
	public async Task ShouldExportImages_OnAChangedModelDuringARun_IsTrue()
	{
		// Act
		var result = AspireC4LifecycleHook.ShouldExportImages(changed: true, exportImages: true, isPublishMode: false);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task ShouldExportImages_OnAnUnchangedModel_IsFalse()
	{
		// The expensive part of an export is starting a container; it is worth doing only when there is
		// something new to render.
		// Act
		var result = AspireC4LifecycleHook.ShouldExportImages(changed: false, exportImages: true, isPublishMode: false);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ShouldExportImages_InPublishMode_IsFalseEvenForAChangedModel()
	{
		// Publish generates the model and exits, so a fire-and-forget export would be killed part-way
		// through. It is also expected to be hermetic: a build agent may have no container runtime.
		// Act
		var result = AspireC4LifecycleHook.ShouldExportImages(changed: true, exportImages: true, isPublishMode: true);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ShouldExportImages_WhenExportIsOff_IsFalse()
	{
		// Act
		var result = AspireC4LifecycleHook.ShouldExportImages(changed: true, exportImages: false, isPublishMode: false);

		// Assert
		await Assert.That(result).IsFalse();
	}
}
