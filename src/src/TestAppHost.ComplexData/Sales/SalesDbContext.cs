using Microsoft.EntityFrameworkCore;

namespace Aspire.Hosting.AspireC4.TestAppHost.ComplexData.Sales;

/// <summary>
/// The richest of the three sample contexts: a default schema, a per-entity schema override, a
/// composite key, a self-referencing foreign key, and fluent column configuration.
/// </summary>
/// <remarks>
/// Every fluent call here is within the subset AspireC4 interprets statically. Configuration applied
/// indirectly — through <c>ApplyConfigurationsFromAssembly</c> or a helper method — would not be seen,
/// and is reported as ASPIREC4010.
/// </remarks>
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
	public DbSet<Customer> Customers => Set<Customer>();

	public DbSet<Category> Categories => Set<Category>();

	public DbSet<Product> Products => Set<Product>();

	public DbSet<Order> Orders => Set<Order>();

	public DbSet<OrderLine> OrderLines => Set<OrderLine>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.HasDefaultSchema("sales");

		modelBuilder.Entity<Customer>().Property(e => e.Email).HasColumnName("email_address");

		modelBuilder.Entity<Category>().ToTable("categories", "catalogue");

		modelBuilder.Entity<Order>().Property(e => e.Total).HasColumnType("decimal(18,2)");

		modelBuilder.Entity<Order>().HasIndex(e => e.PlacedAt);

		// A composite primary key across the two foreign keys.
		modelBuilder.Entity<OrderLine>().HasKey(e => new { e.OrderId, e.ProductId });

		modelBuilder.Entity<OrderLine>().Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");

		base.OnModelCreating(modelBuilder);
	}
}
