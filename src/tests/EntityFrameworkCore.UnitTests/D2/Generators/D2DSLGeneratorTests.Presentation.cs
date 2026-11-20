using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;

/// <summary>What d2 makes of styles, layout direction and descriptions, built from an EF model.</summary>
public sealed partial class D2DSLGeneratorTests
{
	// Helpers

	/// <summary>Customers ← Orders, one context, with a description on Orders.</summary>
	static EfModel CreateEfModel() =>
		new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "SalesDbContext",
					TypeName = "Shop.SalesDbContext",
					Tables =
					[
						new EfTable
						{
							Name = "Customers",
							ClrTypeName = "Shop.Customer",
							Columns =
							[
								new EfColumn
								{
									Name = "Id",
									StoreType = "int",
									IsPrimaryKey = true,
								},
							],
						},
						new EfTable
						{
							Name = "Orders",
							ClrTypeName = "Shop.Order",
							Description = "Placed orders",
							Columns =
							[
								new EfColumn
								{
									Name = "Id",
									StoreType = "int",
									IsPrimaryKey = true,
								},
								new EfColumn
								{
									Name = "CustomerId",
									StoreType = "int",
									IsForeignKey = true,
								},
							],
							ForeignKeys =
							[
								new EfForeignKey
								{
									Columns = ["CustomerId"],
									PrincipalTable = "Customers",
									PrincipalColumns = ["Id"],
								},
							],
						},
					],
				},
			],
		};

	static string GenerateFromEf(AspireC4EfDiagramOptions options) =>
		D2DSLGenerator.Generate(D2ModelBuilder.Build(CreateEfModel(), options), options).ReplaceLineEndings("\n");

	// Tests

	[Test]
	public async Task Generate_SidewaysLayout_EmitsTheDirection()
	{
		// Arrange
		var options = CreateOptions().WithLayout(ErdLayoutDirection.Right);

		// Act
		var dsl = GenerateFromEf(options);

		// Assert
		await Assert.That(dsl).Contains("\ndirection: right\n");
	}

	[Test]
	public async Task Generate_DefaultLayout_EmitsNoDirection()
	{
		// d2 lays out downward on its own, so an unconfigured diagram stays as it was.
		// Act
		var dsl = GenerateFromEf(CreateOptions());

		// Assert
		await Assert.That(dsl).DoesNotContain("direction:");
	}

	[Test]
	public async Task Generate_StyledTable_EmitsAStyleBlock()
	{
		// Arrange
		var options = CreateOptions()
			.WithTableStyle("Orders", s =>
				s.WithAccent("#dc2626", "#f87171")
					.WithBackground("#fef2f2")
					.WithForeground("#ffffff")
					.WithLineStyle(ErdLineStyle.Dotted)
					.WithItalic()
			);

		// Act
		var dsl = GenerateFromEf(options);

		// Assert
		await Assert
			.That(dsl)
			.Contains(
				"\tstyle: {\n\t\tfill: \"#dc2626\"\n\t\tstroke: \"#fef2f2\"\n\t\tfont-color: \"#ffffff\"\n\t\tstroke-dash: 2\n\t\titalic: true\n\t}\n"
			);
	}

	[Test]
	public async Task Generate_StyledRelationship_StylesTheEdge()
	{
		// Arrange
		var options = CreateOptions()
			.WithStyle(r =>
				r.ForTable("Customers").ForRelationships().WithAccent("#16a34a").WithLineStyle(ErdLineStyle.Dashed)
			);

		// Act
		var dsl = GenerateFromEf(options);

		// Assert — an edge has no header to fill, so only its line takes the accent.
		await Assert.That(dsl).Contains("Orders.CustomerId -> Customers.Id: ");
		await Assert.That(dsl).Contains(" {style.stroke: \"#16a34a\"; style.stroke-dash: 5}");
	}

	[Test]
	public async Task Generate_DescriptionsShown_BecomeTheTableTooltip()
	{
		// Arrange
		var options = CreateOptions()
			.WithDescriptions()
			.WithTableStyle("Orders", s => s.WithTooltip("Partitioned by month"));

		// Act
		var dsl = GenerateFromEf(options);

		// Assert
		await Assert.That(dsl).Contains("\ttooltip: \"Placed orders — Partitioned by month\"\n");
	}

	[Test]
	public async Task Generate_DescriptionsOff_EmitNoTooltip()
	{
		// Act
		var dsl = GenerateFromEf(CreateOptions());

		// Assert
		await Assert.That(dsl).DoesNotContain("tooltip:");
	}
}
