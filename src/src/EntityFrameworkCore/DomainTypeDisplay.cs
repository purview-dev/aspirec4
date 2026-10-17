namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// How a column shows its domain type — a value object or an enum — alongside the real database type.
/// </summary>
/// <remarks>
/// The database type is never discarded: every mode keeps it in the model, and each renderer exposes it
/// in its annotation slot when it is not the headline.
/// </remarks>
public enum DomainTypeDisplay
{
	/// <summary><c>AssetId (guid)</c>. The default.</summary>
	DomainTypeFirst,

	/// <summary><c>guid (AssetId)</c>.</summary>
	DatabaseFirst,

	/// <summary>
	/// <c>AssetId</c>. The database type is kept in the model and shown in each renderer's annotation
	/// slot, but is not the headline.
	/// </summary>
	DomainTypeOnly,

	/// <summary><c>guid</c>. The domain type is not shown.</summary>
	DatabaseOnly,
}
