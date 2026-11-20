namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Styles every table, column or relationship in the ER diagram that matches its patterns.
/// </summary>
/// <remarks>
/// <para>
/// Each pattern is optional and narrows the match; one left unset matches anything. Patterns are
/// case-insensitive and accept <c>*</c> for any run of characters and <c>?</c> for one character:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Context"/> — the <c>DbContext</c>'s short or full type name.</description></item>
///   <item><description><see cref="Table"/> — the table name, or the entity's short or full CLR type name.</description></item>
///   <item><description><see cref="Column"/> — the column name.</description></item>
///   <item><description>
///     <see cref="Type"/> — the column's store type (<c>decimal</c>, <c>varchar(200)</c>), its domain
///     type (<c>OrderId</c>), its CLR type, or its kind of domain type (<c>enum</c>, <c>union</c>,
///     <c>ValueObject</c>).
///   </description></item>
/// </list>
/// <para>
/// Any combination works: <c>Table = "Order*", Type = "decimal"</c> styles the decimal columns of every
/// order table. <see cref="Target"/> decides what is styled — see <see cref="ErdStyleTarget"/>.
/// </para>
/// </remarks>
[AspireExport(ExposeProperties = true, ExposeMethods = true)]
public sealed class ErdStyleRule
{
	/// <summary>Matches the <c>DbContext</c> by short or full type name.</summary>
	public string? Context { get; set; }

	/// <summary>Matches the table name, or the entity's short or full CLR type name.</summary>
	public string? Table { get; set; }

	/// <summary>Matches the column name.</summary>
	public string? Column { get; set; }

	/// <summary>Matches the column's store, domain or CLR type, or its kind of domain type.</summary>
	public string? Type { get; set; }

	/// <summary>Which kind of element is styled. Defaults to <see cref="ErdStyleTarget.Auto"/>.</summary>
	public ErdStyleTarget Target { get; set; }

	/// <summary>The style applied to every matching element.</summary>
	public ErdElementStyle Style { get; set; } = new();

	/// <summary>Restricts the rule to the contexts matching a pattern.</summary>
	/// <param name="pattern">A context name pattern, such as <c>Sales*</c>.</param>
	/// <returns>This rule, for chaining.</returns>
	public ErdStyleRule ForContext(string pattern)
	{
		Context = Pattern(pattern);

		return this;
	}

	/// <summary>Restricts the rule to the tables matching a pattern.</summary>
	/// <param name="pattern">A table or entity name pattern, such as <c>Order*</c>.</param>
	/// <returns>This rule, for chaining.</returns>
	public ErdStyleRule ForTable(string pattern)
	{
		Table = Pattern(pattern);

		return this;
	}

	/// <summary>Restricts the rule to the columns matching a pattern, so it styles columns.</summary>
	/// <param name="pattern">A column name pattern, such as <c>*Id</c>.</param>
	/// <returns>This rule, for chaining.</returns>
	public ErdStyleRule ForColumn(string pattern)
	{
		Column = Pattern(pattern);

		return this;
	}

	/// <summary>Restricts the rule to the columns of a matching type, so it styles columns.</summary>
	/// <param name="pattern">A type pattern, such as <c>decimal</c>, <c>OrderId</c> or <c>enum</c>.</param>
	/// <returns>This rule, for chaining.</returns>
	public ErdStyleRule ForType(string pattern)
	{
		Type = Pattern(pattern);

		return this;
	}

	/// <summary>Makes the rule style the matching tables, even when it names a column or type.</summary>
	/// <remarks>
	/// With a column or type pattern this styles each table that has a matching column — every table
	/// carrying a <c>TenantId</c>, for example.
	/// </remarks>
	/// <returns>This rule, for chaining.</returns>
	public ErdStyleRule ForTables()
	{
		Target = ErdStyleTarget.Table;

		return this;
	}

	/// <summary>
	/// Makes the rule style relationships: those with a matching table at either end and a matching
	/// foreign-key column.
	/// </summary>
	/// <returns>This rule, for chaining.</returns>
	public ErdStyleRule ForRelationships()
	{
		Target = ErdStyleTarget.Relationship;

		return this;
	}

	/// <inheritdoc cref="ErdElementStyle.WithAccent"/>
	public ErdStyleRule WithAccent(string light, string? dark = null) => Configure(s => s.WithAccent(light, dark));

	/// <inheritdoc cref="ErdElementStyle.WithBackground"/>
	public ErdStyleRule WithBackground(string light, string? dark = null) =>
		Configure(s => s.WithBackground(light, dark));

	/// <inheritdoc cref="ErdElementStyle.WithForeground"/>
	public ErdStyleRule WithForeground(string light, string? dark = null) =>
		Configure(s => s.WithForeground(light, dark));

	/// <inheritdoc cref="ErdElementStyle.WithBold"/>
	public ErdStyleRule WithBold(bool bold = true) => Configure(s => s.WithBold(bold));

	/// <inheritdoc cref="ErdElementStyle.WithItalic"/>
	public ErdStyleRule WithItalic(bool italic = true) => Configure(s => s.WithItalic(italic));

	/// <inheritdoc cref="ErdElementStyle.WithLineStyle"/>
	public ErdStyleRule WithLineStyle(ErdLineStyle lineStyle) => Configure(s => s.WithLineStyle(lineStyle));

	/// <inheritdoc cref="ErdElementStyle.WithBadge"/>
	public ErdStyleRule WithBadge(string badge) => Configure(s => s.WithBadge(badge));

	/// <inheritdoc cref="ErdElementStyle.WithTooltip"/>
	public ErdStyleRule WithTooltip(string tooltip) => Configure(s => s.WithTooltip(tooltip));

	/// <summary>What the rule styles once <see cref="ErdStyleTarget.Auto"/> is resolved.</summary>
	internal ErdStyleTarget EffectiveTarget =>
		Target != ErdStyleTarget.Auto ? Target
		: Column is not null || Type is not null ? ErdStyleTarget.Column
		: ErdStyleTarget.Table;

	ErdStyleRule Configure(Action<ErdElementStyle> configure)
	{
		Style ??= new();
		configure(Style);

		return this;
	}

	static string Pattern(string pattern)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

		return pattern;
	}
}
