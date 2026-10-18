using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;

public sealed class D2ModelBuilderTests
{
	// Helpers

	static AspireC4EfDiagramOptions CreateOptions(
		bool showNullability = true,
		bool groupBySchema = true,
		bool groupByContext = true
	) =>
		new()
		{
			ShowNullability = showNullability,
			GroupBySchema = groupBySchema,
			GroupByContext = groupByContext,
		};

	static EfColumn CreateColumn(
		string name = "Id",
		string storeType = "int",
		bool isNullable = false,
		bool isPrimaryKey = false,
		bool isUnique = false,
		bool isForeignKey = false,
		int? maxLength = null
	) =>
		new()
		{
			Name = name,
			StoreType = storeType,
			IsNullable = isNullable,
			IsPrimaryKey = isPrimaryKey,
			IsUnique = isUnique,
			IsForeignKey = isForeignKey,
			MaxLength = maxLength,
		};

	static EfTable CreateTable(
		string name = "Users",
		string? schema = null,
		IReadOnlyList<EfColumn>? columns = null,
		IReadOnlyList<EfForeignKey>? foreignKeys = null
	) =>
		new()
		{
			Name = name,
			ClrTypeName = $"Sample.{name}",
			Schema = schema,
			Columns = columns ?? [CreateColumn(isPrimaryKey: true)],
			ForeignKeys = foreignKeys ?? [],
		};

	static EfContext CreateContext(
		string name = "AppDbContext",
		string? defaultSchema = null,
		IReadOnlyList<EfTable>? tables = null
	) =>
		new()
		{
			Name = name,
			TypeName = $"Sample.{name}",
			DefaultSchema = defaultSchema,
			Tables = tables ?? [CreateTable()],
		};

	static EfModel CreateModel(IReadOnlyList<EfContext>? contexts = null) =>
		new() { Contexts = contexts ?? [CreateContext()] };

	// Tests

