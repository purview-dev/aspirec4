using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Aspire.Hosting.AspireC4.TestAppHost.ComplexData.Sales;

/// <summary>The lifecycle states an order moves through.</summary>
public enum OrderStatus
{
	Draft,
	Placed,
	Shipped,
	Cancelled,
}

/// <summary>A customer, keyed conventionally and uniquely indexed on email.</summary>
[Index(nameof(Email), IsUnique = true)]
public sealed class Customer
{
	public int Id { get; set; }

	[Required]
	[MaxLength(200)]
	public string Email { get; set; } = string.Empty;

	[MaxLength(120)]
	public string? DisplayName { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Not persisted — exercises <c>[NotMapped]</c>.</summary>
	[NotMapped]
	public string Initials => DisplayName?[..1] ?? "?";

	public ICollection<Order> Orders { get; } = [];
}

/// <summary>
/// A self-referencing hierarchy: the foreign key points back at the same table.
/// </summary>
public sealed class Category
{
	public int Id { get; set; }

	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;

	public int? ParentCategoryId { get; set; }

	public Category? ParentCategory { get; set; }

	public ICollection<Product> Products { get; } = [];
}

/// <summary>Table and column names come from attributes rather than convention.</summary>
[Table("products", Schema = "catalogue")]
public sealed class Product
{
	[Column("product_id")]
	public int Id { get; set; }

	[Column("product_name")]
	[MaxLength(200)]
	public string Name { get; set; } = string.Empty;

	[Column("unit_price", TypeName = "decimal(18,4)")]
	[Comment("Price excluding tax")]
	public decimal UnitPrice { get; set; }

	public int CategoryId { get; set; }

	public Category? Category { get; set; }

	public ICollection<OrderLine> OrderLines { get; } = [];
}

/// <summary>An order, with an enum status and an optional shipped timestamp.</summary>
public sealed class Order
{
	public int Id { get; set; }

	public int CustomerId { get; set; }

	public Customer? Customer { get; set; }

	public OrderStatus Status { get; set; }

	public decimal Total { get; set; }

	public DateTimeOffset PlacedAt { get; set; }

	public DateTimeOffset? ShippedAt { get; set; }

	public ICollection<OrderLine> Lines { get; } = [];
}

/// <summary>
/// A join entity carrying a payload, keyed by a composite primary key configured fluently.
/// </summary>
public sealed class OrderLine
{
	public int OrderId { get; set; }

	public Order? Order { get; set; }

	public int ProductId { get; set; }

	public Product? Product { get; set; }

	public int Quantity { get; set; }

	public decimal UnitPrice { get; set; }
}
