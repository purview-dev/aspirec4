using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Aspire.Hosting.AspireC4.TestAppHost.ComplexData.Sales;
using Aspire.Hosting.AspireC4.TestAppHost.SimpleData;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Reads the descriptors the generator emitted into the two sample data assemblies and asserts the
/// resulting diagram.
/// </summary>
/// <remarks>
/// The generator's own tests compile against stub EF types. These tests run against assemblies built
/// from the real <c>Microsoft.EntityFrameworkCore</c> API, so they are what proves the generator
/// recognises genuine <c>DbContext</c> and <c>DbSet&lt;T&gt;</c> shapes.
/// </remarks>
public sealed class SampleDataModelTests
{
	// Helpers

	static AspireC4EfDiagramOptions CreateOptions() => new();

	static EfModel LoadSimple() => LoadFor(typeof(BlogDbContext));

	static EfModel LoadComplex() => LoadFor(typeof(SalesDbContext));

	static EfModel LoadFor(Type typeInAssembly)
	{
		var found = EfModelReader.TryRead(typeInAssembly.Assembly.Location, out var descriptor);

		return found ? EfModelLoader.Parse(descriptor) : EfModel.Empty;
	}

	static EfTable Table(EfModel model, string name) =>
		model.Contexts.SelectMany(c => c.Tables).Single(t => t.Name == name);

	static EfColumn Column(EfTable table, string name) => table.Columns.Single(c => c.Name == name);

	// Simple sample

	[Test]
	public async Task SimpleSample_EmitsADescriptorForItsSingleContext()
	{
		// Act
		var model = LoadSimple();

		// Assert
		await Assert.That(model.Contexts.Count).IsEqualTo(1);
		await Assert.That(model.Contexts[0].Name).IsEqualTo("BlogDbContext");
	}

	[Test]
	public async Task SimpleSample_DerivesTableNamesFromDbSetProperties()
	{
		// Act
		var model = LoadSimple();

		// Assert
		await Assert.That(model.Contexts[0].Tables.Select(t => t.Name)).Contains("Blogs");
		await Assert.That(model.Contexts[0].Tables.Select(t => t.Name)).Contains("Posts");
	}

	[Test]
	public async Task SimpleSample_ResolvesConventionalPrimaryKey()
	{
		// Act
		var model = LoadSimple();

		// Assert
		await Assert.That(Column(Table(model, "Blogs"), "Id").IsPrimaryKey).IsTrue();
	}

	[Test]
	public async Task SimpleSample_ResolvesConventionalForeignKey()
	{
		// The FK comes purely from the Blog navigation plus the matching BlogId property.
		// Act
		var model = LoadSimple();
		var posts = Table(model, "Posts");

		// Assert
		await Assert.That(posts.ForeignKeys.Count).IsEqualTo(1);
		await Assert.That(posts.ForeignKeys[0].PrincipalTable).IsEqualTo("Blogs");
		await Assert.That(posts.ForeignKeys[0].Columns[0]).IsEqualTo("BlogId");
		await Assert.That(Column(posts, "BlogId").IsForeignKey).IsTrue();
	}

	[Test]
	public async Task SimpleSample_MarksOptionalPropertiesNullable()
	{
		// Act
		var model = LoadSimple();

		// Assert
		await Assert.That(Column(Table(model, "Posts"), "PublishedAt").IsNullable).IsTrue();
		await Assert.That(Column(Table(model, "Posts"), "Title").IsNullable).IsFalse();
	}

	[Test]
	public async Task SimpleSample_RendersAnErDiagram()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(D2ModelBuilder.Build(LoadSimple(), CreateOptions()), CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("shape: sql_table");
		await Assert.That(dsl).Contains("Posts.BlogId -> Blogs.Id");
	}

	// Complex sample

	[Test]
	public async Task ComplexSample_DiscoversAllThreeContexts()
	{
		// Act
		var model = LoadComplex();

		// Assert
		await Assert.That(model.Contexts.Select(c => c.Name)).Contains("SalesDbContext");
		await Assert.That(model.Contexts.Select(c => c.Name)).Contains("IdentityDbContext");
		await Assert.That(model.Contexts.Select(c => c.Name)).Contains("AuditDbContext");
	}

	[Test]
	public async Task ComplexSample_ReadsDefaultSchemaPerContext()
	{
		// Act
		var model = LoadComplex();

		// Assert
		var schemas = model.Contexts.ToDictionary(c => c.Name, c => c.DefaultSchema);
		await Assert.That(schemas["SalesDbContext"]).IsEqualTo("sales");
		await Assert.That(schemas["IdentityDbContext"]).IsEqualTo("identity");
		await Assert.That(schemas["AuditDbContext"]).IsEqualTo("audit");
	}

