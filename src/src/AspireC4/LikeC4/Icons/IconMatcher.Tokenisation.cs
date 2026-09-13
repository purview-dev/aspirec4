namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

static partial class IconMatcher
{
	static string[] Tokenize(string? value)
	{
		var normalized = NormalizeForIconLookup(value);
		if (string.IsNullOrEmpty(normalized))
			return [];

		var rawTokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		// Merge "java" + "script" bigrams into "javascript" so that the class name
		// "JavaScriptInstallerResource" (Aspire.Hosting.JavaScript namespace) does not produce
		// a "java" token that exactly matches tech:java.  The "javascript" alias redirects to
		// "node", yielding tech:nodejs instead.
		return
		[
			.. MergeJavaScriptBigrams(rawTokens).Select(t => TokenAliases.TryGetValue(t, out var alias) ? alias : t),
		];
	}

	static IEnumerable<string> MergeJavaScriptBigrams(string[] tokens)
	{
		for (var i = 0; i < tokens.Length; i++)
		{
			if (tokens[i] == "java" && i + 1 < tokens.Length && tokens[i + 1] == "script")
			{
				yield return "javascript";
				i++; // skip "script"
			}
			else
			{
				yield return tokens[i];
			}
		}
	}

	// Splits PascalCase / kebab-case / dotted strings into lowercase space-delimited tokens.
	// E.g. "AzureRedisCacheResource" → "azure redis cache resource"
	//      "Azure.Redis"             → "azure redis"
	//      "library/postgres:latest" → "library postgres latest"
	//      "RabbitMQContainerResource" → "rabbit mq container resource"  (uppercase-run boundary)
	//      "MySQLDatabase"            → "my sql database"
	static string NormalizeForIconLookup(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		System.Text.StringBuilder sb = new(value.Length * 2);
		var previousWasSeparator = true;

		for (var i = 0; i < value.Length; i++)
		{
			var current = value[i];

			if (
				i > 0
				&& char.IsUpper(current)
				&& !previousWasSeparator
				&& (
					// Simple CamelCase: lowercase/digit → uppercase (e.g. "camelCase", "v2Beta")
					char.IsLower(value[i - 1])
					|| char.IsDigit(value[i - 1])
					// Uppercase-run boundary: last uppercase before a new lowercase word
					// (e.g. "RabbitMQContainer" → "Rabbit", "MQ", "Container"
					//        "MySQLDatabase"     → "My", "SQL", "Database")
					|| (char.IsUpper(value[i - 1]) && i + 1 < value.Length && char.IsLower(value[i + 1]))
				)
			)
			{
				sb.Append(' ');
				previousWasSeparator = true;
			}

			if (char.IsLetterOrDigit(current))
			{
				sb.Append(char.ToLowerInvariant(current));
				previousWasSeparator = false;
				continue;
			}

			if (!previousWasSeparator)
			{
				sb.Append(' ');
				previousWasSeparator = true;
			}
		}

		return sb.ToString().Trim();
	}
}