	[Test]
	public async Task Build_NoContexts_ReturnsEmptyModel()
	{
		// Arrange
		EfModel model = new() { Contexts = [] };

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables).IsEmpty();
		await Assert.That(result.Edges).IsEmpty();
	}

	[Test]
	public async Task Build_Table_MapsNameAndLabel()
	{
		// Arrange
		var model = CreateModel([CreateContext(tables: [CreateTable("Order Items")])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables.Count).IsEqualTo(1);
		await Assert.That(result.Tables[0].Id).IsEqualTo("Order_Items");
		await Assert.That(result.Tables[0].Label).IsEqualTo("Order Items");
	}

	[Test]
	public async Task Build_PrimaryKeyColumn_GetsPrimaryKeyConstraint()
	{
		// Arrange
		var table = CreateTable(columns: [CreateColumn("Id", isPrimaryKey: true)]);
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Columns[0].Constraints).Contains(D2Constraint.PrimaryKey);
	}

	[Test]
	public async Task Build_PrimaryKeyColumn_DoesNotAlsoEmitUnique()
	{
		// A primary key is implicitly unique; repeating it adds noise to every row.
		// Arrange
		var table = CreateTable(columns: [CreateColumn("Id", isPrimaryKey: true, isUnique: true)]);
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Columns[0].Constraints).DoesNotContain(D2Constraint.Unique);
	}

	[Test]
	public async Task Build_NullableColumn_AppendsNullabilityMarker()
	{
		// Arrange
		var table = CreateTable(columns: [CreateColumn("Email", "varchar", isNullable: true)]);
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Columns[0].Type).IsEqualTo("varchar?");
	}

	[Test]
	public async Task Build_NullableColumn_WithNullabilityDisabled_OmitsMarker()
	{
		// Arrange
		var table = CreateTable(columns: [CreateColumn("Email", "varchar", isNullable: true)]);
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions(showNullability: false));

		// Assert
		await Assert.That(result.Tables[0].Columns[0].Type).IsEqualTo("varchar");
	}

	[Test]
	public async Task Build_ColumnWithMaxLength_AppendsLengthToStoreType()
	{
		// Arrange
		var table = CreateTable(columns: [CreateColumn("Email", "varchar", maxLength: 256)]);
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Columns[0].Type).IsEqualTo("varchar(256)");
	}

	[Test]
	public async Task Build_ColumnWithExplicitLengthInStoreType_DoesNotDoubleUpLength()
	{
		// Arrange
		var table = CreateTable(columns: [CreateColumn("Email", "varchar(64)", maxLength: 256)]);
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Columns[0].Type).IsEqualTo("varchar(64)");
	}

	[Test]
	public async Task Build_ForeignKey_ProducesColumnLevelEdge()
	{
		// Arrange
		EfForeignKey foreignKey = new()
		{
			Columns = ["UserId"],
			PrincipalTable = "Users",
			PrincipalColumns = ["Id"],
		};
		var orders = CreateTable(
			"Orders",
			columns: [CreateColumn("UserId", isForeignKey: true)],
			foreignKeys: [foreignKey]
		);
		var model = CreateModel([CreateContext(tables: [orders, CreateTable("Users")])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Edges.Count).IsEqualTo(1);
		await Assert.That(result.Edges[0].SourceTable).IsEqualTo("Orders");
		await Assert.That(result.Edges[0].SourceColumn).IsEqualTo("UserId");
		await Assert.That(result.Edges[0].TargetTable).IsEqualTo("Users");
		await Assert.That(result.Edges[0].TargetColumn).IsEqualTo("Id");
	}

	[Test]
	public async Task Build_ForeignKeyToUnknownTable_IsSkipped()
	{
		// A principal entity outside the analysed set would otherwise emit a dangling reference.
		// Arrange
		EfForeignKey foreignKey = new()
		{
			Columns = ["TenantId"],
			PrincipalTable = "Tenants",
			PrincipalColumns = ["Id"],
		};
		var orders = CreateTable("Orders", foreignKeys: [foreignKey]);
		var model = CreateModel([CreateContext(tables: [orders])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Edges).IsEmpty();
	}

	[Test]
	public async Task Build_SchemaGrouping_PlacesTableInContainer()
	{
		// Arrange
		var table = CreateTable("Orders", schema: "sales");
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions(groupByContext: false));

		// Assert
		await Assert.That(result.Tables[0].Container).IsEqualTo("sales");
	}

	[Test]
	public async Task Build_SchemaGroupingDisabled_LeavesTableUngrouped()
	{
		// Arrange
		var table = CreateTable("Orders", schema: "sales");
		var model = CreateModel([CreateContext(tables: [table])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions(groupBySchema: false, groupByContext: false));

		// Assert
		await Assert.That(result.Tables[0].Container).IsNull();
	}

	[Test]
	public async Task Build_SingleContext_DoesNotGroupByContext()
	{
		// Grouping a lone context adds a container with nothing to distinguish it from.
		// Arrange
		var model = CreateModel([CreateContext(tables: [CreateTable("Orders")])]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Container).IsNull();
	}

	[Test]
	public async Task Build_MultipleContexts_GroupsEachByContextName()
	{
		// Arrange
		var first = CreateContext("AppDbContext", tables: [CreateTable("Orders")]);
		var second = CreateContext("AuditDbContext", tables: [CreateTable("Events")]);
		var model = CreateModel([first, second]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Container).IsEqualTo("AppDbContext");
		await Assert.That(result.Tables[1].Container).IsEqualTo("AuditDbContext");
	}

	[Test]
	public async Task Build_DuplicateTableNamesInSameContainer_GetUniqueIdentifiers()
	{
		// Arrange
		var context = CreateContext(tables: [CreateTable("Orders"), CreateTable("Orders")]);
		var model = CreateModel([context]);

		// Act
		var result = D2ModelBuilder.Build(model, CreateOptions());

		// Assert
		await Assert.That(result.Tables[0].Id).IsEqualTo("Orders");
		await Assert.That(result.Tables[1].Id).IsEqualTo("Orders_2");
	}

	[Test]
	public async Task Build_NullModel_Throws()
	{
		// Arrange
		var options = CreateOptions();

		// Act & Assert
		await Assert.That(() => D2ModelBuilder.Build(null!, options)).Throws<ArgumentNullException>();
	}
}
