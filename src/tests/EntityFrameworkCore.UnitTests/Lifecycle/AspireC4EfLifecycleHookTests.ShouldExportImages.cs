namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

/// <summary>
/// Covers when the ER diagram follows a write with an image export.
/// </summary>
public sealed class AspireC4EfLifecycleHookTests
{
	[Test]
	public async Task ShouldExportImages_WithContextsDuringARun_IsTrue()
	{
		// Act
		var result = AspireC4EfLifecycleHook.ShouldExportImages(exportImages: true, contexts: 2, isPublishMode: false);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task ShouldExportImages_WithNoContexts_IsFalse()
	{
		// An empty model is the transient state before a contributing project has been built; rendering
		// it would replace a good image with a picture of nothing.
		// Act
		var result = AspireC4EfLifecycleHook.ShouldExportImages(exportImages: true, contexts: 0, isPublishMode: false);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ShouldExportImages_InPublishMode_IsFalseEvenWithContexts()
	{
		// Publish generates its output and exits, so a fire-and-forget export would be killed part-way
		// through. It is also expected to be hermetic: a build agent may have no container runtime.
		// Act
		var result = AspireC4EfLifecycleHook.ShouldExportImages(exportImages: true, contexts: 2, isPublishMode: true);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ShouldExportImages_WhenExportIsOff_IsFalse()
	{
		// Act
		var result = AspireC4EfLifecycleHook.ShouldExportImages(exportImages: false, contexts: 2, isPublishMode: false);

		// Assert
		await Assert.That(result).IsFalse();
	}
}
