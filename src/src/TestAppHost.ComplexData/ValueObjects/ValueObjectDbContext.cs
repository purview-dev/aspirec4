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
public sealed class ValueObjectDbContext(DbContextOptions<ValueObjectDbContext> options)
	: ValueObjectDbContextBase<ValueObjectDbContext>(options)
{
	public DbSet<ValueCustomer> ValueCustomers => Set<ValueCustomer>();

	public DbSet<ValueOrder> ValueOrders => Set<ValueOrder>();

	public DbSet<ValueCustomerProfile> ValueCustomerProfiles => Set<ValueCustomerProfile>();

	public DbSet<ValueSiteEntity> ValueSites => Set<ValueSiteEntity>();

	public DbSet<ValueSiteStateEntity> ValueSiteStates => Set<ValueSiteStateEntity>();

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
			.Entity<ValueCustomerProfile>()
			.Property(e => e.Id)
			.HasConversion(value => value.Value, raw => new ValueCustomerId { Value = raw });

		modelBuilder
			.Entity<ValueAudit>()
			.Property(e => e.Id)
			.HasConversion(value => value.Value, raw => new ValueAuditId { Value = raw });

		modelBuilder
			.Entity<ValueOrder>()
			.HasOne(e => e.Customer)
			.WithMany(e => e.Orders)
			.HasForeignKey(e => e.CustomerId);

		modelBuilder
			.Entity<ValueSiteEntity>()
			.Property(e => e.Id)
			.HasConversion(value => value.Value, raw => new ValueSiteId { Value = raw });

		modelBuilder
			.Entity<ValueSiteStateEntity>()
			.Property(e => e.Id)
			.HasConversion(value => value.Value, raw => new ValueSiteId { Value = raw });

		// A shared-primary-key one-to-one: the profile is keyed by the customer it belongs to, so it
		// declares no foreign-key property of its own.
		modelBuilder
			.Entity<ValueCustomerProfile>()
			.HasOne(e => e.Customer)
			.WithOne(e => e.Profile)
			.HasForeignKey<ValueCustomerProfile>(e => e.Id);

		modelBuilder
			.Entity<ValueSiteStateEntity>()
			.HasOne(e => e.Site)
			.WithOne(e => e.State)
			.HasForeignKey<ValueSiteStateEntity>(e => e.Id);

		base.OnModelCreating(modelBuilder);
	}
}

/// <summary>
/// An abstract, generic base context: the shape a codebase uses to share conventions across contexts.
/// </summary>
/// <remarks>
/// It owns a <c>DbSet</c> of its own, so a reader that only looks at what the derived context declares
/// loses that table. It is also never instantiated, so it must not appear on the diagram as a context
/// in its own right — it would be an empty node beside the one that actually has the model.
/// </remarks>
public abstract class ValueObjectDbContextBase<TContext>(DbContextOptions<TContext> options) : DbContext(options)
	where TContext : DbContext
{
	public DbSet<ValueAudit> ValueAudits => Set<ValueAudit>();
}

/// <summary>A strongly-typed site identifier, wrapping a <c>Guid</c>.</summary>
public readonly record struct ValueSiteId
{
	public Guid Value { get; init; }
}

/// <summary>
/// The principal of a shared-primary-key one-to-one, named with the <c>Entity</c> suffix a persistence
/// model usually carries.
/// </summary>
/// <remarks>
/// The suffix is what makes the pairing readable: <c>ValueSiteId</c> is this entity's own identifier,
/// which is how the dependent keyed by that same type is told apart from its principal.
/// </remarks>
public sealed class ValueSiteEntity
{
	public ValueSiteId Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public ValueSiteStateEntity? State { get; set; }
}

/// <summary>A one-to-one extension keyed by the site it describes, not by an identity of its own.</summary>
public sealed class ValueSiteStateEntity
{
	public ValueSiteId Id { get; set; }

	public bool IsArchived { get; set; }

	public ValueSiteEntity Site { get; set; } = null!;
}

/// <summary>A strongly-typed audit identifier, wrapping a <c>Guid</c>.</summary>
public readonly record struct ValueAuditId
{
	public Guid Value { get; init; }
}

/// <summary>
/// The key and the timestamp every audit row carries, declared once on a base entity.
/// </summary>
public abstract class ValueAuditBase
{
	public ValueAuditId Id { get; set; }

	public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>An entity whose key and timestamp are inherited rather than declared.</summary>
public sealed class ValueAudit : ValueAuditBase
{
	public string Message { get; set; } = string.Empty;
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

	public ValueCustomerProfile? Profile { get; set; }
}

/// <summary>
/// A one-to-one dependent whose primary key is also its foreign key.
/// </summary>
/// <remarks>
/// There is no <c>CustomerId</c> property to find: the relationship exists only in the key types, which
/// is what the shared-primary-key recovery has to recognise.
/// </remarks>
public sealed class ValueCustomerProfile
{
	public ValueCustomerId Id { get; set; }

	public string Biography { get; set; } = string.Empty;

	public ValueCustomer? Customer { get; set; }
}

/// <summary>An entity whose foreign key is a value object.</summary>
public sealed class ValueOrder
{
	public ValueOrderId Id { get; set; }

	public ValueCustomerId CustomerId { get; set; }

	public ValueCustomer? Customer { get; set; }

	public decimal Total { get; set; }
}
