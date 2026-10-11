using System.Text.Json;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Verifies the ER diagram is generated from the sample Entity Framework Core models during a real
/// AppHost run, using the default (interactive) renderer.
/// </summary>
/// <remarks>
/// This is the only test that exercises the full AppHost path — annotation discovery, locating each
/// contributing project's build output, reading descriptors from the assemblies in it, and writing the
/// diagram. The unit tests read sample assemblies directly, so they cannot catch a break in that path.
/// Emitter output itself is asserted exactly in <c>ErdEmitterTests</c>.
/// </remarks>
[ClassDataSource<Fixtures.TestAppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class EntityFrameworkDiagramTests(Fixtures.TestAppHostFixture fixture)
{
	[Before(Test)]
	public void SkipWhenNoContainerRuntime()
	{
		if (fixture.SkipReason is not null)
			Skip.Test(fixture.SkipReason);
	}

	static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesTheModelAndViewer()
	{
		// Assert — the default renderer emits its data plus the viewer that reads it.
		await Assert.That(File.Exists(fixture.ErDiagramPath)).IsTrue();
		await Assert.That(File.Exists(fixture.ErViewerPath)).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelContainsEveryDiscoveredContext(CancellationToken cancellationToken)
	{
		// Act
		var json = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);
		var names = Root(json)
			.GetProperty("contexts")
			.EnumerateArray()
			.Select(c => c.GetProperty("name").GetString())
			.ToArray();

		// Assert — the simple sample's single context plus the complex sample's three.
		await Assert.That(names).Contains("BlogDbContext");
		await Assert.That(names).Contains("SalesDbContext");
		await Assert.That(names).Contains("IdentityDbContext");
		await Assert.That(names).Contains("AuditDbContext");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelPreservesAdvancedMappings(CancellationToken cancellationToken)
	{
		// Act
		var json = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert — attribute renames, fluent renames and fluent column types all survive.
		await Assert.That(json).Contains("product_id");
		await Assert.That(json).Contains("email_address");
		await Assert.That(json).Contains("decimal(18,2)");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelOmitsExcludedProperties(CancellationToken cancellationToken)
	{
		// Act
		var json = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert — [NotMapped] and the fluent Ignore call.
		await Assert.That(json).DoesNotContain("Initials");
		await Assert.That(json).DoesNotContain("CachedDisplayName");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramResourceReportsAState(CancellationToken cancellationToken)
	{
		// The resource runs no process of its own, so without publishing its own state the dashboard
		// leaves it Unknown with no logs — which is exactly how this shipped the first time.
		// Act
		var state = await fixture.GetResourceStateAsync("aspirec4-er", cancellationToken);

		// Assert
		await Assert.That(state).IsEqualTo(KnownResourceStates.Running);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ViewerPointsAtTheGeneratedModel(CancellationToken cancellationToken)
	{
		// Act
		var script = await File.ReadAllTextAsync(
			Path.Combine(Path.GetDirectoryName(fixture.ErViewerPath)!, "app.js"),
			cancellationToken
		);

		// Assert — the placeholder must have been substituted, or the viewer loads nothing.
		await Assert.That(script).Contains("schema.gen.json");
		await Assert.That(script).DoesNotContain("__MODEL__");
	}
}
