using System.Text.Json.Nodes;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// The resolved styling and descriptions for one model: what every rule and callback produced for each
/// table, column and relationship, and the description each one shows.
/// </summary>
/// <remarks>
/// Resolved once per regeneration and handed to every renderer, so they agree on what is styled and a
/// callback runs once per element rather than once per format. Only elements with something set are
/// recorded, which keeps an unstyled diagram's output byte-for-byte what it was before styling existed.
/// </remarks>
sealed class ErdPresentation
{
	/// <summary>A presentation with nothing styled or described.</summary>
	public static readonly ErdPresentation Empty = new();

	readonly Dictionary<string, ErdElementStyle> _tables = new(StringComparer.Ordinal);
	readonly Dictionary<string, Dictionary<string, ErdElementStyle>> _columns = new(StringComparer.Ordinal);
	readonly Dictionary<string, ErdElementStyle> _relationships = new(StringComparer.Ordinal);
	readonly Dictionary<string, string> _tableDescriptions = new(StringComparer.Ordinal);
	readonly Dictionary<string, Dictionary<string, string>> _columnDescriptions = new(StringComparer.Ordinal);

	/// <summary>Whether any element is styled.</summary>
	public bool HasStyles => _tables.Count > 0 || _columns.Count > 0 || _relationships.Count > 0;

	/// <summary>The resolved style of a table, or <see langword="null"/> when nothing styles it.</summary>
	public ErdElementStyle? TableStyle(string table) => _tables.GetValueOrDefault(table);

	/// <summary>The resolved style of a column, or <see langword="null"/> when nothing styles it.</summary>
	public ErdElementStyle? ColumnStyle(string table, string column) =>
		_columns.TryGetValue(table, out var columns) ? columns.GetValueOrDefault(column) : null;

	/// <summary>The resolved style of a relationship, or <see langword="null"/> when nothing styles it.</summary>
	public ErdElementStyle? RelationshipStyle(EfTable dependent, EfForeignKey foreignKey) =>
		_relationships.GetValueOrDefault(RelationshipId(dependent, foreignKey));

	/// <summary>The description a table shows, or <see langword="null"/> when it has none.</summary>
	public string? TableDescription(string table) => _tableDescriptions.GetValueOrDefault(table);

	/// <summary>The description a column shows, or <see langword="null"/> when it has none.</summary>
	public string? ColumnDescription(string table, string column) =>
		_columnDescriptions.TryGetValue(table, out var columns) ? columns.GetValueOrDefault(column) : null;

	/// <summary>
	/// The identifier the interactive viewer gives a relationship's edge, so a style resolved here can be
	/// found from there.
	/// </summary>
	/// <remarks>Mirrors <c>buildEdges</c> in the viewer's <c>app.js</c>: change one, change the other.</remarks>
	public static string RelationshipId(EfTable dependent, EfForeignKey foreignKey) =>
		$"{dependent.Name}.{string.Join("_", foreignKey.Columns)}->{foreignKey.PrincipalTable}";

	/// <summary>Resolves every rule, callback and description for a model.</summary>
	public static ErdPresentation Build(EfModel model, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		var rules = (options.Styles ?? []).Where(r => r is not null).ToList();
		var styling =
			rules.Count > 0
			|| options.TableStyleCallbacks.Count > 0
			|| options.ColumnStyleCallbacks.Count > 0
			|| options.RelationshipStyleCallbacks.Count > 0;

		if (!styling && options.DescriptionSources == ErdDescriptionSources.None)
		{
			return Empty;
		}

		ErdPresentation presentation = new();

		// Table names key the viewer's nodes, so the first table of a name is the one a relationship
		// reaches, as it is in every renderer.
		Dictionary<string, EfTable> tablesByName = new(StringComparer.OrdinalIgnoreCase);

		foreach (var table in model.Contexts.SelectMany(c => c.Tables))
		{
			tablesByName.TryAdd(table.Name, table);
		}

		foreach (var context in model.Contexts)
		{
			foreach (var table in context.Tables)
			{
				presentation.Describe(table, options.DescriptionSources);

				if (!styling)
				{
					continue;
				}

				presentation.StyleTable(model, context, table, rules, options);

				foreach (var foreignKey in table.ForeignKeys)
				{
					if (tablesByName.TryGetValue(foreignKey.PrincipalTable, out var principal))
					{
						presentation.StyleRelationship(model, context, table, principal, foreignKey, rules, options);
					}
				}
			}
		}

		return presentation;
	}

