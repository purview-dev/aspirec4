using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers the include/exclude filters that keep a shared or unwanted <c>DbContext</c> out of the
/// diagram.
/// </summary>
public sealed class EfContextFilterTests
{
	// Helpers

	static EfContext CreateContext(string name, string typeName) => new() { Name = name, TypeName = typeName };

	static readonly EfContext Identity = CreateContext("IdentityDbContext", "Shared.Data.IdentityDbContext");
	static readonly EfContext Sales = CreateContext("SalesDbContext", "Sample.Sales.SalesDbContext");

	static bool Allows(EfContext context, string[] included, string[] excluded) =>
		EfContextFilter.Allows(context, included, excluded);

	// Tests

	[Test]
	public async Task Allows_NoFilters_PermitsEverything()
	{
		// Act
		var allowed = Allows(Identity, [], []);

		// Assert
		await Assert.That(allowed).IsTrue();
	}

	[Test]
	public async Task Allows_ExcludedByShortName_RejectsIt()
	{
		// Act
		var allowed = Allows(Identity, [], ["IdentityDbContext"]);

		// Assert
		await Assert.That(allowed).IsFalse();
	}

	[Test]
	public async Task Allows_ExcludedByFullTypeName_RejectsIt()
	{
		// Act
		var allowed = Allows(Identity, [], ["Shared.Data.IdentityDbContext"]);

		// Assert
		await Assert.That(allowed).IsFalse();
	}

	[Test]
	public async Task Allows_ExclusionIsCaseInsensitive()
	{
		// Act
		var allowed = Allows(Identity, [], ["identitydbcontext"]);

		// Assert
		await Assert.That(allowed).IsFalse();
	}

	[Test]
	public async Task Allows_IncludeList_PermitsOnlyTheListedContext()
	{
		// Act
		var identityAllowed = Allows(Identity, ["SalesDbContext"], []);
		var salesAllowed = Allows(Sales, ["SalesDbContext"], []);

		// Assert
		await Assert.That(identityAllowed).IsFalse();
		await Assert.That(salesAllowed).IsTrue();
	}

	[Test]
	public async Task Allows_ExclusionWinsOverInclusion()
	{
		// Act
		var allowed = Allows(Identity, ["IdentityDbContext"], ["IdentityDbContext"]);

		// Assert
		await Assert.That(allowed).IsFalse();
	}

	[Test]
	public async Task Apply_ExcludingAContext_RemovesItFromTheModel()
	{
		// Arrange
		EfModel model = new() { Contexts = [Identity, Sales] };

		// Act
		var filtered = EfContextFilter.Apply(model, [], ["IdentityDbContext"]);

		// Assert
		await Assert.That(filtered.Contexts.Select(c => c.Name)).IsEquivalentTo(["SalesDbContext"]);
	}

	[Test]
	public async Task Apply_NoFilters_ReturnsTheSameModel()
	{
		// Arrange
		EfModel model = new() { Contexts = [Identity, Sales] };

		// Act
		var filtered = EfContextFilter.Apply(model, [], []);

		// Assert
		await Assert.That(filtered).IsSameReferenceAs(model);
	}
}
