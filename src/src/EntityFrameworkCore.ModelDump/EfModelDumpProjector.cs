using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ModelDump;

/// <summary>
/// Projects a built EF Core model into the AspireC4 diagram contract.
/// </summary>
/// <remarks>
/// This runs at build time, in the project that owns the provider, and is the only source that sees
/// the real model — conventions, value converters and composite foreign keys included. The output is
/// deliberately the diagram's own contract rather than the raw model, so the artifact stays small.
/// </remarks>
static class EfModelDumpProjector
{
	const string UnknownStoreType = "unknown";
	const string JsonStoreType = "json";

	static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

	/// <summary>Serialises one context's model into the dump contract.</summary>
	/// <param name="model">The built model.</param>
	/// <param name="contextName">The short context name, used for grouping and the file name.</param>
	/// <param name="contextTypeName">The fully-qualified context type name.</param>
	public static string Project(IModel model, string contextName, string contextTypeName)
	{
		ArgumentNullException.ThrowIfNull(model);

		DumpModel dump = new()
		{
			Contexts =
			[
				new DumpContext
				{
					Name = contextName,
					TypeName = contextTypeName,
					DefaultSchema = model.GetDefaultSchema(),
					Tables = [.. Tables(model)],
				},
			],
		};

		return JsonSerializer.Serialize(dump, SerializerOptions);
	}

	static IEnumerable<DumpTable> Tables(IModel model)
	{
		// A table can back several entity types (TPH); the least-derived one carries every column.
		Dictionary<(string Table, string? Schema), IEntityType> byTable = [];

		foreach (var entityType in model.GetEntityTypes())
		{
			// An owned type that shares its owner's table, or lives in a JSON column on it, is part of the
			// owner's table; one with a table of its own — OwnsMany, or OwnsOne with ToTable — is a table.
			if (entityType.IsOwned() && (entityType.IsMappedToJson() || SharesOwnerTable(entityType)))
			{
				continue;
			}

			if (entityType.GetTableName() is not { } table)
			{
				continue;
			}

			var key = (table, entityType.GetSchema());

			if (!byTable.TryGetValue(key, out var existing) || Depth(entityType) < Depth(existing))
			{
				byTable[key] = entityType;
			}
		}

		foreach (var entityType in byTable.Values)
		{
			yield return Table(entityType);
		}
	}

	static bool SharesOwnerTable(IEntityType owned) =>
		owned.FindOwnership() is { } ownership
		&& owned.GetTableName() == ownership.PrincipalEntityType.GetTableName()
		&& owned.GetSchema() == ownership.PrincipalEntityType.GetSchema();

	static DumpTable Table(IEntityType entityType)
	{
		var foreignKeys = entityType.GetForeignKeys().ToList();
		var uniqueProperties = UniqueProperties(entityType);
		var foreignKeyProperties = foreignKeys.SelectMany(fk => fk.Properties).ToHashSet();
		var store = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

		List<DumpColumn> columns = [];

		foreach (var column in Columns(entityType))
		{
			if (column.Json is { } json)
			{
				columns.Add(json);

				continue;
			}

			var property = column.Property!;
			var (name, kind) = DomainType(property, column.ComplexType);

			columns.Add(
				new DumpColumn
				{
					Name = property.GetColumnName(store) ?? property.GetColumnName(),
					StoreType = ExplicitColumnType(property)
						// EF stores a primitive collection as a JSON document in a string column; the other two
						// sources call it json, and so does this one.
						?? (property.IsPrimitiveCollection ? JsonStoreType : ResolveStoreType(ProviderType(property))),
					ClrTypeName = property.ClrType.FullName,
					DomainTypeName = name,
					DomainTypeKind = kind,
					ComplexPropertyName = column.Owner?.Property,
					ComplexTypeName = column.Owner?.Type,
					// The column's nullability, not the property's: a member of an optional complex type, or a
					// property of an owned type sharing the table, is a required property in a nullable column.
					IsNullable = property.IsColumnNullable(store),
					IsPrimaryKey = property.IsPrimaryKey(),
					IsUnique = uniqueProperties.Contains(property),
					IsForeignKey = foreignKeyProperties.Contains(property),
					MaxLength = property.GetMaxLength(),
					Comment = property.GetComment(),
				}
			);
		}

		var keyColumns = columns.Where(c => c.IsPrimaryKey).ToList();

		return new DumpTable
		{
			Name = entityType.GetTableName()!,
			ClrTypeName = entityType.ClrType.FullName ?? entityType.ClrType.Name,
			Schema = entityType.GetSchema(),
			IsJoinEntity = keyColumns.Count > 1 && keyColumns.TrueForAll(c => c.IsForeignKey),
			Columns = columns,
			ForeignKeys =
			[
				.. foreignKeys.Select(fk => new DumpForeignKey
				{
					Columns = [.. fk.Properties.Select(p => p.GetColumnName())],
					PrincipalTable = fk.PrincipalEntityType.GetTableName() ?? fk.PrincipalEntityType.ClrType.Name,
					PrincipalColumns = [.. fk.PrincipalKey.Properties.Select(p => p.GetColumnName())],
					NavigationName = fk.DependentToPrincipal?.Name,
					PrincipalNavigationName = fk.PrincipalToDependent?.Name,
					ConstraintName = fk.GetConstraintName(),
					IsUnique = fk.IsUnique,
					IsRequired = fk.IsRequired,
				}),
			],
			Indexes =
			[
				.. entityType
					.GetIndexes()
					.Select(index => new DumpIndex
					{
						Columns = [.. index.Properties.Select(p => p.GetColumnName())],
						IsUnique = index.IsUnique,
						Name = index.GetDatabaseName(),
					}),
			],
		};
	}