	void Describe(EfTable table, ErdDescriptionSources sources)
	{
		if (Description(table.Description, table.Documentation, table.Comment, sources) is { } description)
		{
			_tableDescriptions[table.Name] = description;
		}

		foreach (var column in table.Columns)
		{
			if (Description(column.Description, column.Documentation, column.Comment, sources) is not { } text)
			{
				continue;
			}

			if (!_columnDescriptions.TryGetValue(table.Name, out var columns))
			{
				columns = new(StringComparer.Ordinal);
				_columnDescriptions[table.Name] = columns;
			}

			columns[column.Name] = text;
		}
	}

	/// <summary>
	/// The first description the enabled sources offer: an attribute, then XML documentation, then the
	/// database comment.
	/// </summary>
	internal static string? Description(
		string? attribute,
		string? documentation,
		string? comment,
		ErdDescriptionSources sources
	) =>
		Pick(attribute, sources, ErdDescriptionSources.Attributes)
		?? Pick(documentation, sources, ErdDescriptionSources.XmlDocumentation)
		?? Pick(comment, sources, ErdDescriptionSources.Comments);

	static string? Pick(string? value, ErdDescriptionSources sources, ErdDescriptionSources source) =>
		(sources & source) != 0 && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

	void StyleTable(
		EfModel model,
		EfContext context,
		EfTable table,
		List<ErdStyleRule> rules,
		AspireC4EfDiagramOptions options
	)
	{
		ErdElementStyle style = new();

		foreach (var rule in rules)
		{
			if (rule.EffectiveTarget == ErdStyleTarget.Table && MatchesTable(rule, context, table))
			{
				style.Apply(rule.Style ?? new());
			}
		}

		foreach (var callback in options.TableStyleCallbacks)
		{
			Run(() => callback(new ErdTableStyleContext(model, context, table, style)), "table", table.Name);
		}

		if (!style.IsEmpty)
		{
			_tables[table.Name] = style;
		}

		foreach (var column in table.Columns)
		{
			StyleColumn(model, context, table, column, rules, options);
		}
	}

	void StyleColumn(
		EfModel model,
		EfContext context,
		EfTable table,
		EfColumn column,
		List<ErdStyleRule> rules,
		AspireC4EfDiagramOptions options
	)
	{
		ErdElementStyle style = new();

		foreach (var rule in rules)
		{
			if (
				rule.EffectiveTarget == ErdStyleTarget.Column
				&& MatchesContext(rule, context)
				&& MatchesTableName(rule.Table, table)
				&& MatchesColumn(rule, column)
			)
			{
				style.Apply(rule.Style ?? new());
			}
		}

		foreach (var callback in options.ColumnStyleCallbacks)
		{
			Run(
				() => callback(new ErdColumnStyleContext(model, context, table, column, style)),
				"column",
				$"{table.Name}.{column.Name}"
			);
		}

		if (style.IsEmpty)
		{
			return;
		}

		if (!_columns.TryGetValue(table.Name, out var columns))
		{
			columns = new(StringComparer.Ordinal);
			_columns[table.Name] = columns;
		}

		columns[column.Name] = style;
	}

	void StyleRelationship(
		EfModel model,
		EfContext context,
		EfTable dependent,
		EfTable principal,
		EfForeignKey foreignKey,
		List<ErdStyleRule> rules,
		AspireC4EfDiagramOptions options
	)
	{
		ErdElementStyle style = new();

		foreach (var rule in rules)
		{
			if (
				rule.EffectiveTarget == ErdStyleTarget.Relationship
				&& MatchesContext(rule, context)
				&& (
					rule.Table is null
					|| MatchesTableName(rule.Table, dependent)
					|| MatchesTableName(rule.Table, principal)
				)
				&& (rule.Column is null || foreignKey.Columns.Any(c => ErdWildcard.IsMatch(rule.Column, c)))
			)
			{
				style.Apply(rule.Style ?? new());
			}
		}

		foreach (var callback in options.RelationshipStyleCallbacks)
		{
			Run(
				() =>
					callback(new ErdRelationshipStyleContext(model, context, dependent, principal, foreignKey, style)),
				"relationship",
				RelationshipId(dependent, foreignKey)
			);
		}

		if (!style.IsEmpty)
		{
			_relationships[RelationshipId(dependent, foreignKey)] = style;
		}
	}

