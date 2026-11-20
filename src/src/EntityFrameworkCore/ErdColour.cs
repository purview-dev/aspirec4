using System.Text.RegularExpressions;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// A colour for the ER diagram, with an optional variant for the dark theme.
/// </summary>
/// <remarks>
/// <para>
/// Set one value to use it in both themes, or both to have the interactive viewer switch between them
/// as its theme changes. Whichever is missing falls back to the other, so <c>new("#2563eb")</c> and
/// <c>new(dark: "#60a5fa")</c> are both complete.
/// </para>
/// <para>
/// Any CSS colour is accepted by the interactive viewer — <c>#rgb</c>, <c>#rrggbb</c>,
/// <c>#rrggbbaa</c>, a named colour, or a function such as <c>rgb()</c>, <c>hsl()</c> or
/// <c>oklch()</c>. The text renderers (Mermaid, d2 and DBML) have no theme switch and accept only hex,
/// so they use the light value when it is hex and leave the element unstyled otherwise. Prefer hex for
/// a style that should reach every renderer.
/// </para>
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed partial class ErdColour
{
	/// <summary>Creates an empty colour, for configuration binding.</summary>
	public ErdColour() { }

	/// <summary>Creates a colour.</summary>
	/// <param name="light">The colour in the light theme, and in the dark theme when <paramref name="dark"/> is omitted.</param>
	/// <param name="dark">The colour in the dark theme.</param>
	public ErdColour(string? light, string? dark = null)
	{
		Light = light;
		Dark = dark;
	}

	/// <summary>The colour in the light theme. Falls back to <see cref="Dark"/> when unset.</summary>
	/// <exception cref="ArgumentException">The value is not a CSS colour.</exception>
	public string? Light
	{
		get;
		set => field = Validate(value, nameof(Light));
	}

	/// <summary>The colour in the dark theme. Falls back to <see cref="Light"/> when unset.</summary>
	/// <exception cref="ArgumentException">The value is not a CSS colour.</exception>
	public string? Dark
	{
		get;
		set => field = Validate(value, nameof(Dark));
	}

	/// <summary>The colour the light theme uses.</summary>
	internal string? ForLight => Light ?? Dark;

	/// <summary>The colour the dark theme uses.</summary>
	internal string? ForDark => Dark ?? Light;

	/// <summary>Whether neither theme has a colour.</summary>
	internal bool IsEmpty => Light is null && Dark is null;

	/// <summary>
	/// The colour for a renderer with no theme switch: the light value, when it is hex.
	/// </summary>
	/// <remarks>
	/// The text formats each embed the value in their own syntax, which a CSS function's commas and
	/// parentheses would break — Mermaid's <c>classDef</c> separates properties with commas, and DBML's
	/// <c>headercolor</c> accepts hex alone.
	/// </remarks>
	internal string? Hex => ForLight is { } light && HexColour().IsMatch(light) ? light : null;

	/// <inheritdoc/>
	public override string ToString() =>
		Dark is null || string.Equals(Light, Dark, StringComparison.OrdinalIgnoreCase)
			? ForLight ?? string.Empty
			: $"{Light ?? Dark} / {Dark}";

	/// <summary>Whether a value is one this type accepts.</summary>
	/// <remarks>
	/// The check is deliberately a shape, not a parse: it exists to keep a value from escaping the
	/// syntax it is written into — a quote, a semicolon, a brace or a line break would let a style
	/// inject arbitrary DSL or CSS — not to decide whether a browser will understand it.
	/// </remarks>
	public static bool IsValid(string? value) => value is null || CssColour().IsMatch(value);

	static string? Validate(string? value, string name)
	{
		if (value is null)
		{
			return null;
		}

		var trimmed = value.Trim();

		if (trimmed.Length == 0)
		{
			return null;
		}

		return CssColour().IsMatch(trimmed)
			? trimmed
			: throw new ArgumentException(
				$"'{value}' is not a CSS colour. Use hex (#2563eb), a named colour (teal), or a colour "
					+ "function such as rgb(), hsl() or oklch().",
				name
			);
	}

	[GeneratedRegex(
		@"^(#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|[a-zA-Z]{3,32}|(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\([0-9a-zA-Z.,%+\-\s/]{1,96}\))$",
		RegexOptions.CultureInvariant
	)]
	private static partial Regex CssColour();

	[GeneratedRegex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
	private static partial Regex HexColour();
}
