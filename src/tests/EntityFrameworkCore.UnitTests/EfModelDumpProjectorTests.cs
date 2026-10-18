using Aspire.Hosting.AspireC4.EntityFrameworkCore.ModelDump;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Verifies the build-time dump projector against a real EF Core model — the authoritative source that
/// carries conventions, value converters and composite foreign keys.
/// </summary>
public sealed class EfModelDumpProjectorTests
{
	// Helpers

	static DbContextOptions<DumpDbContext> CreateOptions() =>
		new DbContextOptionsBuilder<DumpDbContext>().UseSqlite("Data Source=:memory:").Options;

	static EfModel Project()
	{
		using DumpDbContext context = new(CreateOptions());

		// The design-time model carries the relational configuration the diagram reads.
		var json = EfModelDumpProjector.Project(
			context.GetService<IDesignTimeModel>().Model,
			nameof(DumpDbContext),
			typeof(DumpDbContext).FullName!
		);

		return EfModelLoader.Parse(json);
	}

	static EfTable Table(EfModel model, string name) =>
		model.Contexts.SelectMany(c => c.Tables).Single(t => t.Name == name);

	// Tests

	[Test]
	public async Task Project_ProducesTheDiagramContract()
	{
		// Act
		var model = Project();

		// Assert
		await Assert.That(model.Contexts.Count).IsEqualTo(1);
		await Assert.That(model.Contexts[0].Name).IsEqualTo("DumpDbContext");
	}

	[Test]
	public async Task Project_ShowsValueObjectColumnsBesideTheirDatabaseType()
	{
		// Act
		var orders = Table(Project(), "DumpOrders");

		// Assert — the value type is shown, and the real database type is kept beside it.
		await Assert.That(orders.Columns.Single(c => c.Name == "Id").DomainTypeName).IsEqualTo("DumpOrderId");
		await Assert.That(orders.Columns.Single(c => c.Name == "Id").StoreType).IsEqualTo("guid");
		await Assert
			.That(orders.Columns.Single(c => c.Name == "CustomerId").DomainTypeName)
			.IsEqualTo("DumpCustomerId");
		await Assert.That(orders.Columns.Single(c => c.Name == "CustomerId").StoreType).IsEqualTo("int");
	}

	[Test]
	public async Task Project_CapturesCompositeForeignKeys()
	{
		// The whole reason the dump exists: a composite foreign key the navigation heuristic cannot
		// reconstruct from source.
		// Act
		var orders = Table(Project(), "DumpOrders");
		var foreignKey = orders.ForeignKeys.Single();

		// Assert
		await Assert.That(foreignKey.Columns).IsEquivalentTo(["TenantId", "CustomerId"]);
		await Assert.That(foreignKey.PrincipalTable).IsEqualTo("DumpCustomers");
		await Assert.That(foreignKey.PrincipalColumns).IsEquivalentTo(["TenantId", "Id"]);
	}

	[Test]
	public async Task Project_FlattensComplexValueObjects()
	{
		// EF is authoritative here: it gives the flattened column name and the complex type it came from.
		// Act
		var customers = Table(Project(), "DumpCustomers");
		var city = customers.Columns.Single(c =>
			c.DomainTypeName == "DumpAddress" && c.Name.Contains("City", StringComparison.Ordinal)
		);

		// Assert
		await Assert.That(city.StoreType).IsEqualTo("varchar");
	}

	[Test]
	public async Task Project_ShowsEnumsAsTheirTypeAndMarksThem()
	{
		// Act
		var status = Table(Project(), "DumpCustomers").Columns.Single(c => c.Name == "Status");

		// Assert
		await Assert.That(status.DomainTypeName).IsEqualTo("DumpStatus");
		await Assert.That(status.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Enum);
		await Assert.That(status.StoreType).IsEqualTo("int");
	}

	[Test]
	public async Task Project_CapturesTheRelationshipName()
	{
		// Act
		var foreignKey = Table(Project(), "DumpOrders").ForeignKeys.Single();

		// Assert — the constraint name is visible only to the dump path, which built the model.
		await Assert.That(foreignKey.NavigationName).IsEqualTo("Customer");
		await Assert.That(foreignKey.ConstraintName).IsNotNull();
	}
}

/// <summary>A value object, wrapping an <c>int</c>.</summary>
public readonly record struct DumpCustomerId
{
	public int Value { get; init; }
}

/// <summary>A value object, wrapping a <c>Guid</c>.</summary>
public readonly record struct DumpOrderId
{
	public Guid Value { get; init; }
}

/// <summary>A value object, wrapping a <c>Guid</c>.</summary>
public readonly record struct DumpTenantId
{
	public Guid Value { get; init; }
}

/// <summary>A complex value object, flattened into its owner's table by EF.</summary>
public readonly record struct DumpAddress
{
	public string City { get; init; }

	public string? PostCode { get; init; }
}

/// <summary>An enum, stored as its underlying primitive.</summary>
public enum DumpStatus
{
	Draft,

	Active,
}

public sealed class DumpCustomer
{
	public DumpCustomerId Id { get; set; }

	public DumpTenantId TenantId { get; set; }

	public string Name { get; set; } = string.Empty;

	public DumpAddress Address { get; set; }

	public DumpStatus Status { get; set; }
}

public sealed class DumpOrder
{
	public DumpOrderId Id { get; set; }

	public DumpTenantId TenantId { get; set; }

	public DumpCustomerId CustomerId { get; set; }

	public DumpCustomer? Customer { get; set; }

	public decimal Total { get; set; }
}

/// <summary>A context with value-object keys and a composite foreign key, mirroring a real domain.</summary>
public sealed class DumpDbContext(DbContextOptions<DumpDbContext> options) : DbContext(options)
{
	public DbSet<DumpCustomer> DumpCustomers => Set<DumpCustomer>();

	public DbSet<DumpOrder> DumpOrders => Set<DumpOrder>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<DumpCustomer>(builder =>
		{
			builder.HasKey(c => c.Id);
			builder.HasAlternateKey(c => new { c.TenantId, c.Id });
			builder.ComplexProperty(c => c.Address);
			builder.Property(c => c.Id).HasConversion(value => value.Value, raw => new DumpCustomerId { Value = raw });
			builder
				.Property(c => c.TenantId)
				.HasConversion(value => value.Value, raw => new DumpTenantId { Value = raw });
		});

		modelBuilder.Entity<DumpOrder>(builder =>
		{
			builder.HasKey(o => new { o.TenantId, o.Id });
			builder.Property(o => o.Id).HasConversion(value => value.Value, raw => new DumpOrderId { Value = raw });
			builder
				.Property(o => o.TenantId)
				.HasConversion(value => value.Value, raw => new DumpTenantId { Value = raw });
			builder
				.Property(o => o.CustomerId)
				.HasConversion(value => value.Value, raw => new DumpCustomerId { Value = raw });

			builder
				.HasOne(o => o.Customer)
				.WithMany()
				.HasForeignKey(o => new { o.TenantId, o.CustomerId })
				.HasPrincipalKey(c => new { c.TenantId, c.Id });
		});
	}
}

/// <summary>The design-time factory the dump tool discovers and invokes.</summary>
public sealed class DumpDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DumpDbContext>
{
	public DumpDbContext CreateDbContext(string[] args) =>
		new(new DbContextOptionsBuilder<DumpDbContext>().UseSqlite("Data Source=:memory:").Options);
}
