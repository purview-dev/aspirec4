namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Per-project configuration for a resource opted into the ER diagram with
/// <c>WithEntityFrameworkModel()</c>.
/// </summary>
/// <remarks>
/// Filters here apply only to the contexts discovered in this project's build output, so a context
/// shared by several projects — an <c>IdentityDbContext</c> in a common library, say — can be
/// suppressed for one contributor while another still shows it.
/// </remarks>
public sealed class AspireC4EfProjectOptions
{
	/// <summary>Optional label for this project's group in the diagram.</summary>
	public string? Label { get; set; }

	/// <summary>
	/// Contexts to include from this project. When empty, every discovered context is included.
	/// </summary>
	/// <remarks>Entries match the short or fully-qualified type name, case-insensitively.</remarks>
	public IList<string> IncludedContexts { get; } = [];

	/// <summary>Contexts to exclude from this project, matched the same way as
	/// <see cref="IncludedContexts"/>. An entry here wins over an include entry.</summary>
	public IList<string> ExcludedContexts { get; } = [];

	/// <summary>Restricts this project's contribution to the listed contexts.</summary>
	/// <param name="contextNames">The context names to include.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfProjectOptions WithIncludedContexts(params string[] contextNames)
	{
		AddContextNames(IncludedContexts, contextNames);

		return this;
	}

	/// <summary>Removes the listed contexts from this project's contribution.</summary>
	/// <param name="contextNames">The context names to exclude.</param>
	/// <returns>This options instance, for chaining.</returns>
	public AspireC4EfProjectOptions WithExcludedContexts(params string[] contextNames)
	{
		AddContextNames(ExcludedContexts, contextNames);

		return this;
	}

	static void AddContextNames(IList<string> target, string[] contextNames)
	{
		ArgumentNullException.ThrowIfNull(contextNames);

		foreach (var name in contextNames)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(name);

			if (!target.Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				target.Add(name);
			}
		}
	}
}
