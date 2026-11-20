namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>What each table card in the interactive viewer shows, by default.</summary>
public enum ErdTableDisplay
{
	/// <summary>Every column. The default.</summary>
	Columns,

	/// <summary>The table name alone — an overview for a large schema.</summary>
	Names,
}
