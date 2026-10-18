using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Models;

/// <summary>
/// A value-equatable stand-in for a <see cref="Diagnostic"/>, so diagnostics can travel through the
/// incremental pipeline without defeating its caching.
/// </summary>
sealed record EfDiagnosticInfo(string Id, string Argument, string? FilePath, int Start, int Length)
	: IEquatable<EfDiagnosticInfo>
{
	/// <summary>Captures a descriptor and location as an equatable record.</summary>
	public static EfDiagnosticInfo Create(DiagnosticDescriptor descriptor, string argument, Location? location)
	{
		var span = location?.SourceSpan;

		return new EfDiagnosticInfo(
			descriptor.Id,
			argument,
			location?.SourceTree?.FilePath,
			span?.Start ?? 0,
			span?.Length ?? 0
		);
	}

	/// <summary>Rehydrates a real <see cref="Diagnostic"/> for reporting.</summary>
	public Diagnostic ToDiagnostic(DiagnosticDescriptor descriptor) =>
		Diagnostic.Create(descriptor, Location.None, Argument);
}
