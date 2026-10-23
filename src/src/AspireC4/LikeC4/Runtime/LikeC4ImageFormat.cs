namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// The image formats the architecture diagram is exported to on startup.
/// </summary>
/// <remarks>
/// There is deliberately no SVG member: the LikeC4 CLI renders its views by screenshotting them in a
/// headless browser, and <c>likec4 export</c> offers only <c>png</c> and <c>jpg</c> as image formats.
/// For a vector of the architecture, export the model instead and render it with another tool.
/// </remarks>
public enum LikeC4ImageFormat
{
	/// <summary>Portable network graphic. The default, and the better of the two for a diagram.</summary>
	Png,

	/// <summary>
	/// JPEG. Smaller than a PNG of the same view, at the cost of compression artefacts around the text
	/// and borders a diagram is almost entirely made of.
	/// </summary>
	Jpg,
}
