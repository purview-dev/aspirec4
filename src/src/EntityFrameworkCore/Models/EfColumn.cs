using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>Represents a single mapped column.</summary>
public sealed record EfColumn
{
	/// <summary>The column name, after applying <c>[Column]</c>/<c>HasColumnName</c> or the property name.</summary>
	public required string Name { get; init; }

	/// <summary>The store type, e.g. <c>int</c>, <c>varchar</c>. Inferred from the CLR type unless explicitly configured.</summary>
	public required string StoreType { get; init; }

	/// <summary>The CLR type name of the backing property.</summary>
	public string? ClrTypeName { get; init; }

	/// <summary>
	/// The domain type backing this column, when its declared type is not a primitive — a scalar value
	/// object, a complex value object, or an enum.
	/// </summary>
	/// <remarks>
	/// The database type stays in <see cref="StoreType"/>; this is shown alongside it according to
	/// <c>AspireC4EfDiagramOptions.DomainTypeDisplay</c>. Both are always present, so a consumer can
	/// surface the database definition even when the domain type is the headline.
	/// </remarks>
	public string? DomainTypeName { get; init; }

	/// <summary>Which kind of type <see cref="DomainTypeName"/> is, when one is present.</summary>
	/// <remarks>Read leniently, so a kind added by a newer analyzer cannot empty the whole model.</remarks>
	[JsonConverter(typeof(EfDomainTypeKindConverter))]
	public EfDomainTypeKind? DomainTypeKind { get; init; }

	/// <summary>
	/// For a column flattened out of a complex value object, the owning entity's property it came from —
	/// <c>ShippingAddress</c> for <c>ShippingAddress_PostCode</c>.
	/// </summary>
	/// <remarks>
	/// Recorded rather than inferred from the column name: a renamed member or a property name that
	/// itself contains an underscore defeats the inference, and a member whose own type is a value object
	/// or an enum carries that type rather than the complex one, so nothing else in the column says it
	/// belongs to the group. Set on every member, at any depth, to the outermost property.
	/// </remarks>
	public string? ComplexPropertyName { get; init; }

	/// <summary>
	/// The complex value object type <see cref="ComplexPropertyName"/> holds, e.g. <c>Address</c>.
	/// </summary>
	public string? ComplexTypeName { get; init; }

	/// <summary>Whether the column is nullable.</summary>
	public bool IsNullable { get; init; }

	/// <summary>Whether the column participates in the primary key.</summary>
	public bool IsPrimaryKey { get; init; }

	/// <summary>Whether the column participates in a unique index.</summary>
	public bool IsUnique { get; init; }

	/// <summary>Whether the column participates in a foreign key.</summary>
	public bool IsForeignKey { get; init; }

	/// <summary>The configured maximum length, when one was found.</summary>
	public int? MaxLength { get; init; }

	/// <summary>
	/// The column comment from <c>[Comment]</c> or <c>HasComment</c>, when present.
	/// </summary>
	/// <remarks>Mermaid renders attribute comments natively; DBML carries them as a column note.</remarks>
	public string? Comment { get; init; }
}
