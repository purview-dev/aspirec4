namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Analysis;

/// <summary>A table under construction during analysis.</summary>
sealed class TableShape
{
	public string Name { get; set; } = string.Empty;

	public string ClrTypeName { get; set; } = string.Empty;

	public string? Schema { get; set; }

	public List<ColumnShape> Columns { get; } = [];

	public List<ForeignKeyShape> ForeignKeys { get; } = [];
}

/// <summary>A column under construction during analysis.</summary>
sealed class ColumnShape
{
	/// <summary>The CLR property name, used to resolve fluent and attribute configuration.</summary>
	public string PropertyName { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string StoreType { get; set; } = string.Empty;

	public string? ClrTypeName { get; set; }

	public bool IsNullable { get; set; }

	public bool IsPrimaryKey { get; set; }

	public bool IsUnique { get; set; }

	public bool IsForeignKey { get; set; }

	public int? MaxLength { get; set; }
}

/// <summary>A foreign key under construction during analysis.</summary>
sealed class ForeignKeyShape
{
	public List<string> Columns { get; } = [];

	/// <summary>
	/// Initially the principal's CLR type name; rewritten to the resolved table name once every table
	/// is known.
	/// </summary>
	public string PrincipalEntity { get; set; } = string.Empty;

	public List<string> PrincipalColumns { get; } = [];

	public string? NavigationName { get; set; }

	/// <summary>Set once the dependent columns are known to be a primary key or unique index.</summary>
	public bool IsUnique { get; set; }

	/// <summary>Set when every dependent column is non-nullable.</summary>
	public bool IsRequired { get; set; }
}