	[Test]
	public async Task ComplexSample_AppliesTableAttributeNameAndSchema()
	{
		// Act
		var products = Table(LoadComplex(), "products");

		// Assert
		await Assert.That(products.Schema).IsEqualTo("catalogue");
	}

	[Test]
	public async Task ComplexSample_AppliesColumnAttributeNameAndType()
	{
		// Act
		var products = Table(LoadComplex(), "products");

		// Assert
		await Assert.That(Column(products, "product_id").IsPrimaryKey).IsTrue();
		await Assert.That(Column(products, "unit_price").StoreType).IsEqualTo("decimal(18,4)");
	}

	[Test]
	public async Task ComplexSample_AppliesFluentToTableOverride()
	{
		// Act
		var categories = Table(LoadComplex(), "categories");

		// Assert
		await Assert.That(categories.Schema).IsEqualTo("catalogue");
	}

	[Test]
	public async Task ComplexSample_AppliesFluentColumnNameOverride()
	{
		// Act
		var customers = Table(LoadComplex(), "Customers");

		// Assert
		await Assert.That(customers.Columns.Select(c => c.Name)).Contains("email_address");
		await Assert.That(customers.Columns.Select(c => c.Name)).DoesNotContain("Email");
	}

	[Test]
	public async Task ComplexSample_AppliesFluentColumnTypeOverride()
	{
		// Act
		var orders = Table(LoadComplex(), "Orders");

		// Assert
		await Assert.That(Column(orders, "Total").StoreType).IsEqualTo("decimal(18,2)");
	}

	[Test]
	public async Task ComplexSample_ResolvesCompositePrimaryKey()
	{
		// OrderLines is keyed by HasKey(e => new { e.OrderId, e.ProductId }).
		// Act
		var lines = Table(LoadComplex(), "OrderLines");

		// Assert
		await Assert.That(Column(lines, "OrderId").IsPrimaryKey).IsTrue();
		await Assert.That(Column(lines, "ProductId").IsPrimaryKey).IsTrue();
		await Assert.That(lines.Columns.Count(c => c.IsPrimaryKey)).IsEqualTo(2);
	}

	[Test]
	public async Task ComplexSample_ResolvesSelfReferencingForeignKey()
	{
		// Act
		var categories = Table(LoadComplex(), "categories");

		// Assert
		await Assert.That(categories.ForeignKeys.Any(fk => fk.PrincipalTable == "categories")).IsTrue();
		await Assert.That(Column(categories, "ParentCategoryId").IsNullable).IsTrue();
	}

	[Test]
	public async Task ComplexSample_ExcludesNotMappedProperties()
	{
		// Act
		var customers = Table(LoadComplex(), "Customers");

		// Assert
		await Assert.That(customers.Columns.Select(c => c.Name)).DoesNotContain("Initials");
	}

	[Test]
	public async Task ComplexSample_ExcludesFluentlyIgnoredProperties()
	{
		// Act
		var users = Table(LoadComplex(), "Users");

		// Assert
		await Assert.That(users.Columns.Select(c => c.Name)).DoesNotContain("CachedDisplayName");
	}

	[Test]
	public async Task ComplexSample_MapsEnumsAndGuidsToStoreTypes()
	{
		// Act
		var model = LoadComplex();

		// Assert
		await Assert.That(Column(Table(model, "Orders"), "Status").StoreType).IsEqualTo("int");
		await Assert.That(Column(Table(model, "Users"), "Id").StoreType).IsEqualTo("guid");
	}

	[Test]
	public async Task ComplexSample_ReadsColumnCommentsFromAttributeAndFluentAPI()
	{
		// Act
		var model = LoadComplex();

		// Assert
		await Assert.That(Column(Table(model, "products"), "unit_price").Comment).IsEqualTo("Price excluding tax");
		await Assert.That(Column(Table(model, "Orders"), "Status").Comment).IsEqualTo("Order lifecycle state");
	}

	[Test]
	public async Task ComplexSample_CompositeKeyForeignKeys_AreNotTreatedAsUnique()
	{
		// A column that is only part of a composite key does not constrain itself to one row, so a
		// join table must render as one-to-many on both sides rather than one-to-one.
		// Act
		var lines = Table(LoadComplex(), "OrderLines");

		// Assert
		await Assert.That(lines.ForeignKeys).IsNotEmpty();
		await Assert.That(lines.ForeignKeys.Any(fk => fk.IsUnique)).IsFalse();
	}

