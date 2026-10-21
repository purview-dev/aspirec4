using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Recovers an approximate EF Core model from compiled assemblies when a source-generated descriptor
/// is not available — the case for a shared or third-party assembly the AppHost does not own.
/// </summary>
/// <remarks>
/// <para>
/// This is a metadata-only read. Assemblies are opened with a
/// <see cref="MetadataLoadContext"/>, which materialises types without loading or executing any code
/// in them, so the AppHost stays immune to version conflicts and module initialisers.
/// </para>
/// <para>
/// Fidelity is inherently limited. <c>OnModelCreating</c> is a method body and is absent from
/// metadata, so configuration applied only there — renamed tables and columns, default schemas,
/// ignored properties — cannot be recovered. Convention-based mapping and data annotations are.
/// Every context this produces is marked <see cref="EfModelFidelity.Approximate"/>.
/// </para>
/// </remarks>
static class EfModelMetadataReader
{
	const string DbContextFullName = "Microsoft.EntityFrameworkCore.DbContext";
	const string DbSetFullName = "Microsoft.EntityFrameworkCore.DbSet`1";
	const string NullableFullName = "System.Nullable`1";
	const string UnknownStoreType = "unknown";

	/// <summary>How deep nested complex value objects are flattened before the walk stops.</summary>
	const int MaxComplexDepth = 4;

	/// <summary>Generic collection types that mark a navigation as the many side.</summary>
	static readonly HashSet<string> CollectionFullNames =
	[
		with(StringComparer.Ordinal),
		"System.Collections.Generic.ICollection`1",
		"System.Collections.Generic.IEnumerable`1",
		"System.Collections.Generic.IList`1",
		"System.Collections.Generic.IReadOnlyCollection`1",
		"System.Collections.Generic.IReadOnlyList`1",
		"System.Collections.Generic.ISet`1",
		"System.Collections.Generic.List`1",
		"System.Collections.Generic.HashSet`1",
	];

	/// <summary>
	/// Reads the contexts contributed by every EF-referencing assembly in a build output directory.
	/// </summary>
	/// <param name="outputDirectory">The directory to scan.</param>
	/// <param name="assembliesWithDescriptors">
	/// File names that already carry a source-generated descriptor. Those are read at full fidelity
	/// elsewhere and must not also be recovered here, or the same context would appear twice.
	/// </param>
	internal static IReadOnlyList<EfContext> Read(
		string outputDirectory,
		IReadOnlyCollection<string> assembliesWithDescriptors
	)
	{
		if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
		{
			return [];
		}

		List<string> candidates;

		try
		{
			candidates =
			[
				.. Directory
					.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly)
					.Where(path => !assembliesWithDescriptors.Contains(Path.GetFileName(path))),
			];
		}
		catch (IOException)
		{
			return [];
		}
		catch (UnauthorizedAccessException)
		{
			return [];
		}