	/// <summary>
	/// Runs a consumer's callback, naming the element it failed on — otherwise the failure surfaces as
	/// an empty diagram with no hint of which of possibly hundreds of elements caused it.
	/// </summary>
	static void Run(Action callback, string kind, string element)
	{
		try
		{
			callback();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new InvalidOperationException($"The ER diagram {kind} style callback failed for '{element}'.", ex);
		}
	}

	static bool MatchesTable(ErdStyleRule rule, EfContext context, EfTable table) =>
		MatchesContext(rule, context)
		&& MatchesTableName(rule.Table, table)
		// A table rule naming a column or a type picks out the tables that have one.
		&& ((rule.Column is null && rule.Type is null) || table.Columns.Any(c => MatchesColumn(rule, c)));

	static bool MatchesContext(ErdStyleRule rule, EfContext context) =>
		rule.Context is null
		|| ErdWildcard.IsMatch(rule.Context, context.Name)
		|| ErdWildcard.IsMatch(rule.Context, context.TypeName);

	static bool MatchesTableName(string? pattern, EfTable table) =>
		pattern is null
		|| ErdWildcard.IsMatch(pattern, table.Name)
		|| ErdWildcard.IsMatch(pattern, table.ClrTypeName)
		|| ErdWildcard.IsMatch(pattern, ShortName(table.ClrTypeName));

	static bool MatchesColumn(ErdStyleRule rule, EfColumn column) =>
		(rule.Column is null || ErdWildcard.IsMatch(rule.Column, column.Name))
		&& (rule.Type is null || TypeNames(column).Any(name => ErdWildcard.IsMatch(rule.Type, name)));

	/// <summary>Every name a column's type goes by, any of which a type pattern can match.</summary>
	static IEnumerable<string> TypeNames(EfColumn column)
	{
		yield return column.StoreType;
		yield return EfColumnTypeDisplay.DatabaseType(column);

		// decimal for decimal(18,2): a pattern names a type, and its size is a detail of the column.
		var size = column.StoreType.IndexOf('(', StringComparison.Ordinal);

		if (size > 0)
		{
			yield return column.StoreType[..size].TrimEnd();
		}

		if (!string.IsNullOrEmpty(column.DomainTypeName))
		{
			yield return column.DomainTypeName;
		}

		if (!string.IsNullOrEmpty(column.ClrTypeName))
		{
			yield return column.ClrTypeName;
			yield return ShortName(column.ClrTypeName);
		}

		if (EfColumnTypeDisplay.Marker(column) is { } marker)
		{
			yield return marker;
		}

		if (column.DomainTypeKind is { } kind)
		{
			yield return kind.ToString();
		}
	}

	static string ShortName(string typeName)
	{
		var index = typeName.LastIndexOf('.');

		return index >= 0 && index < typeName.Length - 1 ? typeName[(index + 1)..] : typeName;
	}

	/// <summary>
	/// The presentation as the interactive viewer reads it: viewer defaults, layout, palette, styles and
	/// descriptions.
	/// </summary>
	/// <remarks>
	/// Written by hand rather than serialised so the shape is explicit — <c>app.js</c> reads it field by
	/// field — and so property order, and with it the file's bytes, never depends on reflection.
	/// </remarks>
	public JsonObject ToJson(AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		JsonObject json = new()
		{
			["version"] = 1,
			["viewer"] = new JsonObject
			{
				["theme"] = Lower(options.Theme),
				["background"] = Lower(options.ViewerBackground),
				["tables"] = Lower(options.TableDisplay),
				["animation"] = Lower(options.EdgeAnimation),
				["descriptions"] = options.ShowDescriptions,
				["legend"] = options.ShowLegend,
				["tooltips"] = options.ShowTooltips,
				["transitions"] = options.AnimateTransitions,
				["hover"] = options.HighlightOnHover,
			},
			["layout"] = new JsonObject
			{
				["direction"] = options.LayoutDirection.ToString().ToUpperInvariant(),
				["nodeSpacing"] = Math.Max(0, options.NodeSpacing),
				["layerSpacing"] = Math.Max(0, options.LayerSpacing),
				["contextSpacing"] = Math.Max(0, options.ContextSpacing),
				["edgeStyle"] = options.EdgeStyle switch
				{
					ErdEdgeStyle.SmoothStep => "smoothstep",
					ErdEdgeStyle.Step => "step",
					ErdEdgeStyle.Straight => "straight",
					_ => "default",
				},
			},
		};

		var palette = (options.ContextPalette ?? []).Where(c => c is { IsEmpty: false }).ToList();

		if (palette.Count > 0)
		{
			json["palette"] = new JsonArray([.. palette.Select(Colour)]);
		}

		if (HasStyles)
		{
			json["styles"] = new JsonObject
			{
				["tables"] = Styles(_tables),
				["columns"] = Nested(_columns, Styles),
				["relationships"] = Styles(_relationships),
			};
		}

		if (_tableDescriptions.Count > 0 || _columnDescriptions.Count > 0)
		{
			json["descriptions"] = new JsonObject
			{
				["tables"] = Texts(_tableDescriptions),
				["columns"] = Nested(_columnDescriptions, Texts),
			};
		}

		return json;
	}

