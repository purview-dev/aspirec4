using Microsoft.EntityFrameworkCore;

namespace Aspire.Hosting.AspireC4.TestAppHost.ComplexData.ValueObjects;

/// <summary>
/// A context whose entities are keyed by scalar value objects rather than primitives — the shape a
/// strongly-typed identifier library produces.
/// </summary>
/// <remarks>
/// The identifiers are plain structs wrapping a single <c>Value</c>, which is exactly what the
/// Purview value-object generator emits. AspireC4 has to recognise them as scalar columns, or their
/// foreign keys disappear from the diagram along with the relationships they carry.
/// </remarks>
public sealed class ValueObjectDbContext(DbContextOptions<ValueObjectDbContext> options) : DbContext(options)
{
	public DbSet<ValueCustomer> ValueCustomers => Set<ValueCustomer>();

	public DbSet<ValueOrder> ValueOrders => Set<ValueOrder>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		// The converters keep the model valid for EF. AspireC4 does not interpret them: it recovers the
		// value objects from metadata and shows the domain type, which is the point of the sample.
		modelBuilder
			.Entity<ValueCustomer>()
			.Property(e => e.Id)
			.HasConversion(value => value.Value, raw => new ValueCustomerId { Value = raw });

		modelBuilder
			.Entity<ValueOrder>()
			.Property(e => e.Id)
			.HasConversion(value => value.Value, raw => new ValueOrderId { Value = raw });

		modelBuilder
			.Entity<ValueOrder>()
			.Property(e => e.CustomerId)
			.HasConversion(value => value.Value, raw => new ValueCustomerId { Value = raw });

		modelBuilder
			.Entity<ValueOrder>()
			.HasOne(e => e.Customer)
			.WithMany(e => e.Orders)
			.HasForeignKey(e => e.CustomerId);

		base.OnModelCreating(modelBuilder);
	}
}

/// <summary>A strongly-typed customer identifier, wrapping an <c>int</c>.</summary>
public readonly record struct ValueCustomerId
{
	public int Value { get; init; }
}

/// <summary>A strongly-typed order identifier, wrapping a <c>Guid</c>.</summary>
public readonly record struct ValueOrderId
{
	public Guid Value { get; init; }
}

/// <summary>
/// A complex value object: flattened into its owning table rather than mapped as a table of its own.
/// </summary>
[ValueObject(EFMapping = ValueObjectMapping.ComplexType)]
public readonly record struct ValueAddress
{
	public string City { get; init; }

	public string? PostCode { get; init; }
}

/// <summary>An enum, stored as its underlying primitive.</summary>
public enum ValueStatus
{
	Draft,

	Active,

	Archived,
}

/// <summary>An entity keyed by a value object rather than a primitive.</summary>
public sealed class ValueCustomer
{
	public ValueCustomerId Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public ValueAddress Address { get; set; }

	public ValueStatus Status { get; set; }

	public ICollection<ValueOrder> Orders { get; } = [];
}

/// <summary>An entity whose foreign key is a value object.</summary>
public sealed class ValueOrder
{
	public ValueOrderId Id { get; set; }

	public ValueCustomerId CustomerId { get; set; }

	public ValueCustomer? Customer { get; set; }

	public decimal Total { get; set; }
}
