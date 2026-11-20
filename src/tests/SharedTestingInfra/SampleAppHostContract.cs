using System.Text.Json;
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
/// labelled relationships and their kinds, tags, technology, links, metadata, groups, and what must be
/// hidden — not relationships Aspire itself adds (such as <c>rediscommander -&gt; azure_redis</c>), whose
/// labels change between Aspire versions.
/// </para>
/// <para>
/// <see cref="VerifyErModel"/> does the same for the ER diagram's <c>schema.gen.json</c>: the contexts it
/// finds and the styling every sample applies (<c>SampleErdStyling.WithSharedSampleStyling</c> in C#).
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

	/// <summary>What one element must carry beyond its kind and label.</summary>
	/// <param name="Id">The DSL identifier.</param>
	/// <param name="Technology">The element's technology, or <see langword="null"/> to not check it.</param>
	/// <param name="Tags">Tags the element must carry, without the <c>#</c>.</param>
	/// <param name="Links">Links the element must carry, as <c>url 'title'</c>.</param>
	/// <param name="Metadata">Metadata entries the element must carry, as <c>key 'value'</c>.</param>
	public sealed record ExpectedDetails(
		string Id,
		string? Technology = null,
		IReadOnlyList<string>? Tags = null,
		IReadOnlyList<string>? Links = null,
		IReadOnlyList<string>? Metadata = null
	);

	/// <summary>A group the generated view must draw, and the elements it must include.</summary>
	public sealed record ExpectedGroup(string Title, IReadOnlyList<string> Members);

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

	/// <summary>What the sample's own <c>WithTag</c>, <c>WithLink</c>, <c>WithMetadata</c> and technology calls add.</summary>
	public static IReadOnlyList<ExpectedDetails> Details { get; } =
	[
		new("azure_redis", Technology: "Azure Redis", Links: ["https://redis.io/ 'Learn more about Redis'"]),
		new(
			"azure_postgres",
			Metadata:
			[
				"Azure_SKU 'Flexible Server x 1 (NON-PROD)'",
				"Azure_SKU 'Flexible Server x 2 (PROD)'",
				"Use_Case 'Primary data store'",
			]
		),
		new("local_redis", Technology: "Redis", Tags: ["local-dev"]),
		new("local_postgres", Technology: "PostgreSQL", Tags: ["local-dev"]),
	];

	/// <summary>The groups every sample declares with <c>WithLikeC4Group</c>.</summary>
	public static IReadOnlyList<ExpectedGroup> Groups { get; } =
	[new("Local Dev/ Sync Group", ["local_redis", "local_postgres"])];

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

		foreach (var expected in Details)
		{
			if (!model.Elements.TryGetValue(expected.Id, out var actual))
				continue; // Already reported as missing.

			if (expected.Technology is { } technology && actual.Technology != technology)
				violations.Add(
					$"element '{expected.Id}' has technology '{actual.Technology ?? "(none)"}', expected '{technology}'"
				);

			Require(violations, expected.Id, "tag", expected.Tags, actual.Tags);
			Require(violations, expected.Id, "link", expected.Links, actual.Links);
			Require(violations, expected.Id, "metadata", expected.Metadata, actual.Metadata);
		}

		foreach (var expected in Groups)
		{
			if (!model.Groups.TryGetValue(expected.Title, out var members))
			{
				violations.Add($"missing group '{expected.Title}'");
				continue;
			}

			foreach (var member in expected.Members.Where(m => !members.Contains(m)))
				violations.Add($"group '{expected.Title}' does not include '{member}'");
		}

		return violations;
	}

	static void Require(
		List<string> violations,
		string id,
		string what,
		IReadOnlyList<string>? expected,
		IReadOnlyList<string> actual
	)
	{
		foreach (var value in expected ?? [])
		{
			if (!actual.Contains(value, StringComparer.Ordinal))
				violations.Add($"element '{id}' is missing {what} {value}");
		}
	}

	/// <summary>The <c>DbContext</c> types the sample data service contributes.</summary>
	public static IReadOnlyList<string> ErContexts { get; } =
	["AuditDbContext", "IdentityDbContext", "SalesDbContext", "ValueObjectDbContext", "BlogDbContext"];

	/// <summary>The context palette every sample sets, light colours in order.</summary>
	public static IReadOnlyList<string> ErPalette { get; } =
	["#2563eb", "#16a34a", "#d97706", "#9333ea", "#e11d48", "#0d9488"];

	/// <summary>Checks a generated ER model (<c>schema.gen.json</c>) against the contract.</summary>
	/// <param name="modelJson">The text of the interactive renderer's <c>schema.gen.json</c>.</param>
	/// <returns>One message per violation; empty when the model satisfies the contract.</returns>
	public static IReadOnlyList<string> VerifyErModel(string modelJson)
	{
		ArgumentNullException.ThrowIfNull(modelJson);

		List<string> violations = [];
		using var document = JsonDocument.Parse(modelJson);
		var root = document.RootElement;

		var contexts = root.GetProperty("contexts")
			.EnumerateArray()
			.Select(c => c.GetProperty("name").GetString())
			.ToHashSet();
		foreach (var context in ErContexts.Where(c => !contexts.Contains(c)))
			violations.Add($"ER diagram is missing context '{context}'");

		if (!root.TryGetProperty("presentation", out var presentation))
		{
			violations.Add("ER diagram has no presentation");
			return violations;
		}

		Expect(violations, presentation, "layout.direction", "RIGHT");
		Expect(violations, presentation, "layout.layerSpacing", "160");
		Expect(violations, presentation, "layout.edgeStyle", "smoothstep");
		Expect(violations, presentation, "viewer.animation", "slow");
		Expect(violations, presentation, "viewer.legend", "true");
		Expect(violations, presentation, "viewer.descriptions", "true");
		Expect(violations, presentation, "styles.tables.Orders.badge", "SALES");
		Expect(violations, presentation, "styles.tables.Orders.accent.light", "#dc2626");
		Expect(violations, presentation, "styles.tables.Orders.accent.dark", "#f87171");
		Expect(violations, presentation, "styles.columns.Orders.Total.accent.light", "#16a34a");

		var palette = presentation.TryGetProperty("palette", out var colours)
			? colours.EnumerateArray().Select(c => c.GetProperty("light").GetString()).ToArray()
			: [];
		if (!palette.SequenceEqual(ErPalette))
			violations.Add($"ER palette is [{string.Join(", ", palette)}], expected [{string.Join(", ", ErPalette)}]");

		return violations;
	}

	static void Expect(List<string> violations, JsonElement root, string path, string expected)
	{
		var current = root;
		foreach (var segment in path.Split('.'))
		{
			if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
			{
				violations.Add($"ER presentation has no '{path}', expected '{expected}'");
				return;
			}
		}

		var actual = current.ValueKind switch
		{
			JsonValueKind.String => current.GetString(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			_ => current.GetRawText(),
		};

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			violations.Add($"ER presentation '{path}' is '{actual}', expected '{expected}'");
	}
}

