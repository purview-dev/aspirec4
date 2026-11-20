namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// A colour is written into CSS and into three DSLs, so anything that could escape its syntax is
/// refused, and each theme falls back to the other so one value is always enough.
/// </summary>
public sealed class ErdColourTests
{
	// Tests

	[Test]
	[Arguments("#2563eb")]
	[Arguments("#fff")]
	[Arguments("#2563eb80")]
	[Arguments("teal")]
	[Arguments("rgb(37 99 235)")]
	[Arguments("rgba(37, 99, 235, 0.5)")]
	[Arguments("hsl(221 83% 53%)")]
	[Arguments("oklch(0.55 0.2 260)")]
	public async Task Constructor_CssColour_IsAccepted(string value)
	{
		// Act
		ErdColour colour = new(value);

		// Assert
		await Assert.That(colour.Light).IsEqualTo(value);
		await Assert.That(ErdColour.IsValid(value)).IsTrue();
	}

	[Test]
	[Arguments("#12")]
	[Arguments("red; background: url(x)")]
	[Arguments("\"#fff\"")]
	[Arguments("#fff}")]
	[Arguments("blue\nstyle")]
	[Arguments("url(https://example.com/x.png)")]
	[Arguments("expression(alert(1))")]
	public async Task Constructor_ValueThatCouldEscapeItsSyntax_Throws(string value)
	{
		// Act & Assert
		await Assert.That(() => new ErdColour(value)).Throws<ArgumentException>();
		await Assert.That(ErdColour.IsValid(value)).IsFalse();
	}

	[Test]
	public async Task Constructor_SurroundingWhitespace_IsTrimmed()
	{
		// Act
		ErdColour colour = new("  #2563eb ");

		// Assert
		await Assert.That(colour.Light).IsEqualTo("#2563eb");
	}

	[Test]
	public async Task ForDark_OnlyLightSet_FallsBackToLight()
	{
		// Act
		ErdColour colour = new("#2563eb");

		// Assert
		await Assert.That(colour.ForLight).IsEqualTo("#2563eb");
		await Assert.That(colour.ForDark).IsEqualTo("#2563eb");
	}

	[Test]
	public async Task ForLight_OnlyDarkSet_FallsBackToDark()
	{
		// Act
		ErdColour colour = new(null, "#60a5fa");

		// Assert
		await Assert.That(colour.ForLight).IsEqualTo("#60a5fa");
		await Assert.That(colour.ForDark).IsEqualTo("#60a5fa");
	}

	[Test]
	public async Task Hex_CssFunction_IsNullForTheTextRenderers()
	{
		// Mermaid separates style properties with commas, which a colour function's own commas break.
		// Act
		ErdColour colour = new("rgb(37, 99, 235)");

		// Assert
		await Assert.That(colour.Hex).IsNull();
	}

	[Test]
	public async Task Hex_HexLight_IsTheLightValue()
	{
		// The text renderers have no theme switch, so they draw in the light value.
		// Act
		ErdColour colour = new("#2563eb", "#60a5fa");

		// Assert
		await Assert.That(colour.Hex).IsEqualTo("#2563eb");
	}

	[Test]
	public async Task ToString_TwoThemes_ShowsBoth()
	{
		// Act
		ErdColour colour = new("#2563eb", "#60a5fa");

		// Assert
		await Assert.That(colour.ToString()).IsEqualTo("#2563eb / #60a5fa");
	}
}
