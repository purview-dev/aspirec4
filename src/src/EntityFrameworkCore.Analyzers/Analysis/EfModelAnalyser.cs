using Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Analysis;

/// <summary>
/// Walks a <c>DbContext</c> and the entity types reachable from it, producing the table/column/foreign
/// key shapes that become the emitted descriptor.
/// </summary>
static class EfModelAnalyser
{
	/// <summary>The result of analysing one context.</summary>
	internal sealed record Result(
		string Name,
		string TypeName,
		string? DefaultSchema,
		List<TableShape> Tables,
		List<(DiagnosticDescriptor Descriptor, string Argument)> Diagnostics
	);

	/// <summary>Analyses a single <c>DbContext</c> type.</summary>
	public static Result Analyse(
		INamedTypeSymbol context,
		SemanticModel semanticModel,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		List<(DiagnosticDescriptor, string)> diagnostics = [];

		var fluent = ReadFluentConfiguration(context, semanticModel, diagnostics, cancellationToken);

		// Entity -> the name the table takes before attributes and fluent config are applied.
		Dictionary<INamedTypeSymbol, string> entities = [with(SymbolEqualityComparer.Default)];

		foreach (var property in Properties(context))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (
				property.Type is INamedTypeSymbol { IsGenericType: true } set
				&& IsDbSet(set)
				&& set.TypeArguments[0] is INamedTypeSymbol entity
			)
			{
				entities[entity] = property.Name;
			}
		}

		AddReachableEntities(entities, cancellationToken);

		List<TableShape> tables = [];
		Dictionary<INamedTypeSymbol, TableShape> tableByEntity = [with(SymbolEqualityComparer.Default)];

		foreach (var pair in entities)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var table = BuildTable(pair.Key, pair.Value, fluent, entities, diagnostics);

