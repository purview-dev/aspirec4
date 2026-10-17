namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Annotations;

/// <summary>
/// Marks a project resource as contributing its EF Core model to the ER diagram.
/// </summary>
/// <remarks>
/// Explicit opt-in is not required: by default every project resource is scanned for
/// <c>DbContext</c> types. Applying this annotation pins a project into the diagram even when
/// automatic discovery is switched off with <c>WithProjectOptInOnly()</c>, and carries the project's
/// label and per-project context filters.
/// </remarks>
public sealed class AspireC4EfProjectAnnotation : IResourceAnnotation
{
	/// <summary>Creates an annotation with empty project options.</summary>
	public AspireC4EfProjectAnnotation()
		: this(new AspireC4EfProjectOptions()) { }

	/// <summary>Creates an annotation from a project's options.</summary>
	/// <param name="options">The project's configuration.</param>
	public AspireC4EfProjectAnnotation(AspireC4EfProjectOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		Options = options;
	}

	/// <summary>This project's configuration.</summary>
	public AspireC4EfProjectOptions Options { get; }

	/// <summary>Optional label for this project's group in the diagram.</summary>
	public string? Label
	{
		get => Options.Label;
		set => Options.Label = value;
	}
}
