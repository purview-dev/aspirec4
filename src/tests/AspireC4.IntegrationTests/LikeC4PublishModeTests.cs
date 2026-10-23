using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Tests the publish-mode code path: the lifecycle hook should generate the .c4 file
/// but not start the live server.
/// </summary>
[ClassDataSource<Fixtures.TestAppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class LikeC4PublishModeTests(Fixtures.TestAppHostFixture fixture)
{
	const string AspireC4ServerResourceName =
		AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
		+ AspireC4DistributedApplicationBuilderExtensions.AspireC4ServerResourceSuffix;

	/// <summary>
	/// Skips the current test when no container runtime is available so the suite
	/// shows "Skipped" rather than "Failed" on developer machines without Docker.
	/// </summary>
	[Before(Test)]
	public void SkipWhenNoContainerRuntime()
	{
		if (fixture.SkipReason is not null)
			Skip.Test(fixture.SkipReason);
	}

	[Test]
	public async Task PublishAsync_InPublishMode_ExportsNoImages(CancellationToken cancellationToken)
	{
		// Export is left enabled for this run, so the setup is not what makes this pass.
		//
		// A caveat worth knowing: this cannot distinguish "the guard skipped it" from "the export had
		// not finished", because an export is fire-and-forget and publish exits immediately afterwards.
		// That race is exactly why exporting on publish is wrong, and the rule itself is asserted
		// directly by AspireC4LifecycleHookTests.ShouldExportImages_InPublishMode_*. What this adds is
		// that a publish still produces its model and does not block on, or trip over, an export.
		// Arrange

		// Act
		var result = await fixture.PublishAsync(cancellationToken);

		// Assert
		await Assert.That(result.IsPublishMode).IsTrue();
		// The model still gets generated; it is only the images that are skipped.
		await Assert.That(File.Exists(result.ModelPath)).IsTrue();

		var images = Directory.Exists(result.ImageDirectory) ? Directory.GetFiles(result.ImageDirectory) : [];

		await Assert.That(images).IsEmpty();
	}

	[Test]
	public async Task PublishAsync_InPublishMode_GeneratesC4FileWithoutStartingServer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// (app built by the shared fixture in publish mode)

		// Act
		var result = await fixture.PublishAsync(cancellationToken);

		// Assert
		await Assert.That(result.IsPublishMode).IsTrue();
		await Assert.That(File.Exists(result.ModelPath)).IsTrue();
		await Assert.That(File.Exists(result.ManifestPath)).IsTrue();

		var content = await File.ReadAllTextAsync(result.ModelPath, cancellationToken);
		await Assert.That(content).Contains("specification {");
		await Assert.That(content).Contains("model {");
		await Assert.That(content).Contains("views {");

		var manifest = await File.ReadAllTextAsync(result.ManifestPath, cancellationToken);
		using var doc = JsonDocument.Parse(manifest);
		await Assert.That(doc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);

		// The live LikeC4 server must never start in publish mode — no endpoints are allocated
		// because DCP is never launched. Assert on the published application's model (not the
		// running fixture app), which represents the same AppHost built in publish mode.
		await Assert.That(HasAllocatedEndpoint(result.App, AspireC4ServerResourceName)).IsFalse();
	}

	static bool HasAllocatedEndpoint(DistributedApplication app, string resourceName) =>
		app.Services.GetRequiredService<DistributedApplicationModel>()
			.Resources.FirstOrDefault(r => string.Equals(r.Name, resourceName, StringComparison.Ordinal))
			?.Annotations.OfType<EndpointAnnotation>()
			.Any(e => e.AllocatedEndpoint is not null) == true;
}