			tables.Add(table);
			tableByEntity[pair.Key] = table;
		}

		if (tables.Count == 0)
		{
			diagnostics.Add((DiagnosticLibrary.NoEntityTypesDiscovered, context.Name));
		}

		ApplyPrincipalSideRelationships(tableByEntity);
		ResolveForeignKeyTargets(tables);

		return new Result(context.Name, context.ToDisplayString(), fluent.DefaultSchema, tables, diagnostics);
	}

	/// <summary>Every property a type exposes, including the ones it inherits.</summary>
	/// <remarks>
	/// A base entity carrying the key, timestamps and audit columns — and a base context declaring the
	/// <c>DbSet</c> properties — are both ordinary shapes, but Roslyn's <c>GetMembers</c> returns only
	/// what a type declares itself. Without walking the chain those properties are invisible, and with
	/// them go the keys, columns and navigations among them. A derived property hides a base one of the
	/// same name, which is how the compiler resolves it too.
	/// </remarks>
	static List<IPropertySymbol> Properties(INamedTypeSymbol type)
	{
		List<IPropertySymbol> properties = [];
		HashSet<string> declared = [with(StringComparer.Ordinal)];

		// Stops at object, ValueType and Enum: a special type declares no mapped property of its own.
		for (var current = type; current is { SpecialType: SpecialType.None }; current = current.BaseType)
		{
			foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
			{
				if (declared.Add(property.Name))
				{
					properties.Add(property);
				}
			}
		}

		return properties;
	}

	static FluentConfiguration ReadFluentConfiguration(
		INamedTypeSymbol context,
		SemanticModel semanticModel,
		List<(DiagnosticDescriptor, string)> diagnostics,
		CancellationToken cancellationToken
	)
	{
		foreach (var reference in context.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax(cancellationToken) is not ClassDeclarationSyntax declaration)
			{
				continue;
			}

			var method = declaration
				.Members.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(m => m.Identifier.ValueText == EfTypes.OnModelCreating);

			if (method is null)
			{
				continue;
			}

			var model =
				semanticModel.SyntaxTree == method.SyntaxTree
					? semanticModel
					: semanticModel.Compilation.GetSemanticModel(method.SyntaxTree);
			var fluent = FluentConfiguration.Read(method, model, cancellationToken);

			foreach (var (description, _) in fluent.Uninterpretable)
			{
				diagnostics.Add((DiagnosticLibrary.UninterpretableConfiguration, description));
			}

			return fluent;
		}

		return new FluentConfiguration();
	}

	/// <summary>
	/// Adds entity types reachable only through navigation properties, so a related entity that has no
	/// <c>DbSet</c> still appears on the diagram.
	/// </summary>
	static void AddReachableEntities(Dictionary<INamedTypeSymbol, string> entities, CancellationToken cancellationToken)
	{
		Queue<INamedTypeSymbol> queue = new(entities.Keys);

		while (queue.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var property in Properties(queue.Dequeue()))
			{
				var target = GetNavigationTarget(property.Type);

				if (target is null || entities.ContainsKey(target))
				{
					continue;
				}

				entities[target] = target.Name;
				queue.Enqueue(target);
			}
		}
	}

	static TableShape BuildTable(
		INamedTypeSymbol entity,
		string defaultName,
		FluentConfiguration fluent,
		Dictionary<INamedTypeSymbol, string> entities,
		List<(DiagnosticDescriptor, string)> diagnostics
	)
	{
		fluent.Entities.TryGetValue(entity.ToDisplayString(), out var config);

		TableShape table = new()
		{
			ClrTypeName = entity.ToDisplayString(),
			Name = config?.TableName ?? GetAttributeString(entity, EfTypes.Attributes.Table, 0) ?? defaultName,
			Schema = config?.Schema ?? GetAttributeNamedString(entity, EfTypes.Attributes.Table, "Schema"),
		};

		var uniqueFromIndexes = ReadUniqueIndexProperties(entity);
		List<IPropertySymbol> navigations = [];

		foreach (var property in Properties(entity))
		{
			if (
				property.DeclaredAccessibility != Accessibility.Public
				|| property.IsStatic
				|| property.GetMethod is null
				|| HasAttribute(property, EfTypes.Attributes.NotMapped)
				|| config?.IgnoredProperties.Contains(property.Name) == true
			)
			{
				continue;
			}

			switch (StoreTypeMap.ComplexValueObjectMapping(property.Type))
			{
				case ComplexMapping.Flattened:
					AddComplexColumns(
						table,
						property.Name,
						property.Type,
						StoreTypeMap.IsNullable(property.Type),
						0,
						property.Name,
						StoreTypeMap.Unwrap(property.Type).Name
					);

					continue;

				case ComplexMapping.Json:
					table.Columns.Add(
						BuildColumn(
							property,
							config,
							uniqueFromIndexes,
							StoreTypeMap.Json,
							(StoreTypeMap.Unwrap(property.Type).Name, EfTypes.DomainTypeKinds.ComplexValueObject)
						)
					);

					continue;

				case ComplexMapping.None:
				default:
					break;
			}

			// A collection of primitives or value objects is a column, not a navigation: EF Core stores a
			// primitive collection as JSON, and a complex collection can only be JSON-mapped.
			if (ScalarCollectionElement(property.Type) is { } element)
			{
				table.Columns.Add(
					BuildColumn(property, config, uniqueFromIndexes, StoreTypeMap.Json, CollectionDomainType(element))
				);

				continue;
			}

			if (GetNavigationTarget(property.Type) is not null)
			{
				navigations.Add(property);

				continue;
			}

			if (!StoreTypeMap.IsScalar(property.Type))
			{
				continue;
			}

			table.Columns.Add(BuildColumn(property, config, uniqueFromIndexes));
		}

		ApplyPrimaryKeys(entity, table, config);
		ApplyForeignKeys(entity, table, navigations, entities);

		if (!table.Columns.Any(c => c.IsPrimaryKey))
		{
			diagnostics.Add((DiagnosticLibrary.NoPrimaryKeyDiscovered, entity.Name));
		}

		// A join table carries no data of its own: every key column is also a foreign key, and a
		// single-column key cannot be joining two things.
		var keyColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();

		table.IsJoinEntity = keyColumns.Count > 1 && keyColumns.TrueForAll(c => c.IsForeignKey);

		return table;
	}

	/// <param name="property">The mapped property.</param>
	/// <param name="config">Its entity's fluent configuration, when there is any.</param>
	/// <param name="uniqueFromIndexes">Properties a unique index covers.</param>
	/// <param name="inferredStoreType">
	/// The store type to show when nothing is configured, for a column whose CLR type does not say — a
	/// JSON-mapped value object or a collection. Configuration still wins.
	/// </param>
	/// <param name="domain">The domain type to record, in place of the one the property type implies.</param>
	static ColumnShape BuildColumn(
		IPropertySymbol property,
		EntityConfiguration? config,
		HashSet<string> uniqueFromIndexes,
		string? inferredStoreType = null,
		(string? Name, string? Kind)? domain = null
	)
	{
		PropertyConfiguration? fluentProperty = null;

		if (config is not null && config.Properties.TryGetValue(property.Name, out var found))
		{
			fluentProperty = found;
		}

		var maxLength = fluentProperty?.MaxLength
			?? GetAttributeInt(property, EfTypes.Attributes.MaxLength, 0)
			?? GetAttributeInt(property, EfTypes.Attributes.StringLength, 0);

		var isRequired =
			fluentProperty?.IsRequired == true
			|| HasAttribute(property, EfTypes.Attributes.Required)
			|| !StoreTypeMap.IsNullable(property.Type);

		var (name, kind) = domain ?? DomainType(property.Type, complexType: null);

		return new ColumnShape
		{
			PropertyName = property.Name,
			Name = fluentProperty?.ColumnName
				?? GetAttributeString(property, EfTypes.Attributes.Column, 0)
				?? property.Name,
			StoreType = fluentProperty?.ColumnType
				?? GetAttributeNamedString(property, EfTypes.Attributes.Column, "TypeName")
				?? inferredStoreType
				?? StoreTypeMap.ColumnStoreType(property.Type),
			ClrTypeName = property.Type.ToDisplayString(),
			DomainTypeName = name,
			DomainTypeKind = kind,
			IsNullable = !isRequired,
			IsUnique =
				uniqueFromIndexes.Contains(property.Name) || config?.UniqueProperties.Contains(property.Name) == true,
			MaxLength = maxLength,
			Comment = fluentProperty?.Comment ?? GetAttributeString(property, EfTypes.Attributes.Comment, 0),
		};
	}

	/// <summary>How deep nested complex value objects are flattened before the walk stops.</summary>
	const int MaxComplexDepth = 4;

	/// <summary>
	/// Flattens a complex value object into columns on the owning table, the way EF maps one.
	/// </summary>
	/// <remarks>
	/// Column names are approximated as <c>{Property}_{Member}</c>. EF's own flattening is only visible
	/// with a built model, which the build-time dump has and this analyser does not.
	/// <para>
	/// Every member records the outermost property it came from in
	/// <see cref="ColumnShape.ComplexPropertyName"/>, whatever its own type: a member that is itself a
	/// scalar value object or an enum carries that type, so without the owner a renderer could not tell it
	/// belongs to the group.
	/// </para>
	/// </remarks>
	static void AddComplexColumns(
		TableShape table,
		string prefix,
		ITypeSymbol complexType,
		bool nullable,
		int depth,
		string ownerProperty,
		string ownerType
	)
	{
		if (depth > MaxComplexDepth || StoreTypeMap.Unwrap(complexType) is not INamedTypeSymbol named)
		{
			return;
		}

		foreach (var member in Properties(named))
		{
			if (
				member.DeclaredAccessibility != Accessibility.Public
				|| member.IsStatic
				|| member.GetMethod is null
				|| HasAttribute(member, EfTypes.Attributes.NotMapped)
			)
			{
				continue;
			}

			var path = $"{prefix}_{member.Name}";
			var memberNullable = nullable || StoreTypeMap.IsNullable(member.Type);
			var mapping = StoreTypeMap.ComplexValueObjectMapping(member.Type);

			if (mapping == ComplexMapping.Flattened)
			{
				AddComplexColumns(table, path, member.Type, memberNullable, depth + 1, ownerProperty, ownerType);

				continue;
			}

			string storeType;
			(string? Name, string? Kind) domain;

			if (mapping == ComplexMapping.Json)
			{
				storeType = StoreTypeMap.Json;
				domain = (StoreTypeMap.Unwrap(member.Type).Name, EfTypes.DomainTypeKinds.ComplexValueObject);
			}
			else if (ScalarCollectionElement(member.Type) is { } element)
			{
				storeType = StoreTypeMap.Json;
				domain = CollectionDomainType(element);
			}
			else if (StoreTypeMap.IsScalar(member.Type))
			{
				storeType = StoreTypeMap.ColumnStoreType(member.Type);
				domain = DomainType(member.Type, named.Name);
			}
			else
			{
				continue;
			}

			table.Columns.Add(
				new ColumnShape
				{
					PropertyName = path,
					Name = GetAttributeString(member, EfTypes.Attributes.Column, 0) ?? path,
					StoreType = GetAttributeNamedString(member, EfTypes.Attributes.Column, "TypeName") ?? storeType,
					ClrTypeName = member.Type.ToDisplayString(),
					DomainTypeName = domain.Name,
					DomainTypeKind = domain.Kind,
					ComplexPropertyName = ownerProperty,
					ComplexTypeName = ownerType,
					IsNullable = memberNullable,
					MaxLength = GetAttributeInt(member, EfTypes.Attributes.MaxLength, 0)
						?? GetAttributeInt(member, EfTypes.Attributes.StringLength, 0),
					Comment = GetAttributeString(member, EfTypes.Attributes.Comment, 0),
				}
			);
		}
	}

	/// <summary>
	/// The element type of a collection EF Core stores in a single JSON column, or <see langword="null"/>
	/// when the collection is a navigation.
	/// </summary>
	/// <remarks>
	/// A primitive collection (<c>List&lt;string&gt;</c>, <c>Guid[]</c>), a collection of scalar value
	/// objects or enums, and a collection of complex value objects are all one JSON column. A collection of
	/// entities — or of <c>[Owned]</c> types, which EF gives a table of their own — is a navigation. Before
	/// this distinction every collection was taken for a navigation, so <c>List&lt;string&gt; Tags</c>
	/// produced a phantom <c>String</c> table with <c>Length</c> and <c>Chars</c> columns.
	/// </remarks>
	static ITypeSymbol? ScalarCollectionElement(ITypeSymbol type)
	{
		var element = type switch
		{
			IArrayTypeSymbol { ElementType.SpecialType: not SpecialType.System_Byte } array => array.ElementType,
			INamedTypeSymbol { IsGenericType: true } generic when IsCollection(generic) => generic.TypeArguments[0],
			_ => null,
		};

		if (element is null || HasAttribute(StoreTypeMap.Unwrap(element), "Owned"))
		{
			return null;
		}

		return StoreTypeMap.IsScalar(element) || StoreTypeMap.ComplexValueObjectMapping(element) != ComplexMapping.None
			? element
			: null;
	}

	/// <summary>
	/// The domain type shown for a JSON collection column: the element's own domain type, as an array —
	/// <c>AssetId[]</c>, <c>Colour[]</c> — or none for a collection of primitives.
	/// </summary>
	static (string? Name, string? Kind) CollectionDomainType(ITypeSymbol element)
	{
		if (StoreTypeMap.ComplexValueObjectMapping(element) != ComplexMapping.None)
		{
			return ($"{StoreTypeMap.Unwrap(element).Name}[]", EfTypes.DomainTypeKinds.ComplexValueObject);
		}

		var (name, kind) = DomainType(element, complexType: null);

		return name is null ? (null, null) : ($"{name}[]", kind);
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
	static (string? Name, string? Kind) DomainType(ITypeSymbol type, string? complexType)
	{
		// First, because a union's case accessor gives it the shape of a scalar value object.
		if (StoreTypeMap.IsUnion(type))
		{
			return (StoreTypeMap.Unwrap(type).Name, EfTypes.DomainTypeKinds.Union);
		}

		var valueObject = StoreTypeMap.ValueObjectType(type);

		if (valueObject is not null)
		{
			return (valueObject.Name, EfTypes.DomainTypeKinds.ValueObject);
		}

		var unwrapped = StoreTypeMap.Unwrap(type);

		if (unwrapped.TypeKind == TypeKind.Enum)
		{
			return (unwrapped.Name, EfTypes.DomainTypeKinds.Enum);
		}

		return complexType is not null ? (complexType, EfTypes.DomainTypeKinds.ComplexValueObject) : (null, null);
	}

	static void ApplyPrimaryKeys(INamedTypeSymbol entity, TableShape table, EntityConfiguration? config)
	{
		List<string> keyProperties = [];

		if (config is not null && config.KeyProperties.Count > 0)
		{
			keyProperties.AddRange(config.KeyProperties);
		}
		else
		{
			keyProperties.AddRange(
				Properties(entity).Where(p => HasAttribute(p, EfTypes.Attributes.Key)).Select(p => p.Name)
			);
		}

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
		INamedTypeSymbol dependent,
		TableShape table,
		List<IPropertySymbol> navigations,
		Dictionary<INamedTypeSymbol, string> entities
	)
	{
		// Only a single-column primary key constrains its own column to one row. A column that is
		// merely part of a composite key — the usual shape of a join table — does not, so treating it
		// as unique would render every join as one-to-one.
		var isSingleColumnKey = table.Columns.Count(c => c.IsPrimaryKey) == 1;

		foreach (var navigation in navigations)
		{
			var target = GetNavigationTarget(navigation.Type);

			// A collection navigation means the foreign key lives on the other entity, so it is
			// discovered when that entity is analysed rather than here.
			if (target is null || !entities.ContainsKey(target) || IsCollection(navigation.Type))
			{
				continue;
			}

			var explicitName = GetAttributeString(navigation, EfTypes.Attributes.ForeignKey, 0);

			var column = (
				explicitName is null ? null : table.Columns.FirstOrDefault(c => c.PropertyName == explicitName)
			)
				?? table.Columns.FirstOrDefault(c => c.PropertyName == navigation.Name + "Id")
				?? table.Columns.FirstOrDefault(c => c.PropertyName == target.Name + "Id")
				?? SharedPrimaryKeyColumn(table, target);

			if (column is null)
			{
				continue;
			}

			column.IsForeignKey = true;

			// A foreign key that is also the whole key, or is uniquely indexed, can only match one
			// principal row — that is what makes a relationship one-to-one rather than one-to-many.
			var isUnique = (column.IsPrimaryKey && isSingleColumnKey) || column.IsUnique;

			ForeignKeyShape foreignKey = new()
			{
				PrincipalEntity = target.ToDisplayString(),
				NavigationName = navigation.Name,
				PrincipalNavigationName = PrincipalNavigation(target, dependent, isUnique),
				IsUnique = isUnique,
				IsRequired = !column.IsNullable,
			};

			foreignKey.Columns.Add(column.Name);
			table.ForeignKeys.Add(foreignKey);
		}
	}

	/// <summary>
	/// The dependent's own primary key, when that key doubles as the foreign key to the principal.
	/// </summary>
	/// <remarks>
	/// A shared-primary-key one-to-one — <c>RepositoryState.Id</c> being the <c>Asset</c> it describes —
	/// declares no <c>&lt;Navigation&gt;Id</c> property, so the usual column lookup finds nothing and the
	/// relationship disappears entirely. What identifies it is the dependent having borrowed the
	/// principal's identity: the principal's key type is its own identifier, and the dependent is keyed by
	/// that same type. A primitive key is deliberately not enough, because every entity keyed by
	/// <c>Guid</c> or <c>int</c> would then match every other one.
	/// </remarks>
	static ColumnShape? SharedPrimaryKeyColumn(TableShape table, INamedTypeSymbol principal)
	{
		var keyColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();

		if (keyColumns.Count != 1 || PrimaryKeyType(principal) is not { } principalKeyType)
		{
			return null;
		}

		return
			StoreTypeMap.ValueObjectType(principalKeyType) is not null
			&& IsOwnIdentifier(principal.Name, principalKeyType.Name)
			&& keyColumns[0].ClrTypeName == principalKeyType.ToDisplayString()
			? keyColumns[0]
			: null;
	}

	/// <summary>
	/// Whether a key type is the entity's own identifier, judged by name: <c>AssetId</c> belongs to
	/// <c>Asset</c> — or <c>AssetEntity</c> — and so marks that entity as the owner of the identity
	/// rather than a borrower of someone else's.
	/// </summary>
	static bool IsOwnIdentifier(string entityName, string keyTypeName)
	{
		const string idSuffix = "Id";
		const string entitySuffix = "Entity";

		if (!keyTypeName.EndsWith(idSuffix, StringComparison.Ordinal))
		{
			return false;
		}

		var stem = keyTypeName.Substring(0, keyTypeName.Length - idSuffix.Length);
		var owner = entityName.EndsWith(entitySuffix, StringComparison.Ordinal)
			? entityName.Substring(0, entityName.Length - entitySuffix.Length)
			: entityName;

		return stem.Length > 0 && string.Equals(stem, owner, StringComparison.Ordinal);
	}

	/// <summary>The type of an entity's single primary key property, following the same conventions as
	/// <see cref="ApplyPrimaryKeys"/>; <see langword="null"/> for a composite or unrecognised key.</summary>
	static ITypeSymbol? PrimaryKeyType(INamedTypeSymbol entity)
	{
		var properties = Properties(entity);
		var keyed = properties.Where(p => HasAttribute(p, EfTypes.Attributes.Key)).ToList();

		if (keyed.Count > 0)
		{
			return keyed.Count == 1 ? keyed[0].Type : null;
		}

		return properties
			.FirstOrDefault(p =>
				string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(p.Name, entity.Name + "Id", StringComparison.OrdinalIgnoreCase)
			)
			?.Type;
	}

	/// <summary>
	/// The principal-side navigation: a collection on the principal whose element is the dependent.
	/// </summary>
	/// <param name="principal">The entity the foreign key points at.</param>
	/// <param name="dependent">The entity the foreign key lives on.</param>
	/// <param name="isUnique">
	/// Whether the foreign key is unique. A one-to-one has no collection to find, so its inverse is a
	/// reference to the dependent — which is only safe to read as the inverse when the key says the
	/// relationship is one-to-one, otherwise any two entities that point at each other would pair up.
	/// </param>
	static string? PrincipalNavigation(INamedTypeSymbol principal, INamedTypeSymbol dependent, bool isUnique)
	{
		var properties = Properties(principal);

		foreach (var property in properties)
		{
			if (
				IsCollection(property.Type)
				&& property.Type is INamedTypeSymbol { IsGenericType: true } generic
				&& SymbolEqualityComparer.Default.Equals(generic.TypeArguments[0], dependent)
			)
			{
				return property.Name;
			}
		}

		return isUnique
			? properties
				.FirstOrDefault(p => SymbolEqualityComparer.Default.Equals(StoreTypeMap.Unwrap(p.Type), dependent))
				?.Name
			: null;
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
			// A foreign key whose principal is not in the analysed set keeps its CLR type name; the
			// AppHost drops those edges rather than emitting a dangling reference.
			foreach (var foreignKey in table.ForeignKeys.Where(fk => byClrType.ContainsKey(fk.PrincipalEntity)))
			{
				var principal = byClrType[foreignKey.PrincipalEntity];

				foreignKey.PrincipalEntity = principal.Name;
				foreignKey.PrincipalColumns.AddRange(
					PrincipalColumns(foreignKey, table, [.. principal.Columns.Where(c => c.IsPrimaryKey)])
				);
			}
		}
	}

	/// <summary>The principal key columns a foreign key's columns point at, one for one.</summary>
	/// <remarks>
	/// The foreign key found from a navigation always has one column, but the principal's key can have
	/// several. Attaching every key column produced a reference whose two sides differ in length — an
	/// edge d2 paired with whichever key column came first, and DBML that does not parse. Each column is
	/// paired by type, then by name, so a typed id such as <c>OrderId</c> lands on the key column of that
	/// type.
	/// </remarks>
	static List<string> PrincipalColumns(ForeignKeyShape foreignKey, TableShape dependent, List<ColumnShape> key)
	{
		if (key.Count == 0 || key.Count == foreignKey.Columns.Count)
		{
			return [.. key.Select(c => c.Name)];
		}

		List<string> paired = [];

		foreach (var name in foreignKey.Columns)
		{
			var column = dependent.Columns.FirstOrDefault(c => c.Name == name);
			var sameType = key.Where(k => column is not null && k.ClrTypeName == column.ClrTypeName).ToList();

			var match =
				sameType.Count == 1
					? sameType[0]
					: key.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase))
						?? sameType.FirstOrDefault()
						?? key[0];

			paired.Add(match.Name);
		}

		return paired;
	}

	/// <summary>
	/// Adds the relationships EF Core discovers from the principal's side alone: a collection navigation
	/// on the principal, and no reference navigation — or no matching foreign-key property — on the
	/// dependent.
	/// </summary>
	/// <remarks>
	/// <see cref="ApplyForeignKeys"/> works from the dependent's reference navigations, so a relationship
	/// declared only as <c>Asset.Tags</c> used to vanish. EF matches the foreign key by name —
	/// <c>{Principal}{Key}</c>, then the key's own name when it already starts with the principal's,
	/// then <c>{Principal}Id</c> — and, when no property matches, adds a shadow foreign key under that
	/// first name. Both are reproduced, so the column the database really has is shown. A collection
	/// answered by a collection on the other side is a many-to-many, which EF maps through a join table
	/// of its own, and is left alone.
	/// </remarks>
	static void ApplyPrincipalSideRelationships(Dictionary<INamedTypeSymbol, TableShape> tables)
	{
		foreach (var pair in tables)
		{
			var principal = pair.Key;
			var key = pair.Value.Columns.Where(c => c.IsPrimaryKey).ToList();

			if (key.Count != 1)
			{
				continue;
			}

			var principalName = principal.ToDisplayString();

			foreach (var navigation in Properties(principal))
			{
				if (
					!IsCollection(navigation.Type)
					|| GetNavigationTarget(navigation.Type) is not { } dependent
					|| !tables.TryGetValue(dependent, out var dependentTable)
					|| dependentTable.ForeignKeys.Any(fk => fk.PrincipalEntity == principalName)
				)
				{
					continue;
				}

				// On a self-referencing entity the navigation is its own dependent's property, and is not an inverse.
				var inverse = Properties(dependent)
					.Where(p =>
						SymbolEqualityComparer.Default.Equals(GetNavigationTarget(p.Type), principal)
						&& !SymbolEqualityComparer.Default.Equals(p, navigation)
					)
					.ToList();

				if (inverse.Any(p => IsCollection(p.Type)))
				{
					continue;
				}

				var column = ForeignKeyByConvention(dependentTable, principal.Name, key[0])
					?? AddShadowForeignKey(dependentTable, principal.Name, key[0]);

				column.IsForeignKey = true;

				ForeignKeyShape foreignKey = new()
				{
					PrincipalEntity = principalName,
					NavigationName = inverse.Count == 1 ? inverse[0].Name : null,
					PrincipalNavigationName = navigation.Name,
					IsRequired = !column.IsNullable,
				};

				foreignKey.Columns.Add(column.Name);
				dependentTable.ForeignKeys.Add(foreignKey);
			}
		}
	}

	static ColumnShape? ForeignKeyByConvention(TableShape dependent, string principal, ColumnShape key)
	{
		string[] candidates = [ShadowForeignKeyName(principal, key), $"{principal}Id"];

		return candidates
			.Select(name =>
				dependent.Columns.FirstOrDefault(c =>
					!c.IsForeignKey && string.Equals(c.PropertyName, name, StringComparison.OrdinalIgnoreCase)
				)
			)
			.FirstOrDefault(c => c is not null);
	}

	/// <summary>
	/// The name EF Core gives a foreign key it has to invent: the principal's name and its key's, unless
	/// the key's name already starts with the principal's.
	/// </summary>
	static string ShadowForeignKeyName(string principal, ColumnShape key) =>
		key.PropertyName.StartsWith(principal, StringComparison.OrdinalIgnoreCase)
			? key.PropertyName
			: principal + key.PropertyName;

	/// <summary>
	/// Adds the shadow foreign-key column EF Core creates when the dependent declares none — optional,
	/// because nothing on the dependent requires it, and typed like the key it references.
	/// </summary>
	static ColumnShape AddShadowForeignKey(TableShape dependent, string principal, ColumnShape key)
	{
		var name = ShadowForeignKeyName(principal, key);

		ColumnShape column = new()
		{
			PropertyName = name,
			Name = name,
			StoreType = key.StoreType,
			ClrTypeName = key.ClrTypeName,
			DomainTypeName = key.DomainTypeName,
			DomainTypeKind = key.DomainTypeKind,
			IsNullable = true,
		};

		dependent.Columns.Add(column);

		return column;
	}

	/// <summary>Reads <c>[Index(nameof(X), IsUnique = true)]</c> declarations from the entity type.</summary>
	static HashSet<string> ReadUniqueIndexProperties(INamedTypeSymbol entity)
	{
		HashSet<string> unique = [with(StringComparer.Ordinal)];

		foreach (var attribute in entity.GetAttributes())
		{
			if (!IsAttribute(attribute, EfTypes.Attributes.Index))
			{
				continue;
			}

			var isUnique = attribute
				.NamedArguments.Where(a => a.Key == "IsUnique")
				.Select(a => a.Value.Value as bool?)
				.FirstOrDefault();

			if (isUnique != true)
			{
				continue;
			}

			foreach (var argument in attribute.ConstructorArguments)
			{
				if (argument.Kind == TypedConstantKind.Array)
				{
					foreach (var element in argument.Values)
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

	/// <summary>Returns the entity type a navigation points at, or <see langword="null"/> when it is not one.</summary>
	static INamedTypeSymbol? GetNavigationTarget(ITypeSymbol type)
	{
		if (type is INamedTypeSymbol { IsGenericType: true } generic && IsCollection(type))
		{
			return ScalarCollectionElement(type) is null
				? Normalise(generic.TypeArguments[0] as INamedTypeSymbol)
				: null;
		}

		if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class)
		{
			return null;
		}

		// A complex value object is flattened into the owner's table, or held in a JSON column on it; it is
		// never an entity of its own.
		if (StoreTypeMap.ComplexValueObjectMapping(named) != ComplexMapping.None)
		{
			return null;
		}

		// A framework type is never an entity in someone's model. Any class this analyser does not
		// recognise as a scalar would otherwise be taken for a navigation and gain a table of its own,
		// named after the framework type — so a single unmapped type turns into a phantom entity, and
		// the walk then follows its properties into more of them.
		if (IsFrameworkType(named))
		{
			return null;
		}

		// A class that maps to a column (string, byte[], ...) is not a navigation.
		return named.SpecialType == SpecialType.None && !StoreTypeMap.IsScalar(named) ? Normalise(named) : null;
	}

	/// <summary>
	/// Whether a type belongs to the framework rather than to the model being analysed.
	/// </summary>
	/// <remarks>
	/// Matched on the namespace root, so a user type is only ever caught by this if it was declared
	/// directly in <c>System</c> or <c>Microsoft</c> — which EF would not map as an entity either.
	/// </remarks>
	static bool IsFrameworkType(INamedTypeSymbol type)
	{
		var root = type.ContainingNamespace;

		if (root is null || root.IsGlobalNamespace)
		{
			return false;
		}

		while (root.ContainingNamespace is { IsGlobalNamespace: false } parent)
		{
			root = parent;
		}

		return root.Name is "System" or "Microsoft";
	}

	/// <summary>
	/// Strips the nullable annotation from a navigation's target type.
	/// </summary>
	/// <remarks>
	/// A nullable reference navigation (<c>public Blog? Blog { get; set; }</c>) renders as <c>Blog?</c>
	/// via <c>ToDisplayString()</c>, which would never match the principal table's unannotated CLR type
	/// name and so would silently drop the foreign key.
	/// </remarks>
	static INamedTypeSymbol? Normalise(INamedTypeSymbol? type) =>
		type?.WithNullableAnnotation(NullableAnnotation.None) as INamedTypeSymbol;

	static bool IsDbSet(INamedTypeSymbol type) =>
		type.OriginalDefinition.MetadataName == EfTypes.DbSetMetadataName
		&& type.OriginalDefinition.ContainingNamespace?.ToDisplayString() == EfTypes.EfNamespace;

	static bool IsCollection(ITypeSymbol type) =>
		type is INamedTypeSymbol { IsGenericType: true } generic
		&& EfTypes.CollectionMetadataNames.Contains(generic.OriginalDefinition.MetadataName)
		&& generic.OriginalDefinition.ContainingNamespace?.ToDisplayString() == EfTypes.CollectionsNamespace;

	static bool HasAttribute(ISymbol symbol, string name) => symbol.GetAttributes().Any(a => IsAttribute(a, name));

	static bool IsAttribute(AttributeData attribute, string name)
	{
		var className = attribute.AttributeClass?.Name;

		return className == name || className == name + "Attribute";
	}

	static string? GetAttributeString(ISymbol symbol, string name, int index) =>
		GetAttributeArgument(symbol, name, index)?.Value as string;

	static int? GetAttributeInt(ISymbol symbol, string name, int index) =>
		GetAttributeArgument(symbol, name, index)?.Value as int?;

	static TypedConstant? GetAttributeArgument(ISymbol symbol, string name, int index)
	{
		var attribute = symbol.GetAttributes().FirstOrDefault(a => IsAttribute(a, name));

		return attribute is not null && attribute.ConstructorArguments.Length > index
			? attribute.ConstructorArguments[index]
			: null;
	}

	static string? GetAttributeNamedString(ISymbol symbol, string name, string argumentName)
	{
		var attribute = symbol.GetAttributes().FirstOrDefault(a => IsAttribute(a, name));

		return attribute
			?.NamedArguments.Where(a => a.Key == argumentName)
			.Select(a => a.Value.Value as string)
			.FirstOrDefault();
	}
}
