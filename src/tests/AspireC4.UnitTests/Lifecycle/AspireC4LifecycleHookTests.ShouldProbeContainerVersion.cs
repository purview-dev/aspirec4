namespace Aspire.Hosting.AspireC4.Lifecycle;

public sealed partial class AspireC4LifecycleHookTests
{
	[Test]
	[Arguments("latest")]
	[Arguments("LATEST")]
	public async Task ShouldProbeContainerVersion_WithTheLatestTagAndTheCheckOn_IsTrue(string tag)
	{
		// Act
		var result = AspireC4LifecycleHook.ShouldProbeContainerVersion(tag, checkLatestImageVersion: true);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task ShouldProbeContainerVersion_WithAPinnedTag_IsFalse()
	{
		// A pinned tag already says which version runs, so no container is started to ask.
		// Act
		var result = AspireC4LifecycleHook.ShouldProbeContainerVersion("1.57.0", checkLatestImageVersion: true);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ShouldProbeContainerVersion_WhenTheCheckIsOff_IsFalse()
	{
		// Act
		var result = AspireC4LifecycleHook.ShouldProbeContainerVersion("latest", checkLatestImageVersion: false);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ContainerProbeTimeoutSeconds_WithTheFormatterDefault_AllowsForTheImagePull()
	{
		// The probe pulls the image, which a 30 s formatter budget does not cover on a fresh machine.
		// Act
		var result = AspireC4LifecycleHook.ContainerProbeTimeoutSeconds(30);

		// Assert
		await Assert.That(result).IsEqualTo(AspireC4LifecycleHook.ContainerProbeMinimumTimeoutSeconds);
	}

	[Test]
	public async Task ContainerProbeTimeoutSeconds_WithALargerConfiguredTimeout_UsesIt()
	{
		// Act
		var result = AspireC4LifecycleHook.ContainerProbeTimeoutSeconds(900);

		// Assert
		await Assert.That(result).IsEqualTo(900);
	}
}