	/// <summary>One column of a table, as it reaches the projection.</summary>
	/// <param name="Property">The mapped property, for an ordinary column.</param>
	/// <param name="ComplexType">The immediately enclosing complex or owned type, for a member of one.</param>
	/// <param name="Owner">The outermost property and type a flattened member came from.</param>
	/// <param name="Json">A whole JSON-mapped complex or owned value, already projected.</param>
	sealed record ColumnSource(
		IProperty? Property,
		string? ComplexType,
		(string Property, string Type)? Owner,
		DumpColumn? Json = null
	);

	/// <summary>
	/// Every column of a table: the entity's own properties, then each complex type's members, then the
	/// properties of owned types that share the table.
	/// </summary>
	/// <remarks>
	/// The complex type name travels with each member so the column can show it beside the database
	/// type, and the outermost owner travels too, so a renderer can fold the members back under the
	/// property they came from. A complex or owned value mapped to JSON — and a complex collection, which
	/// can only be — is the one column that holds it.
	/// </remarks>
	static IEnumerable<ColumnSource> Columns(IEntityType entityType)
	{
		foreach (var property in entityType.GetProperties())
		{
			yield return new(property, null, null);
		}

		foreach (var complex in entityType.GetComplexProperties())
		{
			var owner = (complex.Name, complex.ComplexType.ClrType.Name);

			foreach (var column in ComplexColumns(complex, owner))
			{
				yield return column;
			}
		}

		foreach (var navigation in entityType.GetNavigations())
		{
			if (
				navigation.IsCollection
				|| navigation.ForeignKey.IsOwnership is false
				|| navigation.TargetEntityType == entityType
				|| !navigation.TargetEntityType.IsOwned()
			)
			{
				continue;
			}

			var owned = navigation.TargetEntityType;
			var owner = (navigation.Name, owned.ClrType.Name);

			if (owned.IsMappedToJson())
			{
				yield return JsonColumn(owned.GetContainerColumnName() ?? navigation.Name, owned.ClrType.Name, owner);

				continue;
			}

			if (!SharesOwnerTable(owned))
			{
				continue;
			}

			// The owned type's key is the owner's, carried through the shared table, so it is not a column
			// of its own.
			foreach (var property in owned.GetProperties().Where(p => !p.IsPrimaryKey()))
			{
				yield return new(property, owned.ClrType.Name, owner);
			}
		}
	}

	static IEnumerable<ColumnSource> ComplexColumns(IComplexProperty complex, (string Property, string Type) owner)
	{
		var complexName = complex.ComplexType.ClrType.Name;

		if (complex.IsCollection || complex.ComplexType.IsMappedToJson())
		{
			var name = complex.ComplexType.GetContainerColumnName() ?? complex.Name;
			var type = complex.IsCollection ? $"{complexName}[]" : complexName;

			yield return JsonColumn(name, type, owner == (complex.Name, complexName) ? null : owner);

			yield break;
		}

		foreach (var property in complex.ComplexType.GetProperties())
		{
			yield return new(property, complexName, owner);
		}

		foreach (var nested in complex.ComplexType.GetComplexProperties())
		{
			foreach (var column in ComplexColumns(nested, owner))
			{
				yield return column;
			}
		}
	}

