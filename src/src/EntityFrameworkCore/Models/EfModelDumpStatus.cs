namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>
/// Why a build-time EF Core model dump did, or did not, supply the diagram's model.
/// </summary>
/// <remarks>
/// The dump is the only discovery source that sees conventions, value converters and composite
/// foreign keys, so when it is present but unusable the diagram silently degrades to a less faithful
/// model. Recording which of those happened turns "the diagram is missing configuration" into an
/// actionable message: a stale dump means the project needs rebuilding, a missing one means the
/// ModelDump package is not referenced.
/// </remarks>
public enum EfModelDumpStatus
{
	/// <summary>No dump lookup has run for this project.</summary>
	NotChecked,

	/// <summary>A fresh dump was found and supplied the model.</summary>
	Loaded,

	/// <summary>
	/// The project path did not resolve to a directory, so there was nothing to look beneath.
	/// </summary>
	ProjectUnresolved,

	/// <summary>
	/// The dump directory or its JSON files do not exist, which normally means the project does not
	/// reference <c>AspireC4.EntityFrameworkCore.ModelDump</c> or has not been built yet.
	/// </summary>
	Missing,

	/// <summary>
	/// A dump exists but predates the assembly it describes, so it was ignored rather than risking a
	/// diagram of a model that no longer exists.
	/// </summary>
	Stale,

	/// <summary>The dump files were readable but described no contexts.</summary>
	Empty,

	/// <summary>The dump could not be read, for example because a build was writing it.</summary>
	Unreadable,
}
