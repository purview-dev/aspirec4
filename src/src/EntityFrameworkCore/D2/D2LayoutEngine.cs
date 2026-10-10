namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;

/// <summary>The d2 layout engines available in the bundled CLI.</summary>
public enum D2LayoutEngine
{
	/// <summary>
	/// d2's default layered engine. Does <b>not</b> target individual <c>sql_table</c> rows — foreign key
	/// edges attach to the table as a whole, so it is a poor fit for ER diagrams.
	/// </summary>
	Dagre,

	/// <summary>Targets exact <c>sql_table</c> rows. The default for AspireC4 ER diagrams.</summary>
	Elk,

	/// <summary>
	/// Terrastruct's layout engine, open-sourced and bundled from d2 v0.9.0. Also targets exact rows and
	/// is tuned for software diagrams.
	/// </summary>
	Tala,
}