	static ColumnSource JsonColumn(string name, string domainTypeName, (string Property, string Type)? owner) =>
		new(
			null,
			null,
			owner,
			new DumpColumn
			{
				Name = name,
				StoreType = JsonStoreType,
				DomainTypeName = domainTypeName,
				DomainTypeKind = "ComplexValueObject",
				ComplexPropertyName = owner?.Property,
				ComplexTypeName = owner?.Type,
				IsNullable = true,
			}
		);

	/// <summary>The CLR type EF actually stores — the converted type for a value object.</summary>
	static Type ProviderType(IProperty property) =>
		property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;

	/// <summary>
	/// A column type set explicitly with <c>HasColumnType</c> or <c>[Column(TypeName = ...)]</c>.
	/// </summary>
	/// <remarks>
	/// Read from the annotation rather than <c>GetColumnType()</c>, which also answers with the provider's
	/// default mapping. That default is provider-specific (<c>INTEGER</c>, <c>nvarchar(max)</c>), and the
	/// diagram's generic names are deliberate; only an explicit choice — <c>decimal(18,4)</c> — is real
	/// information the generic name would lose.
	/// </remarks>
	static string? ExplicitColumnType(IProperty property) =>
		property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;

	/// <summary>
	/// The domain type a column is backed by, when its declared type is not a primitive.
	/// </summary>
	/// <param name="property">The mapped property.</param>
	/// <param name="complexType">The enclosing complex type's name, for a plain member of one.</param>
	/// <remarks>
	/// The kind strings match the AppHost's <c>EfDomainTypeKind</c> member names, which is how the
	/// emitted JSON round-trips. <c>Nullable&lt;T&gt;</c> is unwrapped first: an optional value object or
	/// enum is still that value object or enum. A primitive collection shows its element's domain type as
	/// an array.
	/// </remarks>
	static (string? Name, string? Kind) DomainType(IProperty property, string? complexType)
	{
		if (property.IsPrimitiveCollection && property.GetElementType() is { } element)
		{
			var (elementName, elementKind) = DomainTypeOf(element.ClrType);

			return elementName is null ? (null, null) : ($"{elementName}[]", elementKind);
		}

		var (name, kind) = DomainTypeOf(property.ClrType);

		if (name is not null)
		{
			return (name, kind);
		}

		return complexType is not null ? (complexType, "ComplexValueObject") : (null, null);
	}

	static (string? Name, string? Kind) DomainTypeOf(Type type)
	{
		var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

		// First, because a union's case accessor gives it the shape of a scalar value object.
		if (IsUnion(unwrapped))
		{
			return (unwrapped.Name, "Union");
		}

		if (IsValueObject(unwrapped))
		{
			return (unwrapped.Name, "ValueObject");
		}

		if (unwrapped.IsEnum)
		{
			return (unwrapped.Name, "Enum");
		}

		return (null, null);
	}

	static HashSet<IProperty> UniqueProperties(IEntityType entityType)
	{
		HashSet<IProperty> unique = [];

		foreach (var key in entityType.GetKeys())
		{
			unique.UnionWith(key.Properties);
		}

		foreach (var index in entityType.GetIndexes().Where(i => i.IsUnique))
		{
			unique.UnionWith(index.Properties);
		}

		return unique;
	}

	static int Depth(IEntityType entityType)
	{
		var depth = 0;

		for (var current = entityType.BaseType; current is not null; current = current.BaseType)
		{
			depth++;
		}

		return depth;
	}

	/// <summary>Resolves the display type, matching the AppHost's rule for value objects.</summary>
	static string ResolveStoreType(Type type)
	{
		var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

		// An enum stored as itself is its underlying integral type; one converted to a string arrives here
		// as string, the provider type.
		if (unwrapped.IsEnum)
		{
			return ResolveStoreType(Enum.GetUnderlyingType(unwrapped));
		}

		if (unwrapped == typeof(byte[]))
			return "blob";

		// The AppHost's rule for value objects is to use the provider type, which is what EF stores in the
		// database. The provider type is already unwrapped, so the nullable check above is only for enums.
		return unwrapped.FullName switch
		{
			"System.Boolean" => "bool",
			"System.Byte" or "System.SByte" => "tinyint",
			"System.Int16" or "System.UInt16" => "smallint",
			"System.Int32" or "System.UInt32" => "int",
			"System.Int64" or "System.UInt64" => "bigint",
			"System.Single" => "real",
			"System.Double" => "double",
			"System.Decimal" => "decimal",
			"System.String" => "varchar",
			"System.Char" => "char",
			"System.DateTime" => "datetime",
			"System.Guid" => "guid",
			"System.DateTimeOffset" => "datetimeoffset",
			"System.TimeSpan" => "time",
			"System.DateOnly" => "date",
			"System.TimeOnly" => "time",
			_ => UnknownStoreType,
		};
	}

