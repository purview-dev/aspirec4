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
			if (entityType.IsOwned())
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

	static DumpTable Table(IEntityType entityType)
	{
		var foreignKeys = entityType.GetForeignKeys().ToList();
		var uniqueProperties = UniqueProperties(entityType);
		var foreignKeyProperties = foreignKeys.SelectMany(fk => fk.Properties).ToHashSet();

		List<DumpColumn> columns = [];

		foreach (var (property, complexType) in Columns(entityType))
		{
			var (name, kind) = DomainType(property, complexType);

			columns.Add(
				new DumpColumn
				{
					Name = property.GetColumnName(),
					StoreType = ResolveStoreType(ProviderType(property)),
					ClrTypeName = property.ClrType.FullName,
					DomainTypeName = name,
					DomainTypeKind = kind,
					IsNullable = property.IsNullable,
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

	/// <summary>Every column of a table: the entity's own properties, then each complex type's members.</summary>
	/// <remarks>
	/// The complex type name travels with each member so the column can show it beside the database
	/// type, which is what makes a flattened complex value object legible.
	/// </remarks>
	static IEnumerable<(IProperty Property, string? ComplexType)> Columns(IEntityType entityType)
	{
		foreach (var property in entityType.GetProperties())
		{
			yield return (property, null);
		}

		foreach (var complex in entityType.GetComplexProperties())
		{
			foreach (var column in ComplexColumns(complex))
			{
				yield return column;
			}
		}
	}

	static IEnumerable<(IProperty Property, string? ComplexType)> ComplexColumns(IComplexProperty complex)
	{
		var complexName = complex.ComplexType.ClrType.Name;

		foreach (var property in complex.ComplexType.GetProperties())
		{
			yield return (property, complexName);
		}

		foreach (var nested in complex.ComplexType.GetComplexProperties())
		{
			foreach (var column in ComplexColumns(nested))
			{
				yield return column;
			}
		}
	}

	/// <summary>The CLR type EF actually stores — the converted type for a value object.</summary>
	static Type ProviderType(IProperty property) =>
		property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;

	/// <summary>
	/// The domain type a column is backed by, when its declared type is not a primitive.
	/// </summary>
	/// <param name="property">The mapped property.</param>
	/// <param name="complexType">The enclosing complex type's name, for a plain member of one.</param>
	/// <remarks>
	/// The kind strings match the AppHost's <c>EfDomainTypeKind</c> member names, which is how the
	/// emitted JSON round-trips.
	/// </remarks>
	static (string? Name, string? Kind) DomainType(IProperty property, string? complexType)
	{
		if (IsValueObject(property.ClrType))
		{
			return (property.ClrType.Name, "ValueObject");
		}

		if (property.ClrType.IsEnum)
		{
			return (property.ClrType.Name, "Enum");
		}

		return complexType is not null ? (complexType, "ComplexValueObject") : (null, null);
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

		if (unwrapped.IsEnum)
		{
			return "int";
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

	static bool IsValueObject(Type type) =>
		type.IsValueType
		&& !type.IsEnum
		&& !type.IsGenericType
		&& type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance) is not null;

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
