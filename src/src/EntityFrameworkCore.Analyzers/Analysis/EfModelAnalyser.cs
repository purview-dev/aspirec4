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
	public static Result Analyse(INamedTypeSymbol context, SemanticModel semanticModel)
	{
		List<(DiagnosticDescriptor, string)> diagnostics = [];

		var fluent = ReadFluentConfiguration(context, semanticModel, diagnostics);

		// Entity -> the name the table takes before attributes and fluent config are applied.
		Dictionary<INamedTypeSymbol, string> entities = [with(SymbolEqualityComparer.Default)];

		foreach (var property in context.GetMembers().OfType<IPropertySymbol>())
		{
			if (
				property.Type is INamedTypeSymbol { IsGenericType: true } set
				&& IsDbSet(set)
				&& set.TypeArguments[0] is INamedTypeSymbol entity
			)
			{
				entities[entity] = property.Name;
			}
		}

		AddReachableEntities(entities);

		List<TableShape> tables = [];

		foreach (var pair in entities)
		{
			tables.Add(BuildTable(pair.Key, pair.Value, fluent, entities, diagnostics));
		}

		if (tables.Count == 0)
		{
			diagnostics.Add((DiagnosticLibrary.NoEntityTypesDiscovered, context.Name));
		}

		ResolveForeignKeyTargets(tables);

		return new Result(context.Name, context.ToDisplayString(), fluent.DefaultSchema, tables, diagnostics);
	}

	static FluentConfiguration ReadFluentConfiguration(
		INamedTypeSymbol context,
		SemanticModel semanticModel,
		List<(DiagnosticDescriptor, string)> diagnostics
	)
	{
		foreach (var reference in context.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax() is not ClassDeclarationSyntax declaration)
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

			// The semantic model must belong to the same tree as the syntax being asked about.
			var model = semanticModel.SyntaxTree == method.SyntaxTree ? semanticModel : null;

			if (model is null)
			{
				continue;
			}

			var fluent = FluentConfiguration.Read(method, model);

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
	static void AddReachableEntities(Dictionary<INamedTypeSymbol, string> entities)
	{
		Queue<INamedTypeSymbol> queue = new(entities.Keys);

		while (queue.Count > 0)
		{
			foreach (var property in queue.Dequeue().GetMembers().OfType<IPropertySymbol>())
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

		foreach (var property in entity.GetMembers().OfType<IPropertySymbol>())
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
		ApplyForeignKeys(table, navigations, entities);

		if (!table.Columns.Any(c => c.IsPrimaryKey))
		{
			diagnostics.Add((DiagnosticLibrary.NoPrimaryKeyDiscovered, entity.Name));
		}

		return table;
	}

	static ColumnShape BuildColumn(
		IPropertySymbol property,
		EntityConfiguration? config,
		HashSet<string> uniqueFromIndexes
	)
	{
		PropertyConfiguration? fluentProperty = null;

		if (config is not null && config.Properties.TryGetValue(property.Name, out var found))
		{
			fluentProperty = found;
		}

		var maxLength =
			fluentProperty?.MaxLength
			?? GetAttributeInt(property, EfTypes.Attributes.MaxLength, 0)
			?? GetAttributeInt(property, EfTypes.Attributes.StringLength, 0);

		var isRequired =
			fluentProperty?.IsRequired == true
			|| HasAttribute(property, EfTypes.Attributes.Required)
			|| !StoreTypeMap.IsNullable(property.Type);

		return new ColumnShape
		{
			PropertyName = property.Name,
			Name =
				fluentProperty?.ColumnName
				?? GetAttributeString(property, EfTypes.Attributes.Column, 0)
				?? property.Name,
			StoreType =
				fluentProperty?.ColumnType
				?? GetAttributeNamedString(property, EfTypes.Attributes.Column, "TypeName")
				?? StoreTypeMap.Resolve(property.Type),
			ClrTypeName = property.Type.ToDisplayString(),
			IsNullable = !isRequired,
			IsUnique =
				uniqueFromIndexes.Contains(property.Name) || config?.UniqueProperties.Contains(property.Name) == true,
			MaxLength = maxLength,
		};
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
				entity
					.GetMembers()
					.OfType<IPropertySymbol>()
					.Where(p => HasAttribute(p, EfTypes.Attributes.Key))
					.Select(p => p.Name)
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
		TableShape table,
		List<IPropertySymbol> navigations,
		Dictionary<INamedTypeSymbol, string> entities
	)
	{
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

			var column =
				(explicitName is null ? null : table.Columns.FirstOrDefault(c => c.PropertyName == explicitName))
				?? table.Columns.FirstOrDefault(c => c.PropertyName == navigation.Name + "Id")
				?? table.Columns.FirstOrDefault(c => c.PropertyName == target.Name + "Id");

			if (column is null)
			{
				continue;
			}

			column.IsForeignKey = true;

			ForeignKeyShape foreignKey = new()
			{
				PrincipalEntity = target.ToDisplayString(),
				NavigationName = navigation.Name,
			};

			foreignKey.Columns.Add(column.Name);
			table.ForeignKeys.Add(foreignKey);
		}
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
				foreignKey.PrincipalColumns.AddRange(principal.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
			}
		}
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
			return Normalise(generic.TypeArguments[0] as INamedTypeSymbol);
		}

		if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class)
		{
			return null;
		}

		// A class that maps to a column (string, byte[], ...) is not a navigation.
		return named.SpecialType == SpecialType.None && !StoreTypeMap.IsScalar(named) ? Normalise(named) : null;
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
