using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Applies include and exclude name filters to discovered <c>DbContext</c> types.
/// </summary>
/// <remarks>
/// A filter entry matches either the context's short name (<c>IdentityDbContext</c>) or its
/// fully-qualified type name (<c>Shared.Data.IdentityDbContext</c>), case-insensitively, so a shared
/// context can be named unambiguously even when several assemblies define a similarly-named one.
/// An empty include list means "everything"; an exclude entry always wins over an include entry.
/// </remarks>
static class EfContextFilter
{
	/// <summary>Whether a context survives the filters.</summary>
	public static bool Allows(EfContext context, IList<string> included, IList<string> excluded)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (excluded.Count > 0 && excluded.Any(name => Matches(context, name)))
		{
			return false;
		}

		// If the include list is empty, everything is included. Otherwise, the context must match at least one include entry.
		return included.Count == 0 || included.Any(name => Matches(context, name));
	}

	/// <summary>Returns a model containing only the contexts that survive the filters.</summary>
	public static EfModel Apply(EfModel model, IList<string> included, IList<string> excluded)
	{
		ArgumentNullException.ThrowIfNull(model);

		return included.Count == 0 && excluded.Count == 0
			? model
			: new EfModel { Contexts = [.. model.Contexts.Where(context => Allows(context, included, excluded))] };
	}

	static bool Matches(EfContext context, string name) =>
		string.Equals(context.Name, name, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(context.TypeName, name, StringComparison.OrdinalIgnoreCase);
}
