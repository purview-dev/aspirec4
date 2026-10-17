using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Aspire.Hosting.AspireC4.TestAppHost.ComplexData.Sales;
using Aspire.Hosting.AspireC4.TestAppHost.SimpleData;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Exercises the analyzer-less discovery path against assemblies built from the real EF Core API.
/// </summary>
/// <remarks>
/// The sample data assemblies also carry a source-generated descriptor, but these tests call the
/// metadata reader directly so the convention-based recovery is proven independently of it.
/// </remarks>
public sealed class EfModelMetadataReaderTests
{
	// Helpers

	static IReadOnlyList<EfContext> Read(Type typeInAssembly) =>
		EfModelMetadataReader.ReadFromAssembly(typeInAssembly.Assembly.Location);

	static EfContext Context(IReadOnlyList<EfContext> contexts, string name) => contexts.Single(c => c.Name == name);

	static EfTable Table(EfContext context, string name) => context.Tables.Single(t => t.Name == name);

	// Simple sample

	[Test]
	public async Task ReadFromAssembly_SingleContext_FindsItApproximate()
	{
		// Act
		var contexts = Read(typeof(BlogDbContext));

		// Assert
		var context = Context(contexts, "BlogDbContext");
		await Assert.That(context.Fidelity).IsEqualTo(EfModelFidelity.Approximate);
		await Assert.That(context.TypeName).Contains("BlogDbContext");
	}

	[Test]
	public async Task ReadFromAssembly_DerivesTableNamesFromDbSetProperties()
	{
		// Act
		var context = Context(Read(typeof(BlogDbContext)), "BlogDbContext");

		// Assert
		await Assert.That(context.Tables.Select(t => t.Name)).Contains("Blogs");
		await Assert.That(context.Tables.Select(t => t.Name)).Contains("Posts");
	}

	[Test]
	public async Task ReadFromAssembly_ResolvesConventionalKeyAndForeignKey()
	{
		// Act
		var posts = Table(Context(Read(typeof(BlogDbContext)), "BlogDbContext"), "Posts");

		// Assert
		await Assert.That(posts.Columns.Single(c => c.Name == "Id").IsPrimaryKey).IsTrue();
		await Assert.That(posts.ForeignKeys.Count).IsEqualTo(1);
		await Assert.That(posts.ForeignKeys[0].PrincipalTable).IsEqualTo("Blogs");
	}

	// Complex sample

	[Test]
	public async Task ReadFromAssembly_MultipleContexts_FindsAllThree()
	{
		// Act
		var contexts = Read(typeof(SalesDbContext));

		// Assert
		await Assert.That(contexts.Select(c => c.Name)).Contains("SalesDbContext");
		await Assert.That(contexts.Select(c => c.Name)).Contains("IdentityDbContext");
		await Assert.That(contexts.Select(c => c.Name)).Contains("AuditDbContext");
	}

	[Test]
	public async Task ReadFromAssembly_AppliesTableAndColumnAnnotations()
	{
		// Act
		var products = Table(Context(Read(typeof(SalesDbContext)), "SalesDbContext"), "products");

		// Assert
		await Assert.That(products.Schema).IsEqualTo("catalogue");
		await Assert.That(products.Columns.Single(c => c.Name == "unit_price").StoreType).IsEqualTo("decimal(18,4)");
	}

	// Scalar value objects

	[Test]
	public async Task ReadFromAssembly_ShowsScalarValueObjectBesideItsDatabaseType()
	{
		// The domain type sits beside the real database type, which the model always keeps.
		// Act
		var orders = Table(Context(Read(typeof(SalesDbContext)), "ValueObjectDbContext"), "ValueOrders");

		// Assert
		await Assert
			.That(orders.Columns.Single(c => c.Name == "CustomerId").DomainTypeName)
			.IsEqualTo("ValueCustomerId");
		await Assert.That(orders.Columns.Single(c => c.Name == "CustomerId").StoreType).IsEqualTo("int");
	}

	[Test]
	public async Task ReadFromAssembly_ResolvesRelationshipThroughValueObjectForeignKeys()
	{
		// The foreign key is a value object, not a primitive: if it is not recognised as a column, the
		// navigation-derived relationship disappears with it.
		// Act
		var orders = Table(Context(Read(typeof(SalesDbContext)), "ValueObjectDbContext"), "ValueOrders");

		// Assert
		await Assert.That(orders.ForeignKeys.Any(fk => fk.PrincipalTable == "ValueCustomers")).IsTrue();
	}

	[Test]
	public async Task ReadFromAssembly_NamesTheRelationshipWithBothNavigations()
	{
		// The inverse navigation is found from the collection on the principal, so the relationship is
		// named by both ends even without a built model.
		// Act
		var foreignKey = Table(Context(Read(typeof(SalesDbContext)), "ValueObjectDbContext"), "ValueOrders")
			.ForeignKeys.Single();

		// Assert
		await Assert.That(foreignKey.NavigationName).IsEqualTo("Customer");
		await Assert.That(foreignKey.PrincipalNavigationName).IsEqualTo("Orders");
	}

	[Test]
	public async Task ReadFromAssembly_FlattensComplexValueObjectMembers()
	{
		// A complex value object becomes columns on its owning table, each carrying the complex type.
		// Act
		var customers = Table(Context(Read(typeof(SalesDbContext)), "ValueObjectDbContext"), "ValueCustomers");

		// Assert
		await Assert.That(customers.Columns.Select(c => c.Name)).Contains("Address_City");
		await Assert
			.That(customers.Columns.Single(c => c.Name == "Address_City").DomainTypeName)
			.IsEqualTo("ValueAddress");
	}

	[Test]
	public async Task ReadFromAssembly_ShowsEnumsAsTheirTypeAndMarksThem()
	{
		// Act
		var status = Table(Context(Read(typeof(SalesDbContext)), "ValueObjectDbContext"), "ValueCustomers")
			.Columns.Single(c => c.Name == "Status");

		// Assert
		await Assert.That(status.DomainTypeName).IsEqualTo("ValueStatus");
		await Assert.That(status.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Enum);
		await Assert.That(status.StoreType).IsEqualTo("int");
	}

	// Directory scanning and descriptor de-duplication

	[Test]
	public async Task Read_AssemblyThatCarriesADescriptor_IsSkipped()
	{
		// The loader reads descriptor-carrying assemblies at full fidelity elsewhere, so the metadata
		// pass must leave them alone or the same context would appear twice.
		// Arrange
		var assembly = typeof(BlogDbContext).Assembly.Location;
		var directory = Path.GetDirectoryName(assembly)!;

		// Act
		var contexts = EfModelMetadataReader.Read(directory, [Path.GetFileName(assembly)]);

		// Assert
		await Assert.That(contexts.Any(c => c.Name == "BlogDbContext")).IsFalse();
	}

	[Test]
	public async Task Read_Directory_RecoversContextsFromUnskippedAssemblies()
	{
		// Arrange
		var directory = Path.GetDirectoryName(typeof(BlogDbContext).Assembly.Location)!;

		// Act
		var contexts = EfModelMetadataReader.Read(directory, []);

		// Assert
		await Assert.That(contexts.Select(c => c.Name)).Contains("BlogDbContext");
		await Assert.That(contexts.All(c => c.Fidelity == EfModelFidelity.Approximate)).IsTrue();
	}
}
