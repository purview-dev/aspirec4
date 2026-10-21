using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Names a relationship for a renderer.
/// </summary>
/// <remarks>
/// Both ends are named, because a relationship reads in both directions: a foreign key has a
/// navigation on the dependent and usually an inverse collection on the principal. Where the model
/// declares only one, the other is marked <c>?</c> rather than hidden — an unnamed direction is
/// information. Only when neither is declared does it fall back to the constraint name, and then to
/// the columns.
/// </remarks>
static class EfRelationshipName
{
	/// <summary>The label a renderer shows on a relationship.</summary>
	public static string Label(EfForeignKey foreignKey)
	{
		ArgumentNullException.ThrowIfNull(foreignKey);

		var dependent = foreignKey.NavigationName;
		var principal = foreignKey.PrincipalNavigationName;

		if (!string.IsNullOrEmpty(dependent) && !string.IsNullOrEmpty(principal))
		{
			return $"{dependent} ⇄ {principal}";
		}

		if (!string.IsNullOrEmpty(dependent))
		{
			return $"{dependent} → ?";
		}

		if (!string.IsNullOrEmpty(principal))
			return $"? → {principal}";

		// If neither end is named, fall back to the constraint name, and then to the columns.
		return !string.IsNullOrEmpty(foreignKey.ConstraintName)
			? foreignKey.ConstraintName
			: string.Join(", ", foreignKey.Columns);
	}
}
