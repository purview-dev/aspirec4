using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;

/// <summary>
/// The diagnostics emitted by the EF Core model generator. IDs continue the <c>ASPIREC4</c> range
/// used by the registry validator and are tracked in <c>AnalyzerReleases.Unshipped.md</c>.
/// </summary>
public static class DiagnosticLibrary
{
	const string Category = "AspireC4.EntityFrameworkCore";

	/// <summary>ASPIREC4008 — a DbContext was found but no entity types could be discovered.</summary>
	public static readonly DiagnosticDescriptor NoEntityTypesDiscovered = new(
		"ASPIREC4008",
		"No entity types discovered",
		"'{0}' derives from DbContext but no entity types were discovered, so it contributes nothing to the ER diagram",
		Category,
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Expose entities as DbSet<T> properties or configure them through modelBuilder.Entity<T>()."
	);

	/// <summary>ASPIREC4009 — an entity type has no discoverable primary key.</summary>
	public static readonly DiagnosticDescriptor NoPrimaryKeyDiscovered = new(
		"ASPIREC4009",
		"No primary key discovered",
		"No primary key could be determined statically for '{0}', so its ER diagram row will not be marked as a key",
		Category,
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Apply [Key], or follow the Id / <Type>Id naming convention, so the key can be resolved."
	);

	/// <summary>ASPIREC4010 — model configuration that cannot be interpreted by static analysis.</summary>
	public static readonly DiagnosticDescriptor UninterpretableConfiguration = new(
		"ASPIREC4010",
		"Model configuration could not be interpreted statically",
		"'{0}' could not be interpreted by static analysis, so the generated ER diagram may be incomplete",
		Category,
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "AspireC4 reads the EF model from source. Configuration applied indirectly — for example "
			+ "through ApplyConfigurationsFromAssembly — is not visible to it."
	);
}