	/// <summary>
	/// The camel-case name the viewer reads. Every value written this way is a single word, so this is
	/// its lower-case form.
	/// </summary>
	static string Lower<T>(T value)
		where T : struct, Enum => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

	static JsonObject Styles(Dictionary<string, ErdElementStyle> styles)
	{
		JsonObject json = [];

		foreach (var (key, style) in styles.OrderBy(p => p.Key, StringComparer.Ordinal))
		{
			json[key] = Style(style);
		}

		return json;
	}

	static JsonObject Texts(Dictionary<string, string> texts)
	{
		JsonObject json = [];

		foreach (var (key, text) in texts.OrderBy(p => p.Key, StringComparer.Ordinal))
		{
			json[key] = text;
		}

		return json;
	}

	static JsonObject Nested<T>(
		Dictionary<string, Dictionary<string, T>> values,
		Func<Dictionary<string, T>, JsonObject> write
	)
	{
		JsonObject json = [];

		foreach (var (key, inner) in values.OrderBy(p => p.Key, StringComparer.Ordinal))
		{
			json[key] = write(inner);
		}

		return json;
	}

	static JsonObject Style(ErdElementStyle style)
	{
		JsonObject json = [];

		Add(json, "accent", style.Accent);
		Add(json, "background", style.Background);
		Add(json, "foreground", style.Foreground);

		if (style.Bold is { } bold)
		{
			json["bold"] = bold;
		}

		if (style.Italic is { } italic)
		{
			json["italic"] = italic;
		}

		if (style.LineStyle is { } line)
		{
			json["lineStyle"] = Lower(line);
		}

		if (!string.IsNullOrWhiteSpace(style.Badge))
		{
			json["badge"] = style.Badge;
		}

		if (!string.IsNullOrWhiteSpace(style.Tooltip))
		{
			json["tooltip"] = style.Tooltip;
		}

		return json;

		static void Add(JsonObject json, string name, ErdColour? colour)
		{
			if (colour is { IsEmpty: false })
			{
				json[name] = Colour(colour);
			}
		}
	}

	/// <summary>Both theme values, each already falling back to the other, so the viewer needs no rule of its own.</summary>
	static JsonObject Colour(ErdColour colour) => new() { ["light"] = colour.ForLight, ["dark"] = colour.ForDark };
}

/// <summary>Case-insensitive matching with <c>*</c> for any run of characters and <c>?</c> for one.</summary>
static class ErdWildcard
{
	/// <summary>Whether <paramref name="value"/> matches <paramref name="pattern"/> in full.</summary>
	public static bool IsMatch(string pattern, string? value)
	{
		if (value is null)
		{
			return false;
		}

		int p = 0,
			v = 0,
			star = -1,
			resume = 0;

		while (v < value.Length)
		{
			if (p < pattern.Length && (pattern[p] == '?' || Same(pattern[p], value[v])))
			{
				p++;
				v++;
			}
			else if (p < pattern.Length && pattern[p] == '*')
			{
				// Remember where the star was, and first try matching it against nothing.
				star = p++;
				resume = v;
			}
			else if (star >= 0)
			{
				// Let the last star swallow one more character and try again from there.
				p = star + 1;
				v = ++resume;
			}
			else
			{
				return false;
			}
		}

		while (p < pattern.Length && pattern[p] == '*')
		{
			p++;
		}

		return p == pattern.Length;
	}

	static bool Same(char a, char b) => a == b || char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
}