/// <summary>
/// The parts of a generated LikeC4 model the sample contract reads, parsed from the DSL text that
/// AspireC4's generator writes. Not a general LikeC4 parser.
/// </summary>
public sealed partial class GeneratedLikeC4Model
{
	/// <summary>An element: its kind and title, and what its body carries.</summary>
	public sealed record Element(string Kind, string Label)
	{
		/// <summary>The element's technology, if it declares one.</summary>
		public string? Technology { get; internal set; }

		/// <summary>The element's tags, without the <c>#</c>.</summary>
		public List<string> Tags { get; } = [];

		/// <summary>The element's links, as <c>url 'title'</c>.</summary>
		public List<string> Links { get; } = [];

		/// <summary>The element's metadata entries, as <c>key 'value'</c>.</summary>
		public List<string> Metadata { get; } = [];
	}

	/// <summary>A relationship. <see cref="Kind"/> is <see langword="null"/> for a plain <c>-&gt;</c>.</summary>
	public sealed record Relationship(string Source, string? Kind, string Target, string? Label);

	GeneratedLikeC4Model(
		IReadOnlyDictionary<string, Element> elements,
		IReadOnlySet<Relationship> relationships,
		IReadOnlyDictionary<string, IReadOnlyList<string>> groups
	)
	{
		Elements = elements;
		Relationships = relationships;
		Groups = groups;
	}

