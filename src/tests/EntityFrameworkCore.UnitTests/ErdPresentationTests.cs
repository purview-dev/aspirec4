using System.Text.Json.Nodes;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// How style rules, style callbacks and description sources resolve onto a model — the one place every
/// renderer reads styling from.
/// </summary>
public sealed class ErdPresentationTests
{
	// Helpers

	static EfColumn CreateColumn(
		string name,
		string storeType = "int",
		bool isPrimaryKey = false,
		bool isForeignKey = false,
		string? domainTypeName = null,
		EfDomainTypeKind? domainTypeKind = null,
		string? comment = null,
		string? description = null,
		string? documentation = null
	) =>
		new()
		{
			Name = name,
			StoreType = storeType,
			IsPrimaryKey = isPrimaryKey,
			IsForeignKey = isForeignKey,
			DomainTypeName = domainTypeName,
			DomainTypeKind = domainTypeKind,
			Comment = comment,
			Description = description,
			Documentation = documentation,
		};

	/// <summary>
	/// Sales: Customers ← Orders ← OrderLines, with Orders.Total a decimal and Orders.Status an enum.
	/// Audit: AuditEntries, unrelated to the rest.
	/// </summary>
	static EfModel CreateModel(
		string? ordersDescription = null,
		string? ordersDocumentation = null,
		string? ordersComment = null
	) =>
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
							Columns = [CreateColumn("Id", isPrimaryKey: true), CreateColumn("Email", "varchar")],
						},
						new EfTable
						{
							Name = "Orders",
							ClrTypeName = "Shop.Order",
							Description = ordersDescription,
							Documentation = ordersDocumentation,
							Comment = ordersComment,
							Columns =
							[
								CreateColumn("Id", isPrimaryKey: true),
								CreateColumn("CustomerId", isForeignKey: true),
								CreateColumn("Total", "decimal(18,2)", documentation: "Including tax."),
								CreateColumn(
									"Status",
									domainTypeName: "OrderStatus",
									domainTypeKind: EfDomainTypeKind.Enum,
									comment: "Lifecycle state"
								),
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
						new EfTable
						{
							Name = "OrderLines",
							ClrTypeName = "Shop.OrderLine",
							Columns =
							[
								CreateColumn("Id", isPrimaryKey: true),
								CreateColumn("OrderId", isForeignKey: true),
							],
							ForeignKeys =
							[
								new EfForeignKey
								{
									Columns = ["OrderId"],
									PrincipalTable = "Orders",
									PrincipalColumns = ["Id"],
								},
							],
						},
					],
				},
				new EfContext
				{
					Name = "AuditDbContext",
					TypeName = "Shop.AuditDbContext",
					Tables =
					[
						new EfTable
						{
							Name = "AuditEntries",
							ClrTypeName = "Shop.AuditEntry",
							Columns = [CreateColumn("Id", isPrimaryKey: true)],
						},
					],
				},
			],
		};

	static EfTable Orders(EfModel model) => model.Contexts[0].Tables.Single(t => t.Name == "Orders");

	static ErdPresentation Build(Action<AspireC4EfDiagramOptions> configure, EfModel? model = null)
	{
		AspireC4EfDiagramOptions options = new();
		configure(options);

		return ErdPresentation.Build(model ?? CreateModel(), options);
	}

	// Rule targets

	[Test]
	public async Task Build_TableRule_StylesOnlyTheMatchingTables()
	{
		// Act
		var presentation = Build(o => o.WithTableStyle("Order*", s => s.WithAccent("#b45309")));

		// Assert
		await Assert.That(presentation.TableStyle("Orders")?.Accent?.Light).IsEqualTo("#b45309");
		await Assert.That(presentation.TableStyle("OrderLines")?.Accent?.Light).IsEqualTo("#b45309");
		await Assert.That(presentation.TableStyle("Customers")).IsNull();
	}

	[Test]
	public async Task Build_TableRule_MatchesTheEntityTypeName()
	{
		// Act
		var presentation = Build(o => o.WithTableStyle("Customer", s => s.WithBold()));

		// Assert
		await Assert.That(presentation.TableStyle("Customers")?.Bold).IsTrue();
	}

	[Test]
	public async Task Build_ColumnRule_StylesTheMatchingColumnsOfEveryTable()
	{
		// Act
		var presentation = Build(o => o.WithColumnStyle("*Id", s => s.WithForeground("#64748b")));

		// Assert
		await Assert.That(presentation.ColumnStyle("Orders", "CustomerId")).IsNotNull();
		await Assert.That(presentation.ColumnStyle("OrderLines", "OrderId")).IsNotNull();
		await Assert.That(presentation.ColumnStyle("Customers", "Id")).IsNotNull();
		await Assert.That(presentation.ColumnStyle("Customers", "Email")).IsNull();
		await Assert.That(presentation.TableStyle("Orders")).IsNull();
	}

	[Test]
	[Arguments("decimal")]
	[Arguments("decimal(18,2)")]
	public async Task Build_TypeRule_MatchesTheStoreType(string pattern)
	{
		// Act
		var presentation = Build(o => o.WithTypeStyle(pattern, s => s.WithAccent("#16a34a")));

		// Assert
		await Assert.That(presentation.ColumnStyle("Orders", "Total")).IsNotNull();
		await Assert.That(presentation.ColumnStyle("Orders", "Id")).IsNull();
	}

	[Test]
	[Arguments("OrderStatus")]
	[Arguments("enum")]
	[Arguments("Enum")]
	public async Task Build_TypeRule_MatchesTheDomainTypeOrItsKind(string pattern)
	{
		// Act
		var presentation = Build(o => o.WithTypeStyle(pattern, s => s.WithItalic()));

		// Assert
		await Assert.That(presentation.ColumnStyle("Orders", "Status")?.Italic).IsTrue();
		await Assert.That(presentation.ColumnStyle("Orders", "Total")).IsNull();
	}

	[Test]
	public async Task Build_CombinedTableAndColumnRule_StylesOnlyThatTablesColumns()
	{
		// Act
		var presentation = Build(o => o.WithColumnStyle("Id", s => s.WithBadge("KEY"), table: "Orders"));

		// Assert
		await Assert.That(presentation.ColumnStyle("Orders", "Id")?.Badge).IsEqualTo("KEY");
		await Assert.That(presentation.ColumnStyle("Customers", "Id")).IsNull();
	}

	[Test]
	public async Task Build_ContextPattern_NarrowsTheRule()
	{
		// Act
		var presentation = Build(o => o.WithStyle(r => r.ForContext("Audit*").ForColumn("Id").WithBold()));

		// Assert
		await Assert.That(presentation.ColumnStyle("AuditEntries", "Id")?.Bold).IsTrue();
		await Assert.That(presentation.ColumnStyle("Orders", "Id")).IsNull();
	}

	[Test]
	public async Task Build_TableTargetWithColumnPattern_StylesTablesThatHaveTheColumn()
	{
		// Act
		var presentation = Build(o => o.WithStyle(r => r.ForColumn("Status").ForTables().WithBadge("STATEFUL")));

		// Assert
		await Assert.That(presentation.TableStyle("Orders")?.Badge).IsEqualTo("STATEFUL");
		await Assert.That(presentation.TableStyle("Customers")).IsNull();
		await Assert.That(presentation.ColumnStyle("Orders", "Status")).IsNull();
	}

	[Test]
	public async Task Build_RelationshipRule_MatchesEitherEnd()
	{
		// Act
		var presentation = Build(o =>
			o.WithStyle(r => r.ForTable("Customers").ForRelationships().WithLineStyle(ErdLineStyle.Dashed))
		);
		var model = CreateModel();
		var orders = Orders(model);
		var lines = model.Contexts[0].Tables.Single(t => t.Name == "OrderLines");

		// Assert
		await Assert
			.That(presentation.RelationshipStyle(orders, orders.ForeignKeys[0])?.LineStyle)
			.IsEqualTo(ErdLineStyle.Dashed);
		await Assert.That(presentation.RelationshipStyle(lines, lines.ForeignKeys[0])).IsNull();
		await Assert.That(presentation.TableStyle("Customers")).IsNull();
	}

	[Test]
	public async Task Build_RelationshipRuleWithColumn_MatchesTheForeignKeyColumn()
	{
		// Act
		var presentation = Build(o =>
			o.WithStyle(r => r.ForColumn("OrderId").ForRelationships().WithAccent("#dc2626"))
		);
		var lines = CreateModel().Contexts[0].Tables.Single(t => t.Name == "OrderLines");

		// Assert
		await Assert
			.That(presentation.RelationshipStyle(lines, lines.ForeignKeys[0])?.Accent?.Light)
			.IsEqualTo("#dc2626");
	}

	// Precedence

	[Test]
	public async Task Build_SeveralMatchingRules_LaterOverridesOnlyWhatItSets()
	{
		// Act
		var presentation = Build(o =>
			o.WithTableStyle("*", s => s.WithAccent("#111111").WithBold())
				.WithTableStyle("Orders", s => s.WithAccent("#222222", "#333333"))
		);

		// Assert
		var orders = presentation.TableStyle("Orders")!;
		await Assert.That(orders.Accent!.Light).IsEqualTo("#222222");
		await Assert.That(orders.Accent.Dark).IsEqualTo("#333333");
		await Assert.That(orders.Bold).IsTrue();
		await Assert.That(presentation.TableStyle("Customers")!.Accent!.Light).IsEqualTo("#111111");
	}

	// Callbacks

	[Test]
	public async Task Build_TableCallback_SeesTheRulesResultAndCanChangeIt()
	{
		// Arrange
		string? seen = null;

		// Act
		var presentation = Build(o =>
			o.WithTableStyle("Orders", s => s.WithAccent("#111111"))
				.OnTableStyle(ctx =>
				{
					if (ctx.Table.Name != "Orders")
					{
						return;
					}

					seen = ctx.Style.Accent?.Light;
					ctx.Style.WithBadge($"{ctx.Table.Columns.Count} cols");
				})
		);

		// Assert
		await Assert.That(seen).IsEqualTo("#111111");
		await Assert.That(presentation.TableStyle("Orders")?.Badge).IsEqualTo("4 cols");
	}

	[Test]
	public async Task Build_ColumnCallback_ReactsToTheModel()
	{
		// The point of a callback: style computed from the model, which follows it as it changes.
		// Act
		var presentation = Build(o =>
			o.OnColumnStyle(ctx =>
			{
				if (ctx.Column.IsForeignKey)
				{
					ctx.Style.WithAccent("#2563eb");
				}
			})
		);

		// Assert
		await Assert.That(presentation.ColumnStyle("Orders", "CustomerId")).IsNotNull();
		await Assert.That(presentation.ColumnStyle("Orders", "Total")).IsNull();
	}

	[Test]
	public async Task Build_RelationshipCallback_IsGivenBothEnds()
	{
		// Arrange
		List<string> seen = [];

		// Act
		_ = Build(o => o.OnRelationshipStyle(ctx => seen.Add($"{ctx.Dependent.Name}->{ctx.Principal.Name}")));

		// Assert
		await Assert.That(seen).IsEquivalentTo(["Orders->Customers", "OrderLines->Orders"]);
	}

	[Test]
	public async Task Build_CallbackThrows_NamesTheElement()
	{
		// Act & Assert
		var exception = await Assert
			.That(() => Build(o => o.OnColumnStyle(ctx => throw new FormatException("bad"))))
			.Throws<InvalidOperationException>();

		await Assert.That(exception!.Message).Contains("Customers.Id");
		await Assert.That(exception.InnerException).IsTypeOf<FormatException>();
	}

	[Test]
	public async Task Build_CallbackChangingStyle_DoesNotChangeTheRule()
	{
		// Arrange
		var rule = new ErdStyleRule().ForTable("*").WithAccent("#111111");

		// Act
		_ = Build(o => o.WithStyle(rule).OnTableStyle(ctx => ctx.Style.Accent!.Light = "#999999"));

		// Assert
		await Assert.That(rule.Style.Accent!.Light).IsEqualTo("#111111");
	}

	// Descriptions

	[Test]
	public async Task Build_EverySource_PrefersAttributeThenDocumentationThenComment()
	{
		// Act
		var all = Build(_ => { }, CreateModel("from attribute", "from docs", "from comment"));
		var docs = Build(_ => { }, CreateModel(null, "from docs", "from comment"));
		var comment = Build(_ => { }, CreateModel(null, null, "from comment"));

		// Assert
		await Assert.That(all.TableDescription("Orders")).IsEqualTo("from attribute");
		await Assert.That(docs.TableDescription("Orders")).IsEqualTo("from docs");
		await Assert.That(comment.TableDescription("Orders")).IsEqualTo("from comment");
	}

	[Test]
	public async Task Build_RestrictedSources_SkipTheOthers()
	{
		// Act
		var presentation = Build(
			o => o.WithDescriptions(ErdDescriptionSources.Comments),
			CreateModel("from attribute", "from docs", "from comment")
		);

		// Assert
		await Assert.That(presentation.TableDescription("Orders")).IsEqualTo("from comment");
		await Assert.That(presentation.ColumnDescription("Orders", "Total")).IsNull();
		await Assert.That(presentation.ColumnDescription("Orders", "Status")).IsEqualTo("Lifecycle state");
	}

	[Test]
	public async Task Build_ColumnDocumentation_IsAColumnDescription()
	{
		// Act
		var presentation = Build(_ => { });

		// Assert
		await Assert.That(presentation.ColumnDescription("Orders", "Total")).IsEqualTo("Including tax.");
	}

	[Test]
	public async Task Build_NothingToStyleOrDescribe_IsEmpty()
	{
		// Act
		var presentation = Build(o => o.DescriptionSources = ErdDescriptionSources.None);

		// Assert
		await Assert.That(presentation).IsSameReferenceAs(ErdPresentation.Empty);
		await Assert.That(presentation.HasStyles).IsFalse();
	}

	// Serialisation

	[Test]
	public async Task ToJson_Defaults_DescribeTheViewerAndLayout()
	{
		// Act
		var json = ErdPresentation.Empty.ToJson(new AspireC4EfDiagramOptions());

		// Assert
		await Assert.That(json["viewer"]!["theme"]!.GetValue<string>()).IsEqualTo("auto");
		await Assert.That(json["viewer"]!["animation"]!.GetValue<string>()).IsEqualTo("normal");
		await Assert.That(json["viewer"]!["descriptions"]!.GetValue<bool>()).IsFalse();
		await Assert.That(json["viewer"]!["tooltips"]!.GetValue<bool>()).IsTrue();
		await Assert.That(json["layout"]!["direction"]!.GetValue<string>()).IsEqualTo("DOWN");
		await Assert.That(json["layout"]!["nodeSpacing"]!.GetValue<int>()).IsEqualTo(72);
		await Assert.That(json["layout"]!["edgeStyle"]!.GetValue<string>()).IsEqualTo("default");
		await Assert.That(json.ContainsKey("styles")).IsFalse();
		await Assert.That(json.ContainsKey("palette")).IsFalse();
	}

	[Test]
	public async Task ToJson_Configured_CarriesEveryChoice()
	{
		// Arrange
		var options = new AspireC4EfDiagramOptions()
			.WithLayout(ErdLayoutDirection.Right, nodeSpacing: 40, layerSpacing: 200, contextSpacing: 90)
			.WithEdgeStyle(ErdEdgeStyle.SmoothStep)
			.WithAnimation(ErdEdgeAnimation.Fast, transitions: false)
			.WithViewerDefaults(ErdViewerTheme.Dark, ErdViewerBackground.None, ErdTableDisplay.Names)
			.WithLegend()
			.WithDescriptions()
			.WithContextPalette(new ErdColour("#0f766e", "#2dd4bf"));

		// Act
		var json = ErdPresentation.Empty.ToJson(options);

		// Assert
		await Assert.That(json["viewer"]!["theme"]!.GetValue<string>()).IsEqualTo("dark");
		await Assert.That(json["viewer"]!["background"]!.GetValue<string>()).IsEqualTo("none");
		await Assert.That(json["viewer"]!["tables"]!.GetValue<string>()).IsEqualTo("names");
		await Assert.That(json["viewer"]!["animation"]!.GetValue<string>()).IsEqualTo("fast");
		await Assert.That(json["viewer"]!["transitions"]!.GetValue<bool>()).IsFalse();
		await Assert.That(json["viewer"]!["legend"]!.GetValue<bool>()).IsTrue();
		await Assert.That(json["viewer"]!["descriptions"]!.GetValue<bool>()).IsTrue();
		await Assert.That(json["layout"]!["direction"]!.GetValue<string>()).IsEqualTo("RIGHT");
		await Assert.That(json["layout"]!["layerSpacing"]!.GetValue<int>()).IsEqualTo(200);
		await Assert.That(json["layout"]!["edgeStyle"]!.GetValue<string>()).IsEqualTo("smoothstep");
		await Assert.That(json["palette"]![0]!["dark"]!.GetValue<string>()).IsEqualTo("#2dd4bf");
	}

	[Test]
	public async Task ToJson_Styles_FillEachThemeFromTheOther()
	{
		// Arrange
		AspireC4EfDiagramOptions options = new();
		options.WithTableStyle("Orders", s =>
			s.WithAccent("#b45309").WithLineStyle(ErdLineStyle.Dotted).WithBadge("PII")
		);

		// Act
		var json = ErdPresentation.Build(CreateModel(), options).ToJson(options);
		var orders = json["styles"]!["tables"]!["Orders"]!.AsObject();

		// Assert
		await Assert.That(orders["accent"]!["light"]!.GetValue<string>()).IsEqualTo("#b45309");
		await Assert.That(orders["accent"]!["dark"]!.GetValue<string>()).IsEqualTo("#b45309");
		await Assert.That(orders["lineStyle"]!.GetValue<string>()).IsEqualTo("dotted");
		await Assert.That(orders["badge"]!.GetValue<string>()).IsEqualTo("PII");
		await Assert.That(orders.ContainsKey("bold")).IsFalse();
	}

	[Test]
	public async Task ToJson_RelationshipStyle_IsKeyedAsTheViewerKeysItsEdge()
	{
		// Arrange
		AspireC4EfDiagramOptions options = new();
		options.WithStyle(r => r.ForTable("Customers").ForRelationships().WithAccent("#dc2626"));

		// Act
		var json = ErdPresentation.Build(CreateModel(), options).ToJson(options);

		// Assert — app.js builds the id as `${table}.${columns.join("_")}->${principal}`.
		await Assert
			.That(json["styles"]!["relationships"]!.AsObject().ContainsKey("Orders.CustomerId->Customers"))
			.IsTrue();
	}

	[Test]
	public async Task ToJson_Descriptions_AreGroupedByTable()
	{
		// Arrange
		AspireC4EfDiagramOptions options = new();

		// Act
		var json = ErdPresentation.Build(CreateModel(ordersDescription: "Placed orders"), options).ToJson(options);

		// Assert
		await Assert.That(json["descriptions"]!["tables"]!["Orders"]!.GetValue<string>()).IsEqualTo("Placed orders");
		await Assert
			.That(json["descriptions"]!["columns"]!["Orders"]!["Total"]!.GetValue<string>())
			.IsEqualTo("Including tax.");
	}

	[Test]
	public async Task ToJson_SameInput_IsByteForByteStable()
	{
		// The diagram is rewritten only when its bytes change, so serialisation order must not drift.
		// Arrange
		AspireC4EfDiagramOptions options = new();
		options.WithTableStyle("*", s => s.WithBold()).WithColumnStyle("*", s => s.WithItalic());

		// Act
		var first = ErdPresentation.Build(CreateModel(), options).ToJson(options).ToJsonString();
		var second = ErdPresentation.Build(CreateModel(), options).ToJson(options).ToJsonString();

		// Assert
		await Assert.That(second).IsEqualTo(first);
		await Assert.That(JsonNode.Parse(first)).IsNotNull();
	}
}
