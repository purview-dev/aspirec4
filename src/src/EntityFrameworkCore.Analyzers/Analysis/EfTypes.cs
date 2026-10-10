namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Analysis;

/// <summary>Well-known EF Core and data-annotation type and member names the analyser matches on.</summary>
static class EfTypes
{
	/// <summary>The base type every analysed context derives from.</summary>
	public const string DbContext = "Microsoft.EntityFrameworkCore.DbContext";

	/// <summary>The namespace EF Core's own types live in.</summary>
	public const string EfNamespace = "Microsoft.EntityFrameworkCore";

	/// <summary>
	/// The arity-qualified metadata name of <c>DbSet&lt;TEntity&gt;</c>.
	/// </summary>
	/// <remarks>
	/// Matched on metadata name plus namespace rather than a display string: a display string embeds
	/// the declaring type-parameter name (<c>TEntity</c>), which is an implementation detail and will
	/// not match a differently-named stub or a future rename.
	/// </remarks>
	public const string DbSetMetadataName = "DbSet`1";

	/// <summary>The method whose body carries fluent model configuration.</summary>
	public const string OnModelCreating = "OnModelCreating";

	/// <summary>Attribute names, matched with and without the <c>Attribute</c> suffix.</summary>
	public static class Attributes
	{
		public const string Key = "Key";
		public const string Required = "Required";
		public const string MaxLength = "MaxLength";
		public const string StringLength = "StringLength";
		public const string Table = "Table";
		public const string Column = "Column";
		public const string ForeignKey = "ForeignKey";
		public const string NotMapped = "NotMapped";
		public const string Index = "Index";
	}

	/// <summary>Fluent builder members the analyser interprets.</summary>
	public static class Fluent
	{
		public const string Entity = "Entity";
		public const string HasDefaultSchema = "HasDefaultSchema";
		public const string ToTable = "ToTable";
		public const string HasKey = "HasKey";
		public const string Ignore = "Ignore";
		public const string Property = "Property";
		public const string HasColumnName = "HasColumnName";
		public const string HasColumnType = "HasColumnType";
		public const string HasMaxLength = "HasMaxLength";
		public const string IsRequired = "IsRequired";
		public const string HasIndex = "HasIndex";
		public const string IsUnique = "IsUnique";
		public const string ApplyConfiguration = "ApplyConfiguration";
		public const string ApplyConfigurationsFromAssembly = "ApplyConfigurationsFromAssembly";
	}

	/// <summary>
	/// Arity-qualified metadata names of the collection types that mark a navigation as a many-side.
	/// Matched alongside <see cref="CollectionsNamespace"/>.
	/// </summary>
	public static readonly HashSet<string> CollectionMetadataNames = new(StringComparer.Ordinal)
	{
		"ICollection`1",
		"IEnumerable`1",
		"IList`1",
		"IReadOnlyCollection`1",
		"IReadOnlyList`1",
		"ISet`1",
		"List`1",
		"HashSet`1",
	};

	/// <summary>The namespace the supported collection types live in.</summary>
	public const string CollectionsNamespace = "System.Collections.Generic";
}
