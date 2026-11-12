namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// How a column shows its domain type — a value object, an enum or a union — alongside the real database type.
/// </summary>
/// <remarks>
/// The database type is never discarded: every mode keeps it in the model, and each renderer that has an
/// annotation slot — Mermaid's comment, DBML's note, the interactive viewer's tooltip — shows it there when
/// it is not the headline. d2's <c>sql_table</c> rows have no such slot, so under <see cref="DomainTypeOnly"/>
/// a d2 diagram shows the domain type alone.
/// </remarks>
public enum DomainTypeDisplay
{
	/// <summary><c>AssetId (guid)</c>. The default.</summary>
	DomainTypeFirst,

	/// <summary><c>guid (AssetId)</c>.</summary>
	DatabaseFirst,

	/// <summary>
	/// <c>AssetId</c>. The database type is kept in the model and shown in the annotation slot of each
	/// renderer that has one, but is not the headline.
	/// </summary>
	DomainTypeOnly,

	/// <summary><c>guid</c>. The domain type is not shown.</summary>
	DatabaseOnly,
}
