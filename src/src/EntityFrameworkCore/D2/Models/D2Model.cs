namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;

/// <summary>The complete in-memory model used to generate a d2 ER diagram.</summary>
public sealed record D2Model
{
	/// <summary>The ordered list of tables in the diagram.</summary>
	public required IReadOnlyList<D2Table> Tables { get; init; }

	/// <summary>The ordered list of column-level edges between tables.</summary>
	public required IReadOnlyList<D2Edge> Edges { get; init; }

	/// <summary>A pre-built empty model containing no tables or edges.</summary>
	public static readonly D2Model Empty = new() { Tables = [], Edges = [] };
}
