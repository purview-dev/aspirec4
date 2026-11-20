namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// How one table, column or relationship in the ER diagram is drawn.
/// </summary>
/// <remarks>
/// <para>
/// Every property is optional; an unset one leaves the diagram's own styling in place. Where several
/// rules match one element they are applied in the order they were added, each overriding only what it
/// sets, and any style callback then sees the result and can change it further.
/// </para>
/// <para>What each property means depends on the element:</para>
/// <list type="table">
///   <listheader><term>Property</term><term>Table</term><term>Column</term><term>Relationship</term></listheader>
///   <item><term><see cref="Accent"/></term><term>Header band and border</term><term>Bar beside the row</term><term>Line colour</term></item>
///   <item><term><see cref="Background"/></term><term>Card body</term><term>Row</term><term>Label</term></item>
///   <item><term><see cref="Foreground"/></term><term>Header text</term><term>Column name</term><term>Label text</term></item>
///   <item><term><see cref="LineStyle"/></term><term>Border</term><term>Not used</term><term>Line</term></item>
///   <item><term><see cref="Badge"/></term><term>Shown in the header</term><term>Shown beside the key badges</term><term>Not used</term></item>
/// </list>
/// <para>
/// The interactive viewer applies all of it. Mermaid, d2 and DBML have no per-column styling, and so
/// apply only the table and relationship colours their formats support — see the ER diagram
/// documentation for the exact mapping.
/// </para>
/// </remarks>
[AspireExport(ExposeProperties = true, ExposeMethods = true)]
public sealed class ErdElementStyle
{
	/// <summary>The element's accent colour.</summary>
	public ErdColour? Accent { get; set; }

	/// <summary>The element's background colour.</summary>
	public ErdColour? Background { get; set; }

	/// <summary>The element's text colour.</summary>
	public ErdColour? Foreground { get; set; }

	/// <summary>Whether the element's text is bold.</summary>
	public bool? Bold { get; set; }

	/// <summary>Whether the element's text is italic.</summary>
	public bool? Italic { get; set; }

	/// <summary>How a table's border or a relationship's line is drawn.</summary>
	public ErdLineStyle? LineStyle { get; set; }

	/// <summary>
	/// A short label shown on the element, such as <c>PII</c>, <c>AUDIT</c> or <c>LEGACY</c>.
	/// </summary>
	/// <remarks>Listed in the viewer's legend, when the legend is shown.</remarks>
	public string? Badge { get; set; }

	/// <summary>Extra text shown when the reader hovers over the element in the interactive viewer.</summary>
	public string? Tooltip { get; set; }

	/// <summary>Sets the accent colour.</summary>
	/// <param name="light">The colour in the light theme, and in the dark theme when <paramref name="dark"/> is omitted.</param>
	/// <param name="dark">The colour in the dark theme.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithAccent(string light, string? dark = null)
	{
		Accent = new(light, dark);

		return this;
	}

	/// <summary>Sets the background colour.</summary>
	/// <param name="light">The colour in the light theme, and in the dark theme when <paramref name="dark"/> is omitted.</param>
	/// <param name="dark">The colour in the dark theme.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithBackground(string light, string? dark = null)
	{
		Background = new(light, dark);

		return this;
	}

	/// <summary>Sets the text colour.</summary>
	/// <param name="light">The colour in the light theme, and in the dark theme when <paramref name="dark"/> is omitted.</param>
	/// <param name="dark">The colour in the dark theme.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithForeground(string light, string? dark = null)
	{
		Foreground = new(light, dark);

		return this;
	}

	/// <summary>Sets whether the text is bold.</summary>
	/// <param name="bold">Whether the text is bold.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithBold(bool bold = true)
	{
		Bold = bold;

		return this;
	}

	/// <summary>Sets whether the text is italic.</summary>
	/// <param name="italic">Whether the text is italic.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithItalic(bool italic = true)
	{
		Italic = italic;

		return this;
	}

	/// <summary>Sets how a table's border or a relationship's line is drawn.</summary>
	/// <param name="lineStyle">The line style.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithLineStyle(ErdLineStyle lineStyle)
	{
		LineStyle = lineStyle;

		return this;
	}

	/// <summary>Sets a short label shown on the element.</summary>
	/// <param name="badge">The label, such as <c>PII</c>.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithBadge(string badge)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(badge);

		Badge = badge;

		return this;
	}

	/// <summary>Sets extra text shown when the reader hovers over the element.</summary>
	/// <param name="tooltip">The text.</param>
	/// <returns>This style, for chaining.</returns>
	public ErdElementStyle WithTooltip(string tooltip)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tooltip);

		Tooltip = tooltip;

		return this;
	}

	/// <summary>Whether nothing is set, so the element is drawn as it would be unstyled.</summary>
	internal bool IsEmpty =>
		(Accent?.IsEmpty ?? true)
		&& (Background?.IsEmpty ?? true)
		&& (Foreground?.IsEmpty ?? true)
		&& Bold is null
		&& Italic is null
		&& LineStyle is null
		&& string.IsNullOrWhiteSpace(Badge)
		&& string.IsNullOrWhiteSpace(Tooltip);

	/// <summary>Overlays everything <paramref name="other"/> sets onto this style.</summary>
	internal void Apply(ErdElementStyle other)
	{
		Accent = Pick(other.Accent, Accent);
		Background = Pick(other.Background, Background);
		Foreground = Pick(other.Foreground, Foreground);
		Bold = other.Bold ?? Bold;
		Italic = other.Italic ?? Italic;
		LineStyle = other.LineStyle ?? LineStyle;
		Badge = string.IsNullOrWhiteSpace(other.Badge) ? Badge : other.Badge;
		Tooltip = string.IsNullOrWhiteSpace(other.Tooltip) ? Tooltip : other.Tooltip;
	}

	/// <summary>
	/// A copy holding its own colours, so a callback changing it cannot reach back into the rule it came
	/// from.
	/// </summary>
	internal ErdElementStyle Clone() =>
		new()
		{
			Accent = Copy(Accent),
			Background = Copy(Background),
			Foreground = Copy(Foreground),
			Bold = Bold,
			Italic = Italic,
			LineStyle = LineStyle,
			Badge = Badge,
			Tooltip = Tooltip,
		};

	static ErdColour? Pick(ErdColour? preferred, ErdColour? fallback) =>
		preferred is { IsEmpty: false } ? Copy(preferred) : fallback;

	static ErdColour? Copy(ErdColour? colour) => colour is null ? null : new(colour.Light, colour.Dark);
}
