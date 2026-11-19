using System.Text.RegularExpressions;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// The architecture every sample AppHost must draw: the C# <c>TestAppHost</c> on Aspire 17, its Aspire 13
/// twin, and the TypeScript, Go and Python samples under <c>samples/</c>.
/// </summary>
/// <remarks>
/// <para>
/// One definition, asserted against each AppHost's generated <c>model.gen.c4</c>: the C# AppHosts through
/// <c>SampleAppHostContractTests</c> in the integration suites, and the polyglot samples through
/// <c>AspireC4.AppHosts.IntegrationTests</c>. It is what keeps the five samples, and the AspireC4 API
/// they call, consistent: a sample that drifts, or a binding that cannot express a call, fails here.
/// </para>
/// <para>
/// It covers what AspireC4 draws from the sample's own calls — element ids, kinds and labels, the
/// labelled relationships and their kinds, and what must be hidden — not relationships Aspire itself
/// adds (such as <c>rediscommander -&gt; azure_redis</c>), whose labels change between Aspire versions.
/// </para>
/// </remarks>
public static class SampleAppHostContract
{
	/// <summary>An element the diagram must contain.</summary>
	/// <param name="Id">The DSL identifier.</param>
	/// <param name="Label">The element's title.</param>
	/// <param name="Kinds">Accepted element kinds; more than one only where a runtime switch changes it.</param>
	public sealed record ExpectedElement(string Id, string Label, IReadOnlyList<string> Kinds);

	/// <summary>A relationship the diagram must contain.</summary>
	public sealed record ExpectedRelationship(string Source, string Kind, string Target, string Label);

	/// <summary>The elements every sample draws.</summary>
	public static IReadOnlyList<ExpectedElement> Elements { get; } =
	[
		// The LikeC4 server itself: a container, or an executable under WithLocalCLI (the e2e runs).
		new("aspirec4", "AspireC4", ["container", "executable"]),
		new("data_service", "Sample Data Service", ["component"]),
		new("azure_redis", "Azure Redis", ["container"]),
		new("rediscommander", "Redis Commander", ["container"]),
		new("azure_postgres", "Azure Postgres", ["container"]),
		new("pgweb", "PgWeb", ["container"]),
		new("local_redis", "local-redis", ["container"]),
		new("local_postgres", "local-postgres", ["container"]),
		new("node_app", "Sample Node App", ["executable"]),
	];

	/// <summary>The labelled relationships every sample declares with <c>WithLikeC4ConnectionReference</c>.</summary>
	public static IReadOnlyList<ExpectedRelationship> Relationships { get; } =
	[
		new("node_app", "RESP", "azure_redis", "Caches sessions"),
		new("node_app", "RESP", "local_redis", "Caches  sessions (local)"),
		new("node_app", "tcp-ip", "azure_postgres", "Persists data"),
		new("node_app", "tcp-ip", "local_postgres", "Persists data (local)"),
		new("local_postgres", "tcp-ip", "azure_postgres", "syncs with"),
		new("local_redis", "RESP", "azure_redis", "syncs with"),
	];

	/// <summary>
	/// Elements that must not appear: parameters are excluded by type, and the Azure provisioning
	/// environment by name, both by default.
	/// </summary>
	public static IReadOnlyList<string> AbsentElements { get; } =
	["azure_environment", "testing_resource_parameter", "testing_resource_parameter_from_config"];