	/// <summary>The view's groups, by title, with the identifiers each one includes.</summary>
	public IReadOnlyDictionary<string, IReadOnlyList<string>> Groups { get; }

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
		Dictionary<string, IReadOnlyList<string>> groups = new(StringComparer.Ordinal);

		// Body lines belong to the element most recently opened; a relationship or a view ends it. The
		// generator writes one property per line, which is all this relies on.
		Element? current = null;
		string? group = null;
		var inMetadata = false;

		foreach (var line in modelDsl.Split('\n'))
		{
			var element = ElementPattern().Match(line);
			if (element.Success)
			{
				current = elements[element.Groups["id"].Value] = new(
					element.Groups["kind"].Value,
					Unescape(element.Groups["label"].Value)
				);
				inMetadata = false;
				continue;
			}

			var groupTitle = GroupPattern().Match(line);
			if (groupTitle.Success)
			{
				group = Unescape(groupTitle.Groups["title"].Value);
				current = null;
				continue;
			}

			var include = IncludePattern().Match(line);
			if (include.Success && group is not null)
			{
				groups[group] =
				[
					.. groups.GetValueOrDefault(group) ?? [],
					.. include
						.Groups["members"]
						.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
				];
				// The generator writes a group's members on one include line, and a later include belongs to the view.
				group = null;
				continue;
			}

			if (current is not null && ReadBody(current, line, ref inMetadata))
			{
				continue;
			}

			var relationship = RelationshipPattern().Match(line);
			if (relationship.Success)
			{
				current = null;
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

		return new(elements, relationships, groups);
	}

	/// <summary>Records a body line of <paramref name="element"/>, returning whether it was one.</summary>
	static bool ReadBody(Element element, string line, ref bool inMetadata)
	{
		var trimmed = line.Trim();

		if (inMetadata)
		{
			if (trimmed == "}")
			{
				inMetadata = false;
			}
			else if (trimmed.Length > 0)
			{
				element.Metadata.Add(trimmed);
			}

			return true;
		}

		if (trimmed == "metadata {")
		{
			inMetadata = true;
			return true;
		}

		if (trimmed.StartsWith('#'))
		{
			element.Tags.AddRange(
				trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(tag => tag.TrimStart('#'))
			);
			return true;
		}

		var technology = TechnologyPattern().Match(trimmed);
		if (technology.Success)
		{
			element.Technology = Unescape(technology.Groups["value"].Value);
			return true;
		}

		if (trimmed.StartsWith("link ", StringComparison.Ordinal))
		{
			element.Links.Add(trimmed["link ".Length..]);
			return true;
		}

		return false;
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

	//     technology 'Azure Redis'
	[GeneratedRegex(@"^technology\s+'(?<value>(?:[^'\\]|\\.)*)'$")]
	private static partial Regex TechnologyPattern();

	//     group 'Local Dev/ Sync Group' {
	[GeneratedRegex(@"^\s*group\s+'(?<title>(?:[^'\\]|\\.)*)'\s*\{")]
	private static partial Regex GroupPattern();

	//       include local_redis, local_postgres
	[GeneratedRegex(@"^\s*include\s+(?<members>[A-Za-z_][\w.]*(?:\s*,\s*[A-Za-z_][\w.]*)*)\s*$")]
	private static partial Regex IncludePattern();
}