	/// <summary>
	/// Whether a type is a scalar value object, by the same rules the analyzer and the metadata reader
	/// use: Purview's <c>[Scalar]</c> attributes or <c>IScalarValueObject&lt;,&gt;</c> on a struct or a
	/// class, else a non-generic struct with a public <c>Value</c> that is not typed <c>object</c>.
	/// </summary>
	/// <remarks>
	/// Only reached for a property EF has mapped, so a class here has a value converter — which is what
	/// makes a class-based value object a column rather than a navigation.
	/// </remarks>
	static bool IsValueObject(Type type)
	{
		if (type.IsEnum || type.IsPrimitive || type == typeof(string) || IsUnion(type))
		{
			return false;
		}

		if (
			type.GetCustomAttributes(inherit: false)
				.Any(a => a.GetType().Name is "ScalarAttribute" or "ScalarAttribute`1")
		)
		{
			return true;
		}

		if (type.GetInterfaces().Any(i => i.IsGenericType && i.Name == "IScalarValueObject`2"))
		{
			return true;
		}

		return type.IsValueType
			&& !type.IsGenericType
			&& type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance) is { } value
			&& value.PropertyType != typeof(object);
	}

	/// <summary>Whether a type is a C# union: marked <c>[Union]</c> or implementing <c>IUnion</c>.</summary>
	static bool IsUnion(Type type) =>
		type.IsValueType
		&& (
			type.GetCustomAttributes(inherit: false)
				.Any(a => a.GetType() is { Name: "UnionAttribute", Namespace: "System.Runtime.CompilerServices" })
			|| type.GetInterfaces().Any(i => i is { Name: "IUnion", Namespace: "System.Runtime.CompilerServices" })
		);

	// The dump contract, mirroring EfModel's JSON shape. Fidelity is omitted: a dump is authoritative,
	// which is the deserialisation default.

	sealed class DumpModel
	{
		public int SchemaVersion { get; init; } = 1;

		public List<DumpContext> Contexts { get; init; } = [];
	}

	sealed class DumpContext
	{
		public required string Name { get; init; }

		public required string TypeName { get; init; }

		public string? DefaultSchema { get; init; }

		public List<DumpTable> Tables { get; init; } = [];
	}

	sealed class DumpTable
	{
		public required string Name { get; init; }

		public required string ClrTypeName { get; init; }

		public string? Schema { get; init; }

		public bool IsJoinEntity { get; init; }

		public List<DumpColumn> Columns { get; init; } = [];

		public List<DumpForeignKey> ForeignKeys { get; init; } = [];

		public List<DumpIndex> Indexes { get; init; } = [];
	}

	sealed class DumpColumn
	{
		public required string Name { get; init; }

		public required string StoreType { get; init; }

		public string? ClrTypeName { get; init; }

		public string? DomainTypeName { get; init; }

		public string? DomainTypeKind { get; init; }

		public string? ComplexPropertyName { get; init; }

		public string? ComplexTypeName { get; init; }

		public bool IsNullable { get; init; }

		public bool IsPrimaryKey { get; init; }

		public bool IsUnique { get; init; }

		public bool IsForeignKey { get; init; }

		public int? MaxLength { get; init; }

		public string? Comment { get; init; }
	}

	sealed class DumpForeignKey
	{
		public required List<string> Columns { get; init; }

		public required string PrincipalTable { get; init; }

		public required List<string> PrincipalColumns { get; init; }

		public string? NavigationName { get; init; }

		public string? PrincipalNavigationName { get; init; }

		public string? ConstraintName { get; init; }

		public bool IsUnique { get; init; }

		public bool IsRequired { get; init; }
	}

	sealed class DumpIndex
	{
		public required List<string> Columns { get; init; }

		public bool IsUnique { get; init; }

		public string? Name { get; init; }
	}
}
