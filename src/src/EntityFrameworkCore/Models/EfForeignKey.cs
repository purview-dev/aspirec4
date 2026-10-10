namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>Represents a foreign key from a dependent table to a principal table.</summary>
public sealed record EfForeignKey
{
	/// <summary>The dependent (foreign key) column names, in order.</summary>
	public required IReadOnlyList<string> Columns { get; init; }

	/// <summary>The principal table name.</summary>
	public required string PrincipalTable { get; init; }

	/// <summary>The principal column names, in order. Pairs positionally with <see cref="Columns"/>.</summary>
	public required IReadOnlyList<string> PrincipalColumns { get; init; }

	/// <summary>The navigation property this relationship was derived from, when known. Used for labelling.</summary>
	public string? NavigationName { get; init; }
}
