using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers how a relationship is named for a renderer.
/// </summary>
public sealed class EfRelationshipNameTests
{
	// Helpers

	static EfForeignKey CreateForeignKey(
		string? dependent = null,
		string? principal = null,
		string? constraint = null
	) =>
		new()
		{
			Columns = ["CustomerId"],
			PrincipalTable = "Customers",
			PrincipalColumns = ["Id"],
			NavigationName = dependent,
			PrincipalNavigationName = principal,
			ConstraintName = constraint,
		};

	// Tests

	[Test]
	public async Task Label_BothNavigations_NamesBothDirections()
	{
		// Act
		var label = EfRelationshipName.Label(CreateForeignKey(dependent: "Customer", principal: "Orders"));

		// Assert
		await Assert.That(label).IsEqualTo("Customer ⇄ Orders");
	}

	[Test]
	public async Task Label_DependentNavigationOnly_MarksTheOtherEnd()
	{
		// The end the model does not declare is marked rather than hidden: an unnamed direction is
		// information.
		// Act
		var label = EfRelationshipName.Label(CreateForeignKey(dependent: "Customer"));

		// Assert
		await Assert.That(label).IsEqualTo("Customer → ?");
	}

	[Test]
	public async Task Label_PrincipalNavigationOnly_MarksTheOtherEnd()
	{
		// Act
		var label = EfRelationshipName.Label(CreateForeignKey(principal: "Orders"));

		// Assert
		await Assert.That(label).IsEqualTo("? → Orders");
	}

	[Test]
	public async Task Label_NoNavigation_UsesTheConstraintName()
	{
		// Act
		var label = EfRelationshipName.Label(CreateForeignKey(constraint: "FK_Orders_Customers"));

		// Assert
		await Assert.That(label).IsEqualTo("FK_Orders_Customers");
	}

	[Test]
	public async Task Label_NothingKnown_FallsBackToTheColumns()
	{
		// Act
		var label = EfRelationshipName.Label(CreateForeignKey());

		// Assert
		await Assert.That(label).IsEqualTo("CustomerId");
	}
}
