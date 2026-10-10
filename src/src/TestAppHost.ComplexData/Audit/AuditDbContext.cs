using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Aspire.Hosting.AspireC4.TestAppHost.ComplexData.Audit;

/// <summary>
/// A third context whose rows reference accounts in another context by id, with no navigation
/// property.
/// </summary>
/// <remarks>
/// This is the cross-context boundary case: <c>ActorUserId</c> is a logical reference to the identity
/// context's <c>Users</c> table, but because there is no navigation property — and because the
/// principal is not part of this context's model — AspireC4 cannot resolve it into a foreign key, so
/// no edge is drawn. That is the correct outcome: inventing an edge across a database boundary would
/// be misleading.
/// </remarks>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
	public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

	public DbSet<AuditEntryChange> AuditEntryChanges => Set<AuditEntryChange>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.HasDefaultSchema("audit");

		modelBuilder.Entity<AuditEntry>().ToTable("audit_entries");

		modelBuilder.Entity<AuditEntry>().Property(e => e.EntityName).HasColumnName("entity").HasMaxLength(128);

		base.OnModelCreating(modelBuilder);
	}
}

/// <summary>One audited action.</summary>
public sealed class AuditEntry
{
	public long Id { get; set; }

	public string EntityName { get; set; } = string.Empty;

	[MaxLength(64)]
	public string Action { get; set; } = string.Empty;

	/// <summary>References an account in the identity context; deliberately has no navigation.</summary>
	public Guid ActorUserId { get; set; }

	public DateTimeOffset OccurredAt { get; set; }

	public ICollection<AuditEntryChange> Changes { get; } = [];
}

/// <summary>A single field-level change belonging to an audit entry.</summary>
public sealed class AuditEntryChange
{
	public long Id { get; set; }

	public long AuditEntryId { get; set; }

	public AuditEntry? AuditEntry { get; set; }

	[MaxLength(128)]
	public string PropertyName { get; set; } = string.Empty;

	public string? OldValue { get; set; }

	public string? NewValue { get; set; }
}
