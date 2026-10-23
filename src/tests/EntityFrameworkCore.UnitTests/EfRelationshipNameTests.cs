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

	static string Label(
		EfForeignKey foreignKey,
		EfRelationshipDirection direction = EfRelationshipDirection.DependentToPrincipal
	) => EfRelationshipName.Label(foreignKey, direction);

	// Tests

	[Test]
	public async Task Label_BothNavigations_NamesBothDirections()
	{
		// Act
		var label = Label(CreateForeignKey(dependent: "Customer", principal: "Orders"));

		// Assert
		await Assert.That(label).IsEqualTo("Customer ⇄ Orders");
	}

	[Test]
	public async Task Label_BothNavigationsFromThePrincipal_NamesThatEndFirst()
	{
		// The label names a direction of travel, so the end nearest the start of the edge comes first or
		// the arrow in the label disagrees with the arrow on the edge.
		// Act
		var label = Label(
			CreateForeignKey(dependent: "Customer", principal: "Orders"),
			EfRelationshipDirection.PrincipalToDependent
		);

		// Assert
		await Assert.That(label).IsEqualTo("Orders ⇄ Customer");
	}

	[Test]
	public async Task Label_DependentNavigationOnly_PointsAlongTheEdge()
	{
		// Act
		var label = Label(CreateForeignKey(dependent: "Customer"));

		// Assert
		await Assert.That(label).IsEqualTo("Customer →");
	}

	[Test]
	public async Task Label_DependentNavigationOnlyFromThePrincipal_PointsBackAlongTheEdge()
	{
		// The only declared navigation is travelled against the edge, and the undeclared end is left
		// unnamed rather than marked: a placeholder there reads as a relationship nobody could resolve.
		// Act
		var label = Label(CreateForeignKey(dependent: "Customer"), EfRelationshipDirection.PrincipalToDependent);

		// Assert
		await Assert.That(label).IsEqualTo("← Customer");
	}

	[Test]
	public async Task Label_PrincipalNavigationOnly_PointsBackAlongTheEdge()
	{
		// Act
		var label = Label(CreateForeignKey(principal: "Orders"));

		// Assert
		await Assert.That(label).IsEqualTo("← Orders");
	}

	[Test]
	public async Task Label_PrincipalNavigationOnlyFromThePrincipal_PointsAlongTheEdge()
	{
		// Act
		var label = Label(CreateForeignKey(principal: "Orders"), EfRelationshipDirection.PrincipalToDependent);

		// Assert
		await Assert.That(label).IsEqualTo("Orders →");
	}

	[Test]
	public async Task Label_NoNavigation_UsesTheConstraintName()
	{
		// Act
		var label = Label(CreateForeignKey(constraint: "FK_Orders_Customers"));

		// Assert
		await Assert.That(label).IsEqualTo("FK_Orders_Customers");
	}

	[Test]
	public async Task Label_NothingKnown_FallsBackToTheColumns()
	{
		// Act
		var label = Label(CreateForeignKey());

		// Assert
		await Assert.That(label).IsEqualTo("CustomerId");
	}
}
