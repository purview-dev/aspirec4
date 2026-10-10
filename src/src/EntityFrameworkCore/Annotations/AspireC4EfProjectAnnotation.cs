namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;

/// <summary>
/// Marks a project resource as contributing its EF Core model to the ER diagram.
/// </summary>
/// <remarks>
/// Opt-in rather than automatic: scanning every project's build output on startup would cost time and
/// would surprise users whose projects reference EF for unrelated reasons.
/// </remarks>
public sealed class AspireC4EfProjectAnnotation : IResourceAnnotation
{
	/// <summary>An optional label for this project's group in the diagram.</summary>
	public string? Label { get; set; }
}
