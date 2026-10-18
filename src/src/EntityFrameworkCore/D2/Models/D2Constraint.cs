namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;

/// <summary>The column constraints d2 understands for <c>sql_table</c> shapes.</summary>
/// <remarks>
/// d2 renders these as <c>PK</c>, <c>FK</c> and <c>UNQ</c> respectively. Any other value is passed
/// through verbatim and rendered as-is, so the set is intentionally closed.
/// </remarks>
public enum D2Constraint
{
	/// <summary>Emitted as <c>primary_key</c>.</summary>
	PrimaryKey,

	/// <summary>Emitted as <c>foreign_key</c>.</summary>
	ForeignKey,

	/// <summary>Emitted as <c>unique</c>.</summary>
	Unique,
}
