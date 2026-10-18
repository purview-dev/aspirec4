namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// The available ER diagram renderers.
/// </summary>
/// <remarks>
/// Every text format is a lossy projection of the model: only <see cref="Interactive"/> consumes the
/// descriptor directly and so loses nothing. The text renderers exist for publishing and interchange
/// rather than as the primary experience.
/// </remarks>
public enum ErdRenderer
{
	/// <summary>
	/// React Flow + ELK, rendered from the model JSON. The default, and the only renderer with full
	/// fidelity — column-level edges, nested grouping and key badges all survive.
	/// </summary>
	Interactive,

	/// <summary>
	/// Mermaid <c>erDiagram</c>. The only format GitHub, Azure DevOps and most wikis render inline, but
	/// the lowest fidelity: relationships connect entities rather than columns, and grouping is limited
	/// to one flat <c>subgraph</c> level.
	/// </summary>
	Mermaid,

	/// <summary>
	/// DBML. The highest-fidelity text format — column-level references and precision types survive
	/// intact — intended for interchange with database tooling such as dbdiagram.io. Renders nowhere
	/// without such a tool.
	/// </summary>
	Dbml,

	/// <summary>
	/// d2 <c>sql_table</c>, served by a live <c>d2 --watch</c> sidecar. Retains column-level edges and
	/// nested containers; requires the container (or a local <c>d2</c> binary).
	/// </summary>
	D2,
}
