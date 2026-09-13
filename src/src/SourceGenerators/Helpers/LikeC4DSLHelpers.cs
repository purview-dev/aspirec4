using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aspire.Hosting.AspireC4.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

static class LikeC4DSLHelpers
{
	/// <summary>
	/// Line-level patterns safe to apply globally. In LikeC4 DSL, these token sequences only
	/// appear inside <c>specification { }</c> blocks.
	/// </summary>
	static readonly Regex TagLinePattern = new(
		@"^\s*tag\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	static readonly Regex ElementKindLinePattern = new(
		@"^\s*element\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	static readonly Regex RelationshipKindLinePattern = new(
		@"^\s*relationship\s+([\w][\w-]*)",
		RegexOptions.Multiline | RegexOptions.Compiled
	);

	public static bool IsDSLFile(string path) =>
		path.EndsWith(".c4", StringComparison.OrdinalIgnoreCase)
		|| path.EndsWith(".likec4", StringComparison.OrdinalIgnoreCase);

	public static DSLDefinitions ParseDSLFile(AdditionalText file, CancellationToken ct) =>
		ExtractSpecificationItems(file.GetText(ct)?.ToString() ?? string.Empty);

	/// <summary>
	/// Extracts declared tags, element kinds, and relationship kinds from a LikeC4 DSL file.
	/// Exposed as <see langword="internal"/> for direct unit-testing.
	/// </summary>
	internal static DSLDefinitions ExtractSpecificationItems(string text)
	{
		return new(
			ExtractMatches(TagLinePattern, text),
			ExtractMatches(ElementKindLinePattern, text),
			ExtractMatches(RelationshipKindLinePattern, text)
		);
	}

	static ImmutableArray<string> ExtractMatches(Regex pattern, string text)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (Match m in pattern.Matches(text))
			builder.Add(m.Groups[1].Value);

		return builder.ToImmutable();
	}

	public static DSLDefinitions MergeDSLDefinitions(ImmutableArray<DSLDefinitions> parsed)
	{
		if (parsed.IsEmpty)
			return DSLDefinitions.Empty;

		var tags = ImmutableArray.CreateBuilder<string>();
		var elementKinds = ImmutableArray.CreateBuilder<string>();
		var relationshipKinds = ImmutableArray.CreateBuilder<string>();

		foreach (var d in parsed)
		{
			tags.AddRange(d.Tags);
			elementKinds.AddRange(d.ElementKinds);
			relationshipKinds.AddRange(d.RelationshipKinds);
		}

		return new(tags.ToImmutable(), elementKinds.ToImmutable(), relationshipKinds.ToImmutable());
	}
}
