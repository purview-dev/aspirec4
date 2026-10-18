namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>
/// The descriptor produced by the AspireC4 EF Core source generator and read back by the AppHost.
/// </summary>
/// <remarks>
/// This type is the serialisation contract between the generator (which runs in the service project)
/// and the AppHost. Changing it is a breaking change across the package boundary — see
/// <see cref="SchemaVersion"/>.
/// </remarks>
public sealed record EfModel
{
	/// <summary>The descriptor schema version, so the AppHost can reject descriptors it cannot read.</summary>
	public int SchemaVersion { get; init; } = 1;

	/// <summary>The <c>DbContext</c> types discovered in the analysed assembly.</summary>
	public required IReadOnlyList<EfContext> Contexts { get; init; }

	/// <summary>A pre-built empty model containing no contexts.</summary>
	public static readonly EfModel Empty = new() { Contexts = [] };
}
