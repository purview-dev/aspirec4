using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Dbml;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Mermaid;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Verifies that each renderer honours <see cref="DomainTypeDisplay"/> and never drops the database
/// definition.
/// </summary>
public sealed class EfDomainTypeRenderingTests
{
	// Helpers

	/// <summary>A table with a value-object key, an enum column and a plain, length-carrying column.</summary>
	static EfModel CreateModel() =>
		new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "AppDbContext",
					TypeName = "Sample.AppDbContext",
					Tables =
					[
						new EfTable
						{
							Name = "Assets",
							ClrTypeName = "Sample.Asset",
							Columns =
							[
								new EfColumn
								{
									Name = "Id",
									StoreType = "guid",
									DomainTypeName = "AssetId",
									DomainTypeKind = EfDomainTypeKind.ValueObject,
									IsPrimaryKey = true,
								},
								new EfColumn
								{
									Name = "CreatedByType",
									StoreType = "int",
									DomainTypeName = "ActorType",
									DomainTypeKind = EfDomainTypeKind.Enum,
								},
								new EfColumn
								{
									Name = "Name",
									StoreType = "varchar",
									MaxLength = 100,
									IsNullable = true,
								},
							],
						},
					],
				},
			],
		};

	static AspireC4EfDiagramOptions CreateOptions(DomainTypeDisplay display) => new() { DomainTypeDisplay = display };

	// Mermaid

	[Test]
	public async Task Mermaid_DomainTypeFirst_KeepsTheDatabaseTypeInTheComment()
	{
		// Act
		var dsl = MermaidGenerator.Generate(CreateModel(), CreateOptions(DomainTypeDisplay.DomainTypeFirst));

		// Assert
		await Assert.That(dsl).Contains("AssetId Id PK \"guid\"");
		await Assert.That(dsl).Contains("ActorType CreatedByType \"int, enum\"");
	}

	[Test]
	public async Task Mermaid_DatabaseFirst_PutsTheDatabaseTypeInTheType()
	{
		// Act
		var dsl = MermaidGenerator.Generate(CreateModel(), CreateOptions(DomainTypeDisplay.DatabaseFirst));

		// Assert
		await Assert.That(dsl).Contains("guid Id PK \"AssetId\"");
		await Assert.That(dsl).Contains("int CreatedByType \"ActorType, enum\"");
	}

	[Test]
	public async Task Mermaid_DomainTypeOnly_KeepsTheDatabaseTypeInTheComment()
	{
		// Act
		var dsl = MermaidGenerator.Generate(CreateModel(), CreateOptions(DomainTypeDisplay.DomainTypeOnly));

		// Assert
		await Assert.That(dsl).Contains("AssetId Id PK \"guid\"");
		await Assert.That(dsl).Contains("ActorType CreatedByType \"int\"");
	}

	[Test]
	public async Task Mermaid_DatabaseOnly_DropsTheDomainType()
	{
		// Act
		var dsl = MermaidGenerator.Generate(CreateModel(), CreateOptions(DomainTypeDisplay.DatabaseOnly));

		// Assert
		await Assert.That(dsl).Contains("guid Id PK");
		await Assert.That(dsl).DoesNotContain("AssetId");
		await Assert.That(dsl).DoesNotContain("ActorType");
	}

	// DBML

	[Test]
	public async Task Dbml_DomainTypeFirst_KeepsTheDatabaseTypeInTheNote()
	{
		// Act
		var dbml = DbmlGenerator.Generate(CreateModel(), CreateOptions(DomainTypeDisplay.DomainTypeFirst));

		// Assert
		await Assert.That(dbml).Contains("Id AssetId [pk, note: \"guid\"]");
		await Assert.That(dbml).Contains("CreatedByType ActorType [not null, note: \"int, enum\"]");
	}

	[Test]
	public async Task Dbml_DatabaseOnly_DropsTheDomainType()
	{
		// Act
		var dbml = DbmlGenerator.Generate(CreateModel(), CreateOptions(DomainTypeDisplay.DatabaseOnly));

		// Assert
		await Assert.That(dbml).Contains("Id guid [pk]");
		await Assert.That(dbml).Contains("CreatedByType int");
	}

	// D2

	[Test]
	public async Task D2_DomainTypeFirst_ComposesTheDomainTypeWithTheDatabaseType()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(
			D2ModelBuilder.Build(CreateModel(), CreateOptions(DomainTypeDisplay.DomainTypeFirst)),
			CreateOptions(DomainTypeDisplay.DomainTypeFirst)
		);

		// Assert
		await Assert.That(dsl).Contains("Id: AssetId (guid)");
		await Assert.That(dsl).Contains("CreatedByType: ActorType (int, enum)");
	}

	[Test]
	public async Task D2_DatabaseFirst_ComposesTheDatabaseTypeWithTheDomainType()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(
			D2ModelBuilder.Build(CreateModel(), CreateOptions(DomainTypeDisplay.DatabaseFirst)),
			CreateOptions(DomainTypeDisplay.DatabaseFirst)
		);

		// Assert
		await Assert.That(dsl).Contains("Id: guid (AssetId)");
		await Assert.That(dsl).Contains("CreatedByType: int (ActorType, enum)");
	}

	[Test]
	public async Task D2_DomainTypeOnly_ShowsOnlyTheDomainType()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(
			D2ModelBuilder.Build(CreateModel(), CreateOptions(DomainTypeDisplay.DomainTypeOnly)),
			CreateOptions(DomainTypeDisplay.DomainTypeOnly)
		);

		// Assert
		await Assert.That(dsl).Contains("Id: AssetId");
		await Assert.That(dsl).Contains("CreatedByType: ActorType (enum)");
	}

	[Test]
	public async Task D2_DatabaseOnly_ShowsOnlyTheDatabaseType()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(
			D2ModelBuilder.Build(CreateModel(), CreateOptions(DomainTypeDisplay.DatabaseOnly)),
			CreateOptions(DomainTypeDisplay.DatabaseOnly)
		);

		// Assert
		await Assert.That(dsl).Contains("Id: guid");
		await Assert.That(dsl).Contains("CreatedByType: int (enum)");
	}
}
