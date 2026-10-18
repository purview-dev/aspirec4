using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers how a column shows its domain type — a value object or an enum — beside the real database type.
/// </summary>
public sealed class EfColumnTypeDisplayTests
{
	// Helpers

	static EfColumn CreateColumn(
		string? domainType,
		EfDomainTypeKind? kind = null,
		string storeType = "guid",
		int? maxLength = null
	) =>
		new()
		{
			Name = "Id",
			StoreType = storeType,
			DomainTypeName = domainType,
			DomainTypeKind = kind,
			MaxLength = maxLength,
		};

	static EfColumn CreateValueObject(string storeType = "guid", int? maxLength = null) =>
		CreateColumn("AssetId", EfDomainTypeKind.ValueObject, storeType, maxLength);

	static EfColumn CreateEnum() => CreateColumn("ActorType", EfDomainTypeKind.Enum, "int");

	static AspireC4EfDiagramOptions CreateOptions(DomainTypeDisplay display) => new() { DomainTypeDisplay = display };

	// Tests

	[Test]
	public async Task Default_IsDomainTypeFirst()
	{
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.DomainTypeDisplay).IsEqualTo(DomainTypeDisplay.DomainTypeFirst);
	}

	[Test]
	public async Task Compose_WithoutADomainType_IsTheDatabaseType()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(
			CreateColumn(null),
			CreateOptions(DomainTypeDisplay.DomainTypeFirst)
		);

		// Assert
		await Assert.That(composed).IsEqualTo("guid");
	}

	[Test]
	public async Task Compose_DomainTypeFirst_PutsTheDomainTypeFirst()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(
			CreateValueObject(),
			CreateOptions(DomainTypeDisplay.DomainTypeFirst)
		);

		// Assert
		await Assert.That(composed).IsEqualTo("AssetId (guid)");
	}

	[Test]
	public async Task Compose_DatabaseFirst_PutsTheDatabaseTypeFirst()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(CreateValueObject(), CreateOptions(DomainTypeDisplay.DatabaseFirst));

		// Assert
		await Assert.That(composed).IsEqualTo("guid (AssetId)");
	}

	[Test]
	public async Task Compose_DomainTypeOnly_ShowsOnlyTheDomainType()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(
			CreateValueObject(),
			CreateOptions(DomainTypeDisplay.DomainTypeOnly)
		);

		// Assert
		await Assert.That(composed).IsEqualTo("AssetId");
	}

	[Test]
	public async Task Compose_DatabaseOnly_ShowsOnlyTheDatabaseType()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(CreateValueObject(), CreateOptions(DomainTypeDisplay.DatabaseOnly));

		// Assert
		await Assert.That(composed).IsEqualTo("guid");
	}

	[Test]
	public async Task Compose_Enum_ShowsTheTypeAndTheFactItIsAnEnum()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(CreateEnum(), CreateOptions(DomainTypeDisplay.DomainTypeFirst));

		// Assert
		await Assert.That(composed).IsEqualTo("ActorType (int, enum)");
	}

	[Test]
	public async Task Compose_EnumDatabaseFirst_MarksTheDomainType()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(CreateEnum(), CreateOptions(DomainTypeDisplay.DatabaseFirst));

		// Assert
		await Assert.That(composed).IsEqualTo("int (ActorType, enum)");
	}

	[Test]
	public async Task Compose_EnumDomainTypeOnly_KeepsTheMarker()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(CreateEnum(), CreateOptions(DomainTypeDisplay.DomainTypeOnly));

		// Assert
		await Assert.That(composed).IsEqualTo("ActorType (enum)");
	}

	[Test]
	public async Task Compose_EnumDatabaseOnly_KeepsTheMarker()
	{
		// Act
		var composed = EfColumnTypeDisplay.Compose(CreateEnum(), CreateOptions(DomainTypeDisplay.DatabaseOnly));

		// Assert
		await Assert.That(composed).IsEqualTo("int (enum)");
	}

	[Test]
	public async Task Annotation_DomainTypeOnly_KeepsTheDatabaseTypeAvailable()
	{
		// The database definition must stay reachable even when the domain type is the headline.
		// Act
		var annotation = EfColumnTypeDisplay.Annotation(
			CreateValueObject("varchar", 100),
			CreateOptions(DomainTypeDisplay.DomainTypeOnly)
		);

		// Assert
		await Assert.That(annotation).IsEqualTo("varchar(100)");
	}

	[Test]
	public async Task Annotation_EnumDomainTypeFirst_CarriesTheMarker()
	{
		// Act
		var annotation = EfColumnTypeDisplay.Annotation(CreateEnum(), CreateOptions(DomainTypeDisplay.DomainTypeFirst));

		// Assert
		await Assert.That(annotation).IsEqualTo("int, enum");
	}

	[Test]
	public async Task Annotation_DatabaseOnly_IsAbsent()
	{
		// Act
		var annotation = EfColumnTypeDisplay.Annotation(
			CreateValueObject(),
			CreateOptions(DomainTypeDisplay.DatabaseOnly)
		);

		// Assert
		await Assert.That(annotation).IsNull();
	}

	[Test]
	public async Task DatabaseType_IncludesTheConfiguredLength()
	{
		// Act
		var databaseType = EfColumnTypeDisplay.DatabaseType(CreateValueObject("varchar", 100));

		// Assert
		await Assert.That(databaseType).IsEqualTo("varchar(100)");
	}
}
