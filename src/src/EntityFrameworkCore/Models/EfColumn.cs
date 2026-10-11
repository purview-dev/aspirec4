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