	[Test]
	public async Task ComplexSample_FlagsJoinEntities()
	{
		// UserRoles is a pure join: both key columns are foreign keys. Orders is not.
		// Act
		var model = LoadComplex();

		// Assert
		await Assert.That(Table(model, "UserRoles").IsJoinEntity).IsTrue();
		await Assert.That(Table(model, "Orders").IsJoinEntity).IsFalse();
	}

	[Test]
	public async Task ComplexSample_DerivesRelationshipCardinality()
	{
		// Act
		var orders = Table(LoadComplex(), "Orders");
		var customer = orders.ForeignKeys.Single(fk => fk.PrincipalTable == "Customers");

		// Assert — a non-nullable, non-unique foreign key is a mandatory one-to-many.
		await Assert.That(customer.IsRequired).IsTrue();
		await Assert.That(customer.IsUnique).IsFalse();
	}

	[Test]
	public async Task ComplexSample_DoesNotInventForeignKeysAcrossContexts()
	{
		// AuditEntry.ActorUserId references the identity context's Users by id but has no navigation,
		// so drawing an edge would be misleading.
		// Act
		var entries = Table(LoadComplex(), "audit_entries");

		// Assert
		await Assert.That(entries.Columns.Select(c => c.Name)).Contains("ActorUserId");
		await Assert.That(entries.ForeignKeys.Any(fk => fk.PrincipalTable == "Users")).IsFalse();
	}

	[Test]
	public async Task ComplexSample_RendersGroupedByContextAndSchema()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(D2ModelBuilder.Build(LoadComplex(), CreateOptions()), CreateOptions());

		// Assert — multiple contexts means each is its own container, nested by schema.
		await Assert.That(dsl).Contains("SalesDbContext.sales: {");
		await Assert.That(dsl).Contains("IdentityDbContext.identity: {");
		await Assert.That(dsl).Contains("AuditDbContext.audit: {");
	}

	[Test]
	public async Task ComplexSample_RendersCompositeKeyAndSelfReference()
	{
		// Act
		var dsl = D2DSLGenerator.Generate(D2ModelBuilder.Build(LoadComplex(), CreateOptions()), CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("constraint: [primary_key; foreign_key]");
		await Assert.That(dsl).Contains("ParentCategoryId");
	}

	// Scalar value objects

	[Test]
	public async Task ComplexSample_ShowsScalarValueObjectBesideItsDatabaseType()
	{
		// The domain type sits beside the real database type, which the model always keeps.
		// Act
		var orders = Table(LoadComplex(), "ValueOrders");

		// Assert
		await Assert.That(Column(orders, "CustomerId").DomainTypeName).IsEqualTo("ValueCustomerId");
		await Assert.That(Column(orders, "CustomerId").StoreType).IsEqualTo("int");
		await Assert.That(Column(orders, "Id").DomainTypeName).IsEqualTo("ValueOrderId");
		await Assert.That(Column(orders, "Id").StoreType).IsEqualTo("guid");
	}

	[Test]
	public async Task ComplexSample_ResolvesRelationshipThroughValueObjectForeignKeys()
	{
		// The foreign key is a value object, not a primitive: if it is not recognised as a column, the
		// navigation-derived relationship disappears with it.
		// Act
		var orders = Table(LoadComplex(), "ValueOrders");

		// Assert
		await Assert.That(orders.ForeignKeys.Any(fk => fk.PrincipalTable == "ValueCustomers")).IsTrue();
	}

	[Test]
	public async Task ComplexSample_FlattensComplexValueObjectMembers()
	{
		// A complex value object becomes columns on its owning table, each carrying the complex type.
		// Act
		var customers = Table(LoadComplex(), "ValueCustomers");

		// Assert
		await Assert.That(customers.Columns.Select(c => c.Name)).Contains("Address_City");
		await Assert.That(Column(customers, "Address_City").DomainTypeName).IsEqualTo("ValueAddress");
		await Assert.That(Column(customers, "Address_City").StoreType).IsEqualTo("varchar");
		await Assert.That(Column(customers, "Address_PostCode").IsNullable).IsTrue();
	}

	[Test]
	public async Task ComplexSample_ShowsEnumsAsTheirTypeAndMarksThem()
	{
		// An enum is a domain type too: shown as itself and flagged, with the store type kept.
		// Act
		var status = Column(Table(LoadComplex(), "ValueCustomers"), "Status");

		// Assert
		await Assert.That(status.DomainTypeName).IsEqualTo("ValueStatus");
		await Assert.That(status.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Enum);
		await Assert.That(status.StoreType).IsEqualTo("int");
	}
}
