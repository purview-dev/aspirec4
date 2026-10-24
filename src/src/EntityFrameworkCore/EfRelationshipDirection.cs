namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Which end of a relationship a renderer draws the edge from.
/// </summary>
/// <remarks>
/// The renderers disagree, and legitimately so: d2 attaches an edge to the column that holds the
/// foreign key, so it runs from the dependent, while the interactive viewer and Mermaid run from the
/// principal so the line flows toward the many end. A relationship label names a direction of travel,
/// so it only reads correctly when it is ordered to match the edge it sits on.
/// </remarks>
enum EfRelationshipDirection
{
	/// <summary>The edge starts at the table holding the foreign key.</summary>
	DependentToPrincipal,

	/// <summary>The edge starts at the table the foreign key points at.</summary>
	PrincipalToDependent,
}
