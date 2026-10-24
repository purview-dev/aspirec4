using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Names a relationship for a renderer.
/// </summary>
/// <remarks>
/// Both ends are named, because a relationship reads in both directions: a foreign key has a
/// navigation on the dependent and usually an inverse collection on the principal. The declared end
/// nearest the start of the edge is named first, so the arrow in the label and the arrow on the edge
/// agree. Where the model declares only one end, only that one is named — a single arrow says which
/// way it can be travelled, and nothing claims the other direction exists. Only when neither end is
/// declared does it fall back to the constraint name, and then to the columns.
/// </remarks>
static class EfRelationshipName
{
	/// <summary>The label a renderer shows on a relationship.</summary>
	/// <param name="foreignKey">The relationship to name.</param>
	/// <param name="direction">Which end the renderer draws the edge from.</param>
	public static string Label(EfForeignKey foreignKey, EfRelationshipDirection direction)
	{
		ArgumentNullException.ThrowIfNull(foreignKey);

		var (forward, reverse) =
			direction == EfRelationshipDirection.DependentToPrincipal
				? (foreignKey.NavigationName, foreignKey.PrincipalNavigationName)
				: (foreignKey.PrincipalNavigationName, foreignKey.NavigationName);

		if (!string.IsNullOrEmpty(forward) && !string.IsNullOrEmpty(reverse))
		{
			return $"{forward} ⇄ {reverse}";
		}

		if (!string.IsNullOrEmpty(forward))
		{
			return $"{forward} →";
		}

		if (!string.IsNullOrEmpty(reverse))
			return $"← {reverse}";

		// If neither end is named, fall back to the constraint name, and then to the columns.
		return !string.IsNullOrEmpty(foreignKey.ConstraintName)
			? foreignKey.ConstraintName
			: string.Join(", ", foreignKey.Columns);
	}
}
