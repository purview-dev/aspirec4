namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>
/// How faithfully a <c>DbContext</c>'s shape was recovered.
/// </summary>
/// <remarks>
/// The ER diagram is drawn from two sources. When a service project references the
/// <c>AspireC4.EntityFrameworkCore</c> analyzer, a source-generated descriptor carries the full model,
/// including configuration applied fluently in <c>OnModelCreating</c>. When it does not — the usual
/// case for a shared or third-party assembly the AppHost does not own — the model is recovered from
/// the compiled assembly's metadata instead, which cannot see anything that only exists in a method
/// body. This records which happened so a partial model is never mistaken for a complete one.
/// </remarks>
public enum EfModelFidelity
{
	/// <summary>
	/// The model came from a source-generated descriptor and includes fluent configuration such as
	/// renamed tables and columns, default schemas, and ignored properties.
	/// </summary>
	Full,

	/// <summary>
	/// The model was recovered from assembly metadata. Convention-based mapping and data annotations
	/// are represented; configuration applied only in <c>OnModelCreating</c> is not.
	/// </summary>
	Approximate,
}
