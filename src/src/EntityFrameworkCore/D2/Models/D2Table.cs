namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;

/// <summary>Represents a single <c>shape: sql_table</c> in the d2 model.</summary>
public sealed record D2Table
{
	/// <summary>The d2 identifier for this table. Must already be sanitised.</summary>
	public required string Id { get; init; }

	/// <summary>The display label. Emitted as <c>label:</c> when it differs from <see cref="Id"/>.</summary>
	public required string Label { get; init; }

	/// <summary>Optional container this table is nested inside — used to group tables by schema.</summary>
	public string? Container { get; init; }

	/// <summary>The columns, in order. Each becomes one row of the sql_table shape.</summary>
	public IReadOnlyList<D2Column> Columns { get; init; } = [];
}
