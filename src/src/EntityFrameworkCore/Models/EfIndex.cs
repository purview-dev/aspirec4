namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>Represents an index declared on a table.</summary>
public sealed record EfIndex
{
	/// <summary>The indexed column names, in order.</summary>
	public required IReadOnlyList<string> Columns { get; init; }

	/// <summary>Whether the index is unique.</summary>
	public bool IsUnique { get; init; }

	/// <summary>The index name, when explicitly configured.</summary>
	public string? Name { get; init; }
}
