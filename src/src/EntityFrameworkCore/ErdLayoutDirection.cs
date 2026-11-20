namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>The direction relationships flow in when the diagram is laid out.</summary>
/// <remarks>
/// Applied by the interactive viewer, d2 and Mermaid. DBML carries no layout. Principal tables are
/// placed first and their dependents follow in this direction.
/// </remarks>
public enum ErdLayoutDirection
{
	/// <summary>Top to bottom. The default.</summary>
	Down,

	/// <summary>Left to right — better for wide screens and long chains of relationships.</summary>
	Right,

	/// <summary>Bottom to top.</summary>
	Up,

	/// <summary>Right to left.</summary>
	Left,
}
