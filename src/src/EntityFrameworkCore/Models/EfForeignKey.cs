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

	/// <summary>
	/// The principal-side navigation property, when the model declares one — the other half of the
	/// relationship's name.
	/// </summary>
	public string? PrincipalNavigationName { get; init; }

	/// <summary>
	/// The foreign key's constraint name, when the model gives it one.
	/// </summary>
	/// <remarks>
	/// Only the build-time dump can see this: EF produces it while building the model, which the other
	/// readers never do.
	/// </remarks>
	public string? ConstraintName { get; init; }

	/// <summary>
	/// Whether the dependent side is constrained to at most one row, making this one-to-one rather than
	/// one-to-many.
	/// </summary>
	/// <remarks>
	/// Derived statically: true when the foreign key columns are themselves the primary key, or are
	/// covered by a unique index. EF's own <c>IsUnique</c> is a runtime model concept and is not
	/// available to the generator.
	/// </remarks>
	public bool IsUnique { get; init; }

	/// <summary>
	/// Whether the relationship is mandatory, i.e. every dependent row must reference a principal.
	/// </summary>
	/// <remarks>Derived from the nullability of the foreign key columns.</remarks>
	public bool IsRequired { get; init; }
}
