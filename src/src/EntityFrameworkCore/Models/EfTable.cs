namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>Represents a single mapped table.</summary>
public sealed record EfTable
{
	/// <summary>The table name, after applying <c>[Table]</c>/<c>ToTable</c> or the EF naming convention.</summary>
	public required string Name { get; init; }

	/// <summary>The fully-qualified CLR type name of the mapped entity.</summary>
	public required string ClrTypeName { get; init; }

	/// <summary>The schema, when explicitly configured. <see langword="null"/> means the provider default.</summary>
	public string? Schema { get; init; }

	/// <summary>The mapped columns, in declaration order.</summary>
	public IReadOnlyList<EfColumn> Columns { get; init; } = [];

	/// <summary>The foreign keys declared on this table.</summary>
	public IReadOnlyList<EfForeignKey> ForeignKeys { get; init; } = [];

	/// <summary>The indexes declared on this table.</summary>
	public IReadOnlyList<EfIndex> Indexes { get; init; } = [];

	/// <summary>
	/// Whether this table exists only to join two others — every primary key column is also a foreign
	/// key, and there is more than one of them.
	/// </summary>
	/// <remarks>
	/// Derived from the key shape rather than from EF, which models a join entity no differently from
	/// any other. Renderers use it to de-emphasise tables that carry no data of their own.
	/// </remarks>
	public bool IsJoinEntity { get; init; }
}