		return ReadAssemblies(candidates, candidates);
	}

	/// <summary>Reads the contexts contributed by a single assembly, resolving its dependencies from
	/// its own directory.</summary>
	internal static IReadOnlyList<EfContext> ReadFromAssembly(string assemblyPath)
	{
		if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
		{
			return [];
		}

		var directory = Path.GetDirectoryName(assemblyPath);
		List<string> resolverPaths = [assemblyPath];

		if (directory is not null && Directory.Exists(directory))
		{
			try
			{
				resolverPaths.AddRange(Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly));
			}
			catch (IOException)
			{
				// Fall back to resolving from the runtime alone.
			}
			catch (UnauthorizedAccessException)
			{
				// Fall back to resolving from the runtime alone.
			}
		}

		return ReadAssemblies([assemblyPath], resolverPaths);
	}

	/// <summary>Loads <paramref name="loadPaths"/> and reads the contexts from each EF-referencing one.</summary>
	static List<EfContext> ReadAssemblies(List<string> loadPaths, List<string> resolverPaths)
	{
		if (loadPaths.Count == 0)
		{
			return [];
		}

		try
		{
			using var context = CreateContext(resolverPaths);
			List<EfContext> contexts = [];

			foreach (var path in loadPaths)
			{
				if (!ReferencesEntityFramework(path))
				{
					continue;
				}

				try
				{
					contexts.AddRange(ReadAssembly(context.LoadFromAssemblyPath(path)));
				}
				catch (Exception ex) when (IsLoadFailure(ex))
				{
					// A native, ref-only or unresolvable image contributes nothing.
				}
			}

			return contexts;
		}
		catch (ArgumentException)
		{
			// A resolver that cannot be built (an unreadable or duplicate path) yields no model
			// rather than failing AppHost startup.
			return [];
		}
		catch (FileNotFoundException)
		{
			return [];
		}
	}

	/// <summary>Builds a load context that resolves against the build output first, then the runtime.</summary>
	static MetadataLoadContext CreateContext(IReadOnlyCollection<string> assemblyPaths)
	{
		// Keyed by simple name so a build output that ships a copy of a framework assembly wins over
		// the runtime's own, and so a duplicate path cannot make the resolver throw.
		Dictionary<string, string> byName = [with(StringComparer.OrdinalIgnoreCase)];

		foreach (var path in assemblyPaths)
		{
			byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
		}

		foreach (var path in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
		{
			byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
		}

		return new MetadataLoadContext(new PathAssemblyResolver(byName.Values));
	}

	/// <summary>Whether an assembly references EF Core and so could define a <c>DbContext</c>.</summary>
	/// <remarks>
	/// A cheap gate before the expensive metadata walk. Matched on a substring so an assembly that only
	/// references a provider or the abstractions package is not missed.
	/// </remarks>
	static bool ReferencesEntityFramework(string path)
	{
		try
		{
			using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using PEReader peReader = new(stream);

			if (!peReader.HasMetadata)
			{
				return false;
			}

			var reader = peReader.GetMetadataReader();

			foreach (var handle in reader.AssemblyReferences)
			{
				var name = reader.GetString(reader.GetAssemblyReference(handle).Name);

				if (name.Contains("EntityFrameworkCore", StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		catch (BadImageFormatException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}

		return false;
	}

	static IEnumerable<EfContext> ReadAssembly(Assembly assembly)
	{
		foreach (var type in GetLoadableTypes(assembly))
		{
			if (!IsDbContext(type))
			{
				continue;
			}

			yield return ReadContext(type);
		}
	}

	static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			// A type whose dependencies cannot be resolved is skipped; the rest are still readable.
			return ex.Types.Where(type => type is not null)!;
		}
	}

	static bool IsDbContext(Type type)
	{
		try
		{
			for (var current = type.BaseType; current is not null; current = current.BaseType)
			{
				if (current.FullName == DbContextFullName)
				{
					return true;
				}
			}
		}
		catch (Exception ex) when (IsLoadFailure(ex))
		{
			return false;
		}

		return false;
	}

	/// <summary>Builds one context's shape from its entity types.</summary>
	static EfContext ReadContext(Type context)
	{
		// Entity -> the name the table takes before attributes are applied.
		Dictionary<Type, string> entities = [];

		foreach (var property in context.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (IsDbSet(property.PropertyType) && property.PropertyType.GetGenericArguments()[0] is { } entity)
			{
				entities[entity] = property.Name;
			}
		}

		AddReachableEntities(entities);

		List<TableShape> tables = [];

		foreach (var pair in entities)
		{
			tables.Add(BuildTable(pair.Key, pair.Value, entities));
		}

		ResolveForeignKeyTargets(tables);

		return new EfContext
		{
			Name = context.Name,
			TypeName = context.FullName ?? context.Name,
			DefaultSchema = null,
			Fidelity = EfModelFidelity.Approximate,
			Tables = [.. tables.Select(ToTable)],
		};
	}

	static void AddReachableEntities(Dictionary<Type, string> entities)
	{
		Queue<Type> queue = new(entities.Keys);

		while (queue.Count > 0)
		{
			foreach (var property in queue.Dequeue().GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				var target = GetNavigationTarget(property.PropertyType);

				if (target is null || entities.ContainsKey(target))
				{
					continue;
				}

				entities[target] = target.Name;
				queue.Enqueue(target);
			}
		}
	}

	static TableShape BuildTable(Type entity, string defaultName, Dictionary<Type, string> entities)
	{
		TableShape table = new()
		{
			ClrTypeName = entity.FullName ?? entity.Name,
			Name = GetAttributeString(entity, "Table", 0) ?? defaultName,
			Schema = GetAttributeNamedString(entity, "Table", "Schema"),
		};

		var uniqueFromIndexes = ReadUniqueIndexProperties(entity);
		List<PropertyInfo> navigations = [];

		foreach (var property in entity.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (property.GetMethod is null || property.GetMethod.IsStatic || HasAttribute(property, "NotMapped"))
			{
				continue;
			}

			// A complex value object is flattened into its members; it is not a table of its own.
			if (IsComplexValueObject(property.PropertyType))
			{
				AddComplexColumns(table, property.Name, property.PropertyType, IsNullable(property), depth: 0);

				continue;
			}

			if (GetNavigationTarget(property.PropertyType) is not null)
			{
				navigations.Add(property);

				continue;
			}

			if (!IsScalar(property.PropertyType))
			{
				continue;
			}

			table.Columns.Add(BuildColumn(property, uniqueFromIndexes));
		}

		ApplyPrimaryKeys(entity, table);
		ApplyForeignKeys(entity, table, navigations, entities);

		var keyColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();

		table.IsJoinEntity = keyColumns.Count > 1 && keyColumns.TrueForAll(c => c.IsForeignKey);

		return table;
	}

	static ColumnShape BuildColumn(PropertyInfo property, HashSet<string> uniqueFromIndexes)
	{
		var (Name, Kind) = DomainType(property.PropertyType, complexType: null);

		return new ColumnShape
		{
			PropertyName = property.Name,
			Name = GetAttributeString(property, "Column", 0) ?? property.Name,
			StoreType =
				GetAttributeNamedString(property, "Column", "TypeName") ?? ColumnStoreType(property.PropertyType),
			ClrTypeName = property.PropertyType.FullName,
			DomainTypeName = Name,
			DomainTypeKind = Kind,
			IsNullable = IsNullable(property),
			IsUnique = uniqueFromIndexes.Contains(property.Name),
			MaxLength = GetAttributeInt(property, "MaxLength", 0) ?? GetAttributeInt(property, "StringLength", 0),
			Comment = GetAttributeString(property, "Comment", 0),
		};
	}

	/// <summary>
	/// Flattens a complex value object into columns on the owning table, the way EF maps one.
	/// </summary>
	/// <remarks>
	/// Column names are approximated as <c>{Property}_{Member}</c>. EF's own flattening is only visible
	/// with a built model, which the build-time dump has and this reader does not.
	/// </remarks>
	static void AddComplexColumns(TableShape table, string prefix, Type complexType, bool nullable, int depth)
	{
		if (depth > MaxComplexDepth)
		{
			return;
		}

		foreach (var member in Unwrap(complexType).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (member.GetMethod is null || member.GetMethod.IsStatic || HasAttribute(member, "NotMapped"))
			{
				continue;
			}

			if (IsComplexValueObject(member.PropertyType))
			{
				AddComplexColumns(
					table,
					$"{prefix}_{member.Name}",
					member.PropertyType,
					nullable || IsNullable(member),
					depth + 1
				);

				continue;
			}

			if (!IsScalar(member.PropertyType))
			{
				continue;
			}

			var (Name, Kind) = DomainType(member.PropertyType, Unwrap(complexType).Name);

			table.Columns.Add(
				new ColumnShape
				{
					PropertyName = $"{prefix}_{member.Name}",
					Name = GetAttributeString(member, "Column", 0) ?? $"{prefix}_{member.Name}",
					StoreType = ColumnStoreType(member.PropertyType),
					ClrTypeName = member.PropertyType.FullName,
					DomainTypeName = Name,
					DomainTypeKind = Kind,
					IsNullable = nullable || IsNullable(member),
					MaxLength = GetAttributeInt(member, "MaxLength", 0) ?? GetAttributeInt(member, "StringLength", 0),
					Comment = GetAttributeString(member, "Comment", 0),
				}
			);
		}
	}

	static void ApplyPrimaryKeys(Type entity, TableShape table)
	{
		List<string> keyProperties =
		[
			.. entity
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => HasAttribute(p, "Key"))
				.Select(p => p.Name),
		];

		if (keyProperties.Count == 0)
		{
			// EF's convention: a property named Id, or <TypeName>Id.
			var conventional = table.Columns.FirstOrDefault(c =>
				string.Equals(c.PropertyName, "Id", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(c.PropertyName, entity.Name + "Id", StringComparison.OrdinalIgnoreCase)
			);

			if (conventional is not null)
			{
				keyProperties.Add(conventional.PropertyName);
			}
		}

		foreach (var column in table.Columns)
		{
			if (keyProperties.Contains(column.PropertyName))
			{
				column.IsPrimaryKey = true;
				column.IsNullable = false;
			}
		}
	}

	static void ApplyForeignKeys(
		Type dependent,
		TableShape table,
		List<PropertyInfo> navigations,
		Dictionary<Type, string> entities
	)
	{
		// Only a single-column primary key constrains its own column to one row. A column that is
		// merely part of a composite key does not, so a join table renders as one-to-many on both sides.
		var isSingleColumnKey = table.Columns.Count(c => c.IsPrimaryKey) == 1;

		foreach (var navigation in navigations)
		{
			var target = GetNavigationTarget(navigation.PropertyType);

			// A collection navigation means the foreign key lives on the other entity.
			if (target is null || !entities.ContainsKey(target) || IsCollection(navigation.PropertyType))
			{
				continue;
			}

			var explicitName = GetAttributeString(navigation, "ForeignKey", 0);

			var column =
				(explicitName is null ? null : table.Columns.FirstOrDefault(c => c.PropertyName == explicitName))
				?? table.Columns.FirstOrDefault(c => c.PropertyName == navigation.Name + "Id")
				?? table.Columns.FirstOrDefault(c => c.PropertyName == target.Name + "Id");

			if (column is null)
			{
				continue;
			}

			column.IsForeignKey = true;

			table.ForeignKeys.Add(
				new ForeignKeyShape
				{
					PrincipalEntity = target.FullName ?? target.Name,
					NavigationName = navigation.Name,
					PrincipalNavigationName = PrincipalNavigation(target, dependent),
					IsUnique = (column.IsPrimaryKey && isSingleColumnKey) || column.IsUnique,
					IsRequired = !column.IsNullable,
					Columns = [column.Name],
				}
			);
		}
	}

	/// <summary>
	/// The principal-side navigation: a collection on the principal whose element is the dependent.
	/// </summary>
	static string? PrincipalNavigation(Type principal, Type dependent)
	{
		foreach (var property in principal.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (IsCollection(property.PropertyType) && property.PropertyType.GetGenericArguments()[0] == dependent)
			{
				return property.Name;
			}
		}

		return null;
	}

	/// <summary>
	/// Rewrites each foreign key's principal from a CLR type name to the resolved table name and its
	/// primary key columns, now that every table is known.
	/// </summary>
	static void ResolveForeignKeyTargets(List<TableShape> tables)
	{
		var byClrType = tables.GroupBy(t => t.ClrTypeName).ToDictionary(g => g.Key, g => g.First());

		foreach (var table in tables)
		{
			foreach (var foreignKey in table.ForeignKeys.Where(fk => byClrType.ContainsKey(fk.PrincipalEntity)))
			{
				var principal = byClrType[foreignKey.PrincipalEntity];

				foreignKey.PrincipalEntity = principal.Name;
				foreignKey.PrincipalColumns = [.. principal.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name)];
			}
		}
	}

	/// <summary>Reads <c>[Index(nameof(X), IsUnique = true)]</c> declarations from the entity type.</summary>
	static HashSet<string> ReadUniqueIndexProperties(Type entity)
	{
		HashSet<string> unique = [with(StringComparer.Ordinal)];

		foreach (var attribute in entity.GetCustomAttributesData())
		{
			if (!IsAttribute(attribute, "Index"))
			{
				continue;
			}

			var isUnique =
				attribute.NamedArguments.FirstOrDefault(a => a.MemberName == "IsUnique").TypedValue.Value as bool?;

			if (isUnique != true)
			{
				continue;
			}

			foreach (var argument in attribute.ConstructorArguments)
			{
				if (argument.Value is IEnumerable<CustomAttributeTypedArgument> elements)
				{
					foreach (var element in elements)
					{
						if (element.Value is string name)
						{
							unique.Add(name);
						}
					}
				}
				else if (argument.Value is string single)
				{
					unique.Add(single);
				}
			}
		}

		return unique;
	}

	static bool IsDbSet(Type type) => type.IsGenericType && type.GetGenericTypeDefinition().FullName == DbSetFullName;

	/// <summary>Returns the entity type a navigation points at, or <see langword="null"/> when it is not one.</summary>
	static Type? GetNavigationTarget(Type type)
	{
		if (type.IsGenericType && IsCollection(type))
		{
			return type.GetGenericArguments()[0];
		}

		if (!type.IsClass || IsComplexValueObject(type))
		{
			return null;
		}

		// A class that maps to a column (string, byte[], ...) is not a navigation.
		return !IsScalar(type) ? type : null;
	}

	static bool IsCollection(Type type) =>
		type.IsGenericType && CollectionFullNames.Contains(type.GetGenericTypeDefinition().FullName ?? string.Empty);

	/// <summary>Unwraps <c>Nullable&lt;T&gt;</c> to <c>T</c>, leaving other types untouched.</summary>
	static Type Unwrap(Type type) =>
		type.IsGenericType && type.GetGenericTypeDefinition().FullName == NullableFullName
			? type.GetGenericArguments()[0]
			: type;

	static bool IsScalar(Type type)
	{
		var unwrapped = Unwrap(type);

		if (unwrapped.IsEnum || IsValueObject(unwrapped))
		{
			return true;
		}

		// EF Core's built-in type mapping is not available here, so this is a best-effort guess based on the CLR type.
		return unwrapped.IsArray
			? unwrapped.GetElementType()?.FullName == "System.Byte"
			: ResolveStoreType(unwrapped) != UnknownStoreType;
	}

	/// <summary>
	/// Determines whether a type is a scalar value object: a struct that wraps a single value under a
	/// <c>Value</c> member, as a strongly-typed identifier does.
	/// </summary>
	/// <remarks>
	/// A struct is never an EF entity, so recognising one as a scalar is safe. The <c>Value</c> member
	/// is what the value-object generators emit — including a forwarding <c>Value</c> when the author
	/// names the underlying member something else — so this matches a strongly-typed id without
	/// depending on the library that produced it.
	/// </remarks>
	static bool IsValueObject(Type type) =>
		type.IsValueType
		&& !type.IsEnum
		&& !type.IsGenericType
		&& type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance) is not null;

	/// <summary>The scalar value-object type a property is backed by, or <see langword="null"/>.</summary>
	static Type? ValueObjectType(Type type)
	{
		var unwrapped = Unwrap(type);

		return IsValueObject(unwrapped) ? unwrapped : null;
	}

	/// <summary>The primitive a scalar value object wraps.</summary>
	static Type? ValueObjectUnderlying(Type valueObject) =>
		valueObject.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.PropertyType;

	/// <summary>The real database type for a property, resolving a scalar value object to its primitive.</summary>
	static string ColumnStoreType(Type propertyType)
	{
		var valueObject = ValueObjectType(propertyType);

		return valueObject is not null && ValueObjectUnderlying(valueObject) is { } underlying
			? ResolveStoreType(underlying)
			: ResolveStoreType(propertyType);
	}

	/// <summary>
	/// The domain type a column is backed by, when its declared type is not a primitive.
	/// </summary>
	/// <param name="type">The property or member type.</param>
	/// <param name="complexType">The enclosing complex type's name, for a plain member of one.</param>
	/// <remarks>
	/// A member's own type wins over the enclosing complex type, so an enum member of a complex value
	/// object shows the enum rather than the complex type.
	/// </remarks>
	static (string? Name, EfDomainTypeKind? Kind) DomainType(Type type, string? complexType)
	{
		var valueObject = ValueObjectType(type);

		if (valueObject is not null)
		{
			return (valueObject.Name, EfDomainTypeKind.ValueObject);
		}

		var unwrapped = Unwrap(type);

		if (unwrapped.IsEnum)
		{
			return (unwrapped.Name, EfDomainTypeKind.Enum);
		}

		return complexType is not null ? (complexType, EfDomainTypeKind.ComplexValueObject) : (null, null);
	}

	/// <summary>
	/// Whether a type is mapped as an EF Core complex type — a Purview complex value object.
	/// </summary>
	/// <remarks>Matched by attribute name, so no dependency on the value-object package is taken.</remarks>
	static bool IsComplexValueObject(Type type)
	{
		foreach (var attribute in Unwrap(type).GetCustomAttributesData())
		{
			if (attribute.AttributeType.Name is not ("ValueObjectAttribute" or "ValueObject"))
			{
				continue;
			}

			var named = attribute.NamedArguments.FirstOrDefault(a => a.MemberName == "EFMapping");

			if (IsNamedEnumMember(named.TypedValue.ArgumentType, named.TypedValue.Value, "ComplexType"))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Whether a constant is the named member of its enum type.
	/// </summary>
	/// <remarks>
	/// The compiler reports either the member or its underlying value, so both are accepted.
	/// </remarks>
	static bool IsNamedEnumMember(Type type, object? value, string member)
	{
		if (value is null)
		{
			return false;
		}

		if (string.Equals(value.ToString(), member, StringComparison.Ordinal))
		{
			return true;
		}

		// EF's attribute reader reports the underlying value, so check that too.
		return type.IsEnum && string.Equals(Enum.GetName(type, value), member, StringComparison.Ordinal);
	}

	static string ResolveStoreType(Type type)
	{
		var unwrapped = Unwrap(type);

		if (unwrapped.IsEnum)
		{
			return "int";
		}

		if (unwrapped.IsArray)
		{
			return unwrapped.GetElementType()?.FullName == "System.Byte" ? "blob" : UnknownStoreType;
		}

		// EF Core's built-in type mapping is not available here, so this is a best-effort guess based on the CLR type.
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
	/// Whether a property maps to a nullable column.
	/// </summary>
	/// <remarks>
	/// A value type is nullable only when it is <c>Nullable&lt;T&gt;</c>. For a reference type the
	/// nullable-reference annotation is read when it can be; when it cannot, the property is treated as
	/// optional rather than inventing a mandatory relationship that the model may not have.
	/// </remarks>
	static bool IsNullable(PropertyInfo property)
	{
		var type = property.PropertyType;

		if (Unwrap(type) != type)
		{
			return true;
		}

		if (type.IsValueType)
		{
			return false;
		}

		try
		{
			return new NullabilityInfoContext().Create(property).ReadState != NullabilityState.NotNull;
		}
		catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or ArgumentException)
		{
			return true;
		}
	}

	static bool HasAttribute(MemberInfo member, string name) =>
		member.GetCustomAttributesData().Any(attribute => IsAttribute(attribute, name));

	static bool IsAttribute(CustomAttributeData attribute, string name) =>
		attribute.AttributeType.Name == name || attribute.AttributeType.Name == name + "Attribute";

	static string? GetAttributeString(MemberInfo member, string name, int index) =>
		GetConstructorArgument(member, name, index)?.Value as string;

	static int? GetAttributeInt(MemberInfo member, string name, int index) =>
		GetConstructorArgument(member, name, index)?.Value as int?;

	static CustomAttributeTypedArgument? GetConstructorArgument(MemberInfo member, string name, int index)
	{
		var attribute = member.GetCustomAttributesData().FirstOrDefault(a => IsAttribute(a, name));

		return attribute is not null && attribute.ConstructorArguments.Count > index
			? attribute.ConstructorArguments[index]
			: null;
	}

	static string? GetAttributeNamedString(MemberInfo member, string name, string argumentName)
	{
		var attribute = member.GetCustomAttributesData().FirstOrDefault(a => IsAttribute(a, name));

		return attribute?.NamedArguments.FirstOrDefault(a => a.MemberName == argumentName).TypedValue.Value as string;
	}

	/// <summary>Whether an exception means an assembly simply could not be read.</summary>
	static bool IsLoadFailure(Exception exception) =>
		exception
			is BadImageFormatException
				or FileNotFoundException
				or FileLoadException
				or TypeLoadException
				or NotSupportedException
				or ArgumentException;

	static EfTable ToTable(TableShape shape) =>
		new()
		{
			Name = shape.Name,
			ClrTypeName = shape.ClrTypeName,
			Schema = shape.Schema,
			IsJoinEntity = shape.IsJoinEntity,
			Columns =
			[
				.. shape.Columns.Select(c => new EfColumn
				{
					Name = c.Name,
					StoreType = c.StoreType,
					ClrTypeName = c.ClrTypeName,
					DomainTypeName = c.DomainTypeName,
					DomainTypeKind = c.DomainTypeKind,
					IsNullable = c.IsNullable,
					IsPrimaryKey = c.IsPrimaryKey,
					IsUnique = c.IsUnique,
					IsForeignKey = c.IsForeignKey,
					MaxLength = c.MaxLength,
					Comment = c.Comment,
				}),
			],
			ForeignKeys =
			[
				.. shape.ForeignKeys.Select(fk => new EfForeignKey
				{
					Columns = fk.Columns,
					PrincipalTable = fk.PrincipalEntity,
					PrincipalColumns = fk.PrincipalColumns,
					NavigationName = fk.NavigationName,
					PrincipalNavigationName = fk.PrincipalNavigationName,
					IsUnique = fk.IsUnique,
					IsRequired = fk.IsRequired,
				}),
			],
		};

	sealed class TableShape
	{
		public required string Name { get; set; }

		public required string ClrTypeName { get; init; }

		public string? Schema { get; init; }

		public List<ColumnShape> Columns { get; } = [];

		public List<ForeignKeyShape> ForeignKeys { get; } = [];

		public bool IsJoinEntity { get; set; }
	}

	sealed class ColumnShape
	{
		public required string PropertyName { get; init; }

		public required string Name { get; init; }

		public required string StoreType { get; init; }

		public string? ClrTypeName { get; init; }

		public string? DomainTypeName { get; init; }

		public EfDomainTypeKind? DomainTypeKind { get; init; }

		public bool IsNullable { get; set; }

		public bool IsPrimaryKey { get; set; }

		public bool IsUnique { get; init; }

		public bool IsForeignKey { get; set; }

		public int? MaxLength { get; init; }

		public string? Comment { get; init; }
	}

	sealed class ForeignKeyShape
	{
		public required string PrincipalEntity { get; set; }

		public required IReadOnlyList<string> Columns { get; init; }

		public IReadOnlyList<string> PrincipalColumns { get; set; } = [];

		public string? NavigationName { get; init; }

		public string? PrincipalNavigationName { get; init; }

		public bool IsUnique { get; init; }

		public bool IsRequired { get; init; }
	}
}
