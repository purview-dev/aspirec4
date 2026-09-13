namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

/// <summary>
/// Matches resource names and types to LikeC4 icon IDs using token-overlap scoring
/// against the bundled icon manifest generated from the LikeC4 GitHub repository.
/// </summary>
/// <remarks>
/// The manifest is loaded once from embedded resources and cached for the lifetime
/// of the process. Refresh it by running <c>just refresh-icons</c>
/// (or <c>just refresh-icons</c>) and rebuilding the project.
/// </remarks>
static partial class IconMatcher
{
	const float MinScore = 0.35f;
	const float ExactMatchScore = 1.0f;
	const float ContainmentMatchScore = 0.8f;

	// Minimum token length for containment scoring.  Short tokens (< 3 chars) like "mq", "db",
	// "go", and "js" are excluded from the effective denominator in BestMatch so they don't
	// dilute scores when mixed with longer tokens.  They can still contribute to matchSum
	// via exact match (e.g. a resource named "go" scores 1.0 for tech:go).
	const int MinContainmentLength = 3;

	static readonly Lazy<LikeC4IconManifest?> Manifest = new(
		LoadManifest,
		LazyThreadSafetyMode.ExecutionAndPublication
	);

	// Cloud collection names and the keyword tokens that identify a resource as belonging there.
	static readonly (string Collection, HashSet<string> Markers)[] CloudCollections =
	[
		("azure", new HashSet<string>(StringComparer.Ordinal) { "azure" }),
		("aws", new HashSet<string>(StringComparer.Ordinal) { "aws", "amazon" }),
		("gcp", new HashSet<string>(StringComparer.Ordinal) { "gcp", "google" }),
	];

	// Penalty multiplier applied when a query token is found inside an icon token but not at
	// the start (infix/suffix containment is weaker evidence than prefix containment).
	const float InfixPenalty = 0.75f;

	// Tokens that are never meaningful discriminators for icon matching (appear in .NET class
	// names, Docker image paths, or container tags but not in icon names).

#pragma warning disable IDE0028 // Simplify collection initialization
	static readonly HashSet<string> QueryStopTokens = new(StringComparer.Ordinal)
#pragma warning restore IDE0028 // Simplify collection initialization
	{
		"resource", // e.g. "AzureRedisResource" → strip "resource"
		"library", // e.g. "library/redis" Docker image namespace
		"latest", // e.g. ":latest" Docker image tag
		"app", // e.g. "NodeAppResource" → strip "app"
		"installer", // e.g. "node-app-installer" → installer is not part of the icon name
		"aspire", // e.g. "Aspire.Hosting.Azure.*" → namespace token, not an icon category
		"hosting", // e.g. "Aspire.Hosting.Azure.*" → namespace token, not an icon category
		"container", // e.g. "ContainerResource" class name → strip generic .NET Aspire term
		"application", // e.g. "Aspire.Hosting.ApplicationModel" namespace → strip namespace segment
		"model", // e.g. "ApplicationModel" → strip namespace segment
		"alpine", // Docker image variant tag (e.g. "postgres:16-alpine" → strip base image name)
		"slim", // Docker image variant tag (e.g. "debian:slim")
		"database", // e.g. "MySQLDatabaseResource" → structural noise, not part of icon name
	};

	// Canonical form for tokens that normalise to an ambiguous short string.
	// ".NET"        → token "net"        → preferred canonical icon token "dotnet".
	// "JavaScript"  → tokens "java"+"script" (CamelCase split) merged to "javascript"
	//               → preferred canonical icon token "node" (avoids matching tech:java).
	static readonly Dictionary<string, string> TokenAliases = new(StringComparer.Ordinal)
	{
		["net"] = "dotnet",
		["javascript"] = "node",
	};

