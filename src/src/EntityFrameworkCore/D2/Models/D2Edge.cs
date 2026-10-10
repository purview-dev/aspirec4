namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;

/// <summary>Represents a column-level edge between two tables, i.e. a foreign key relationship.</summary>
/// <remarks>
/// d2 only targets the exact row when rendered with the ELK or TALA layout engines; under the default
/// dagre engine the edge attaches to the table as a whole.
/// </remarks>
public sealed record D2Edge
{
	/// <summary>The dependent table's d2 identifier.</summary>
	public required string SourceTable { get; init; }

	/// <summary>The dependent column name.</summary>
	public required string SourceColumn { get; init; }

	/// <summary>The principal table's d2 identifier.</summary>
	public required string TargetTable { get; init; }

	/// <summary>The principal column name.</summary>
	public required string TargetColumn { get; init; }

	/// <summary>Optional edge label.</summary>
	public string? Label { get; init; }
}
