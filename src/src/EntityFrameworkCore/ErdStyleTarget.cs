namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>Which kind of diagram element an <see cref="ErdStyleRule"/> styles.</summary>
public enum ErdStyleTarget
{
	/// <summary>
	/// A column when the rule names a column or a type, otherwise a table. The default, and what
	/// <c>WithTableStyle</c>, <c>WithColumnStyle</c> and <c>WithTypeStyle</c> produce.
	/// </summary>
	Auto,

	/// <summary>
	/// Tables. A column or type pattern on the rule then selects the tables that have a matching column —
	/// every table carrying a <c>TenantId</c>, say — rather than the columns themselves.
	/// </summary>
	Table,

	/// <summary>Columns, narrowed by any context, table, column and type patterns on the rule.</summary>
	Column,

	/// <summary>
	/// Relationships. The table pattern matches either end, and the column pattern any foreign-key
	/// column.
	/// </summary>
	Relationship,
}
