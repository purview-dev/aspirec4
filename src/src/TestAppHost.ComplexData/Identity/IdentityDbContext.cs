using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Aspire.Hosting.AspireC4.TestAppHost.ComplexData.Identity;

/// <summary>A second context in its own schema, sharing no tables with the first.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();

	public DbSet<Role> Roles => Set<Role>();

	public DbSet<UserRole> UserRoles => Set<UserRole>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.HasDefaultSchema("identity");

		// A pure join table: composite key, no payload.
		modelBuilder.Entity<UserRole>().HasKey(e => new { e.UserId, e.RoleId });

		// Excluded from the model entirely, so it must not appear as a column.
		modelBuilder.Entity<User>().Ignore(e => e.CachedDisplayName);

		modelBuilder.Entity<User>().Property(e => e.UserName).IsRequired().HasMaxLength(64);

		base.OnModelCreating(modelBuilder);
	}
}

/// <summary>An account. Keyed by a <c>Guid</c> rather than an <c>int</c>.</summary>
[Index(nameof(UserName), IsUnique = true)]
public sealed class User
{
	public Guid Id { get; set; }

	public string UserName { get; set; } = string.Empty;

	[MaxLength(256)]
	public string? Email { get; set; }

	public bool IsLockedOut { get; set; }

	public DateTimeOffset? LastSignInAt { get; set; }

	/// <summary>Removed via the fluent <c>Ignore</c> call rather than an attribute.</summary>
	public string? CachedDisplayName { get; set; }

	public ICollection<UserRole> UserRoles { get; } = [];
}

/// <summary>A role an account can hold.</summary>
public sealed class Role
{
	public Guid Id { get; set; }

	[MaxLength(64)]
	public string Name { get; set; } = string.Empty;

	public ICollection<UserRole> UserRoles { get; } = [];
}

/// <summary>The join between <see cref="User"/> and <see cref="Role"/>.</summary>
public sealed class UserRole
{
	public Guid UserId { get; set; }

	public User? User { get; set; }

	public Guid RoleId { get; set; }

	public Role? Role { get; set; }

	public DateTimeOffset GrantedAt { get; set; }
}
