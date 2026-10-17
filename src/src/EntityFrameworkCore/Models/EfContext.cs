namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>Represents a single <c>DbContext</c> and the tables reachable from it.</summary>
public sealed record EfContext
{
	/// <summary>The fully-qualified CLR type name of the <c>DbContext</c>.</summary>
	public required string TypeName { get; init; }

	/// <summary>The short (unqualified) type name, used for labelling.</summary>
	public required string Name { get; init; }

	/// <summary>The default schema applied via <c>HasDefaultSchema</c>, when one was found.</summary>
	public string? DefaultSchema { get; init; }

	/// <summary>
	/// How faithfully this context's shape was recovered.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="EfModelFidelity.Full"/> so a descriptor produced by the generator — which
	/// never emits the property — deserialises as a complete model.
	/// </remarks>
	public EfModelFidelity Fidelity { get; init; } = EfModelFidelity.Full;

	/// <summary>The tables mapped by this context.</summary>
	public IReadOnlyList<EfTable> Tables { get; init; } = [];
}
