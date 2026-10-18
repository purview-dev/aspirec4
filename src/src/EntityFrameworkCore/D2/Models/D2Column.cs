namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;

/// <summary>Represents a single row of a d2 <c>sql_table</c> shape.</summary>
public sealed record D2Column
{
	/// <summary>The column name, used as the d2 row key.</summary>
	public required string Name { get; init; }

	/// <summary>The rendered type, e.g. <c>varchar</c>. Becomes the row's primary value.</summary>
	public required string Type { get; init; }

	/// <summary>The constraints applied to this column, emitted as the row's <c>constraint</c> value.</summary>
	public IReadOnlyList<D2Constraint> Constraints { get; init; } = [];
}
