namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>The pattern behind the interactive viewer's diagram, by default.</summary>
public enum ErdViewerBackground
{
	/// <summary>A grid of lines. The default.</summary>
	Lines,

	/// <summary>A grid of dots.</summary>
	Dots,

	/// <summary>A grid of small crosses.</summary>
	Cross,

	/// <summary>A plain background.</summary>
	None,
}
