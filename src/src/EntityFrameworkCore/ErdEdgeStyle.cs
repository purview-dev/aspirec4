namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>How the interactive viewer draws a relationship between two tables.</summary>
/// <remarks>A table's relationship to itself is always drawn as an arc beside its card.</remarks>
public enum ErdEdgeStyle
{
	/// <summary>A smooth curve. The default.</summary>
	Bezier,

	/// <summary>Right-angled segments with rounded corners.</summary>
	SmoothStep,

	/// <summary>Right-angled segments with square corners.</summary>
	Step,

	/// <summary>A straight line.</summary>
	Straight,
}