	/// <summary>Checks a generated model against the contract.</summary>
	/// <param name="modelDsl">The text of a generated <c>model.gen.c4</c>.</param>
	/// <returns>One message per violation; empty when the model satisfies the contract.</returns>
	public static IReadOnlyList<string> Verify(string modelDsl)
	{
		var model = GeneratedLikeC4Model.Parse(modelDsl);
		List<string> violations = [];

		foreach (var expected in Elements)
		{
			if (!model.Elements.TryGetValue(expected.Id, out var actual))
			{
				violations.Add($"missing element '{expected.Id}' ('{expected.Label}')");
				continue;
			}

			if (!expected.Kinds.Contains(actual.Kind, StringComparer.Ordinal))
				violations.Add(
					$"element '{expected.Id}' is a '{actual.Kind}', expected {string.Join(" or ", expected.Kinds)}"
				);

			if (!string.Equals(actual.Label, expected.Label, StringComparison.Ordinal))
				violations.Add($"element '{expected.Id}' is labelled '{actual.Label}', expected '{expected.Label}'");
		}

		foreach (var expected in Relationships)
		{
			if (!model.Relationships.Contains(new(expected.Source, expected.Kind, expected.Target, expected.Label)))
				violations.Add(
					$"missing relationship {expected.Source} .{expected.Kind} {expected.Target} '{expected.Label}'"
				);
		}

		foreach (var absent in AbsentElements)
		{
			if (model.Elements.ContainsKey(absent))
				violations.Add($"element '{absent}' must not be drawn");
		}

		return violations;
	}
}

/// <summary>
/// The parts of a generated LikeC4 model the sample contract reads, parsed from the DSL text that
/// AspireC4's generator writes. Not a general LikeC4 parser.
/// </summary>
public sealed partial class GeneratedLikeC4Model
{
	/// <summary>An element: its kind and title.</summary>
	public sealed record Element(string Kind, string Label);

	/// <summary>A relationship. <see cref="Kind"/> is <see langword="null"/> for a plain <c>-&gt;</c>.</summary>
	public sealed record Relationship(string Source, string? Kind, string Target, string? Label);

	GeneratedLikeC4Model(IReadOnlyDictionary<string, Element> elements, IReadOnlySet<Relationship> relationships)
	{
		Elements = elements;
		Relationships = relationships;
	}

	/// <summary>Elements by DSL identifier. Nested elements are keyed by their own identifier.</summary>
	public IReadOnlyDictionary<string, Element> Elements { get; }

	/// <summary>Every relationship in the model.</summary>
	public IReadOnlySet<Relationship> Relationships { get; }

	/// <summary>Parses the elements and relationships out of a generated model.</summary>
	public static GeneratedLikeC4Model Parse(string modelDsl)
	{
		ArgumentNullException.ThrowIfNull(modelDsl);

		Dictionary<string, Element> elements = new(StringComparer.Ordinal);
		HashSet<Relationship> relationships = [];

		foreach (var line in modelDsl.Split('\n'))
		{
			var element = ElementPattern().Match(line);
			if (element.Success)
			{
				elements[element.Groups["id"].Value] = new(
					element.Groups["kind"].Value,
					Unescape(element.Groups["label"].Value)
				);
				continue;
			}

			var relationship = RelationshipPattern().Match(line);
			if (relationship.Success)
			{
				relationships.Add(
					new(
						LastSegment(relationship.Groups["source"].Value),
						relationship.Groups["kind"].Success ? relationship.Groups["kind"].Value : null,
						LastSegment(relationship.Groups["target"].Value),
						relationship.Groups["label"].Success ? Unescape(relationship.Groups["label"].Value) : null
					)
				);
			}
		}

		return new(elements, relationships);
	}

	static string LastSegment(string fqn) => fqn[(fqn.LastIndexOf('.') + 1)..];

	static string Unescape(string value) => value.Replace("\\'", "'", StringComparison.Ordinal);

	// `  azure_redis = container 'Azure Redis' {`
	[GeneratedRegex(@"^\s*(?<id>[A-Za-z_]\w*)\s*=\s*(?<kind>[A-Za-z_][\w-]*)\s+'(?<label>(?:[^'\\]|\\.)*)'")]
	private static partial Regex ElementPattern();

	// `  node_app .RESP azure_redis 'Caches sessions' {` or `  rediscommander -> azure_redis 'Manages'`
	[GeneratedRegex(
		@"^\s*(?<source>[A-Za-z_][\w.]*)\s+(?:\.(?<kind>[A-Za-z_][\w-]*)|->)\s+(?<target>[A-Za-z_][\w.]*)(?:\s+'(?<label>(?:[^'\\]|\\.)*)')?"
	)]
	private static partial Regex RelationshipPattern();
}
