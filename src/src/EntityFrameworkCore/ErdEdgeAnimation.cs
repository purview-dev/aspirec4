namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// How fast the interactive viewer's relationship lines flow toward their many end, by default.
/// </summary>
/// <remarks>
/// The reader can change it in the viewer's settings. A browser asking for reduced motion stops the
/// animation whatever is chosen here, unless the reader overrides that in the viewer too.
/// </remarks>
public enum ErdEdgeAnimation
{
	/// <summary>Still lines.</summary>
	Off,

	/// <summary>A slow flow.</summary>
	Slow,

	/// <summary>The default flow.</summary>
	Normal,

	/// <summary>A fast flow.</summary>
	Fast,
}
