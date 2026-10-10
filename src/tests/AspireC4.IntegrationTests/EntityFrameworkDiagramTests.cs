namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Verifies the d2 ER diagram is generated from the sample Entity Framework Core models during a real
/// AppHost run.
/// </summary>
/// <remarks>
/// This is the only test that exercises the full AppHost path — annotation discovery, locating each
/// contributing project's build output, reading descriptors from the assemblies in it, and writing the
/// diagram. The unit tests read sample assemblies directly, so they cannot catch a break in that path.
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

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesTheErDiagram()
	{
		// Assert
		await Assert.That(File.Exists(fixture.ErDiagramPath)).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramPinsARowTargetingLayoutEngine(
		CancellationToken cancellationToken
	)
	{
		// dagre cannot target individual sql_table rows, so the generated file must pin elk or tala.
		// Act
		var content = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert
		await Assert.That(content).Contains("layout-engine: elk");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramContainsEveryDiscoveredContext(
		CancellationToken cancellationToken
	)
	{
		// Act
		var content = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert — the simple sample's single context plus the complex sample's three.
		await Assert.That(content).Contains("BlogDbContext");
		await Assert.That(content).Contains("SalesDbContext");
		await Assert.That(content).Contains("IdentityDbContext");
		await Assert.That(content).Contains("AuditDbContext");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramRendersTablesAndColumnLevelEdges(
		CancellationToken cancellationToken
	)
	{
		// Act
		var content = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert
		await Assert.That(content).Contains("shape: sql_table");
		await Assert.That(content).Contains("BlogDbContext.Posts.BlogId -> BlogDbContext.Blogs.Id");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramPreservesAdvancedMappings(CancellationToken cancellationToken)
	{
		// Act
		var content = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert — attribute renames, fluent renames and types, and a composite key.
		await Assert.That(content).Contains("product_id");
		await Assert.That(content).Contains("email_address");
		await Assert.That(content).Contains("decimal(18,2)");
		await Assert.That(content).Contains("constraint: [primary_key; foreign_key]");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramOmitsExcludedProperties(CancellationToken cancellationToken)
	{
		// Act
		var content = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert — [NotMapped] and the fluent Ignore call.
		await Assert.That(content).DoesNotContain("Initials");
		await Assert.That(content).DoesNotContain("CachedDisplayName");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramDoesNotLinkAcrossContexts(CancellationToken cancellationToken)
	{
		// AuditEntry.ActorUserId references the identity context's Users by id with no navigation.
		// Act
		var content = await File.ReadAllTextAsync(fixture.ErDiagramPath, cancellationToken);

		// Assert
		await Assert.That(content).Contains("ActorUserId");
		await Assert.That(content).DoesNotContain("ActorUserId -> ");
	}
}
