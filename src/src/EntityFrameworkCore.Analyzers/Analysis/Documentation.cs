using Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Analysis;

/// <summary>Reads the XML documentation summary of an entity type or a mapped property.</summary>
/// <remarks>
/// <para>
/// Roslyn hands back parsed documentation only when the compilation asks for it — that is, when the
/// project generates a documentation file. Most service projects do not, and then the comments are
/// still in the syntax tree, lexed as ordinary <c>///</c> comments. Both are read, so a description
/// does not depend on a project setting that has nothing to do with the diagram.
/// </para>
/// <para>
/// A symbol from a referenced assembly has no syntax; its documentation comes from the reference's own
/// documentation file, which Roslyn resolves for <see cref="ISymbol.GetDocumentationCommentXml"/>.
/// </para>
/// </remarks>
static class Documentation
{
	/// <summary>The type's summary.</summary>
	public static string? OfType(INamedTypeSymbol type, CancellationToken cancellationToken) =>
		XmlDocumentationText.Summary(Xml(type, cancellationToken));

	/// <summary>
	/// The property's summary, or — for a positional record property, which is documented on the record
	/// — the record's <c>&lt;param&gt;</c> for it.
	/// </summary>
	public static string? OfProperty(IPropertySymbol property, CancellationToken cancellationToken)
	{
		if (XmlDocumentationText.Summary(Xml(property, cancellationToken)) is { } summary)
		{
			return summary;
		}

		return property.ContainingType is { } type
			? XmlDocumentationText.Param(Xml(type, cancellationToken), property.Name)
			: null;
	}

	static string? Xml(ISymbol symbol, CancellationToken cancellationToken)
	{
		var xml = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);

		if (!string.IsNullOrWhiteSpace(xml))
		{
			return xml;
		}

		foreach (var reference in symbol.DeclaringSyntaxReferences)
		{
			// A positional record property's syntax is the primary-constructor parameter, which carries
			// no documentation of its own.
			if (reference.GetSyntax(cancellationToken) is ParameterSyntax)
			{
				continue;
			}

			List<string> lines = [];

			foreach (var trivia in reference.GetSyntax(cancellationToken).GetLeadingTrivia())
			{
				if (
					trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
					|| trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
				)
				{
					// The structured form holds every line of the comment, each with its /// exterior.
					lines.AddRange(trivia.ToFullString().Split('\n'));
				}
			}

			if (XmlDocumentationText.FromCommentLines(lines) is { } comment)
			{
				return comment;
			}
		}

		return null;
	}
}
