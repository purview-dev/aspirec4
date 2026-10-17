namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers the discovery and filtering configuration on the ER diagram options.
/// </summary>
public sealed class AspireC4EfDiagramOptionsTests
{
	// Tests

	[Test]
	public async Task DiscoverAllProjects_ByDefault_IsOn()
	{
		// The whole point of the default: WithEntityFrameworkDiagram() alone finds every context.
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.DiscoverAllProjects).IsTrue();
	}

	[Test]
	public async Task WithProjectOptInOnly_TurnsOffAutomaticDiscovery()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithProjectOptInOnly();

		// Assert
		await Assert.That(options.DiscoverAllProjects).IsFalse();
	}

	[Test]
	public async Task WithIncludedContexts_AddsTheNames()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithIncludedContexts("SalesDbContext", "AuditDbContext");

		// Assert
		await Assert.That(options.IncludedContexts).IsEquivalentTo(["SalesDbContext", "AuditDbContext"]);
	}

	[Test]
	public async Task WithExcludedContexts_AddsTheNames()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithExcludedContexts("IdentityDbContext");

		// Assert
		await Assert.That(options.ExcludedContexts).IsEquivalentTo(["IdentityDbContext"]);
	}

	[Test]
	public async Task WithExcludedContexts_RepeatedName_IsAddedOnce()
	{
		// Act
		var options = new AspireC4EfDiagramOptions()
			.WithExcludedContexts("IdentityDbContext")
			.WithExcludedContexts("identitydbcontext");

		// Assert
		await Assert.That(options.ExcludedContexts.Count).IsEqualTo(1);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task WithIncludedContexts_BlankName_Throws(string? name)
	{
		// Act & Assert
		await Assert.That(() => new AspireC4EfDiagramOptions().WithIncludedContexts(name!)).Throws<ArgumentException>();
	}

	[Test]
	public async Task ProjectOptions_CarryLabelAndFilters()
	{
		// Act
		AspireC4EfProjectOptions options = new() { Label = "API" };
		options.WithExcludedContexts("IdentityDbContext");

		// Assert
		await Assert.That(options.Label).IsEqualTo("API");
		await Assert.That(options.ExcludedContexts).IsEquivalentTo(["IdentityDbContext"]);
	}
}