	/// <summary>
	/// Tries to infer a LikeC4 icon ID from the supplied string candidates.
	/// </summary>
	/// <param name="candidates">
	/// Strings to score, in priority order (technology label, inferred technology, resource type,
	/// class name, resource name). <see langword="null"/> entries are silently skipped.
	/// </param>
	/// <returns>
	/// A LikeC4 icon string such as <c>tech:redis</c> or <c>azure:azure-managed-redis</c>,
	/// or <see langword="null"/> if no confident match was found.
	/// </returns>
	public static string? TryInferIcon(string?[] candidates)
	{
		var manifest = Manifest.Value;
		if (manifest is null)
		{
			return null;
		}

		var allCloudMarkers = CloudCollections.SelectMany(static c => c.Markers).ToHashSet(StringComparer.Ordinal);

		// Phase 1: cloud collections — first-above-threshold in candidate priority order.
		// We process candidates in the order they were supplied so that high-priority signals
		// (e.g. inferred technology, hidden Azure resource type name) take precedence over
		// low-priority ones (e.g. plain resource name).  Within each candidate we check every
		// cloud collection and return the first icon that scores above MinScore.  Richer
		// candidates (more tokens, e.g. "AzurePostgresFlexibleServerResource") yield more
		// discriminative queries than terse ones (e.g. "azure-postgres") and must not be
		// overridden by the higher raw scores that terse candidates can produce for ambiguous
		// icons.
		foreach (var candidate in candidates)
		{
			var tokens = Tokenize(candidate);
			if (tokens.Length == 0)
			{
				continue;
			}

			foreach (var (collection, markers) in CloudCollections)
			{
				if (!manifest.Icons.TryGetValue(collection, out var collectionIcons))
				{
					continue;
				}

				// Skip candidates that don't contain any marker for this collection.
				if (!tokens.Any(markers.Contains))
				{
					continue;
				}

				// Remove cloud markers, generic stop-tokens, and pure numeric tokens.
				// Deduplicate to prevent repeated alias tokens from inflating scores.
				var queryTokens = tokens
					.Where(t => !markers.Contains(t) && !QueryStopTokens.Contains(t) && !IsPurelyNumeric(t))
					.Distinct()
					.ToArray();

				if (queryTokens.Length == 0)
				{
					continue;
				}

				// Cloud icons have verbose, category-structured names (e.g. "azure-database-postgre-sql-server").
				// Skipping the unmatched-icon-token penalty keeps the denominator predictable for these
				// long icon names.  We return as soon as we find a confident match.
				var (score, icon) = BestMatch(
					queryTokens,
					collectionIcons,
					collection,
					penalizeUnmatchedIconTokens: false
				);
				if (score >= MinScore)
				{
					return icon;
				}
			}
		}

		// Phase 2: tech collection — collect the best-scoring icon across all non-cloud candidates.
		if (!manifest.Icons.TryGetValue("tech", out var techIcons))
		{
			return null;
		}

		var bestTechScore = 0f;
		var bestTechIcon = string.Empty;

		foreach (var candidate in candidates)
		{
			var tokens = Tokenize(candidate);
			if (tokens.Length == 0)
			{
				continue;
			}

			// Skip candidates already handled by the cloud phase.
			if (tokens.Any(allCloudMarkers.Contains))
			{
				continue;
			}

			// Remove stop-tokens and pure numeric tokens; deduplicate.
			var queryTokens = tokens
				.Where(t => !QueryStopTokens.Contains(t) && !IsPurelyNumeric(t))
				.Distinct()
				.ToArray();

			if (queryTokens.Length == 0)
			{
				continue;
			}

			var (score, icon) = BestMatch(queryTokens, techIcons, "tech");
			if (score > bestTechScore)
			{
				bestTechScore = score;
				bestTechIcon = icon;
			}
		}

		return bestTechScore >= MinScore ? bestTechIcon : null;
	}

	// Returns true when the token consists entirely of digits (e.g. "16", "3", "2022").
	// Such tokens originate from Docker image version tags (e.g. "postgres:16-alpine") and
	// are never part of an icon name.
	static bool IsPurelyNumeric(string t) => t.Length > 0 && t.All(char.IsDigit);
}
