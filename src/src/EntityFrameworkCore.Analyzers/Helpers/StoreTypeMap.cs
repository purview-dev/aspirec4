using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;

/// <summary>How a complex value object reaches the database.</summary>
enum ComplexMapping
{
	/// <summary>Not a complex value object.</summary>
	None,

	/// <summary>Flattened into the owner's table, one column per member.</summary>
	Flattened,

	/// <summary>Serialised into a single JSON column on the owner's table.</summary>
	Json,
}

/// <summary>
/// Maps CLR types to the store type shown in the ER diagram, and recognises the domain types — value
/// objects, enums and unions — that a column can be declared with.
/// </summary>
/// <remarks>
/// These names are deliberately generic rather than provider-specific: the generator cannot know which
/// provider the AppHost will configure, and the diagram is documentation rather than DDL. An explicit
/// <c>[Column(TypeName = "...")]</c> or <c>HasColumnType("...")</c> always wins over this map.
/// <para>
/// Value objects are recognised by attribute first and by shape second. The attributes are matched by
/// name, so no dependency on the value-object package is taken. Attributes matter because a generator
/// that writes the <c>Value</c> member — Purview's <c>[Scalar&lt;T&gt;]</c>, or the forwarding
/// <c>Value</c> of <c>[Scalar("Amount")]</c> — writes it in a generated file this analyzer cannot see:
/// one generator never sees another's output. Shape alone would miss every such type.
/// </para>
/// </remarks>
static class StoreTypeMap
{
	/// <summary>The store type used when the CLR type is not recognised.</summary>
	public const string Unknown = "unknown";

	/// <summary>The store type of a column holding a JSON document.</summary>
	public const string Json = "json";

	const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

	/// <summary>Resolves the display store type for a property's CLR type.</summary>
	/// <param name="type">The property type, which may be <c>Nullable&lt;T&gt;</c>.</param>
	public static string Resolve(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		if (unwrapped is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
		{
			return enumType.EnumUnderlyingType is { } underlying ? Resolve(underlying) : "int";
		}

		if (unwrapped is IArrayTypeSymbol array)
		{
			return array.ElementType.SpecialType == SpecialType.System_Byte ? "blob" : Unknown;
		}

#pragma warning disable IDE0072 // Add missing cases
		var special = unwrapped.SpecialType switch
		{
			SpecialType.System_Boolean => "bool",
			SpecialType.System_Byte or SpecialType.System_SByte => "tinyint",
			SpecialType.System_Int16 or SpecialType.System_UInt16 => "smallint",
			SpecialType.System_Int32 or SpecialType.System_UInt32 => "int",
			SpecialType.System_Int64 or SpecialType.System_UInt64 => "bigint",
			SpecialType.System_Single => "real",
			SpecialType.System_Double => "double",
			SpecialType.System_Decimal => "decimal",
			SpecialType.System_String => "varchar",
			SpecialType.System_Char => "char",
			SpecialType.System_DateTime => "datetime",
			_ => null,
		};
#pragma warning restore IDE0072 // Add missing cases

		return special
			?? unwrapped.ToDisplayString() switch
			{
				"System.Guid" => "guid",
				"System.DateTimeOffset" => "datetimeoffset",
				"System.TimeSpan" => "time",
				"System.DateOnly" => "date",
				"System.TimeOnly" => "time",
				// A JSON document is a column, not a table. Without these it resolves to Unknown, and an
				// unknown class is taken for a navigation — which is how a property such as
				// `JsonDocument Payload` used to invent an entity named after the framework type.
				"System.Text.Json.JsonDocument"
				or "System.Text.Json.JsonElement"
				or "System.Text.Json.Nodes.JsonNode"
				or "System.Text.Json.Nodes.JsonObject"
				or "System.Text.Json.Nodes.JsonArray"
				or "System.Text.Json.Nodes.JsonValue" => Json,
				_ => Unknown,
			};
	}

	/// <summary>
	/// Determines whether a type is a scalar value object: a type that wraps a single value, as a
	/// strongly-typed identifier does.
	/// </summary>
	/// <remarks>
	/// <list type="number">
	/// <item>A Purview <c>[Scalar]</c>, <c>[Scalar&lt;T&gt;]</c> or <c>[Scalar(typeof(T))]</c> attribute —
	/// on a struct or a class.</item>
	/// <item><c>IScalarValueObject&lt;TSelf, TValue&gt;</c>, declared by hand.</item>
	/// <item>A non-generic struct with a public instance <c>Value</c> member, for value objects from any
	/// other library or written by hand. A <c>Value</c> typed <c>object</c> is excluded: that is the
	/// shape of a union's case accessor, not of a wrapped value.</item>
	/// </list>
	/// A union is never a scalar value object, whatever its shape.
	/// </remarks>
	public static bool IsValueObject(ITypeSymbol type) => ScalarValueObject(type).IsValueObject;

	/// <summary>The scalar value-object type a property is backed by, or <see langword="null"/>.</summary>
	public static ITypeSymbol? ValueObjectType(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		return IsValueObject(unwrapped) ? unwrapped : null;
	}

	/// <summary>The real database type for a property, resolving a scalar value object to its primitive.</summary>
	public static string ColumnStoreType(ITypeSymbol type)
	{
		var (isValueObject, underlying) = ScalarValueObject(Unwrap(type));

		if (isValueObject)
		{
			return underlying is null ? Unknown : Resolve(underlying);
		}

		return IsUnion(type) ? Unknown : Resolve(type);
	}

	/// <summary>
	/// Whether a scalar value object wraps an enum, so a renderer can mark it as one.
	/// </summary>
	public static bool WrapsEnum(ITypeSymbol type) =>
		ScalarValueObject(Unwrap(type)).Underlying is { } underlying && Unwrap(underlying).TypeKind == TypeKind.Enum;

	static (bool IsValueObject, ITypeSymbol? Underlying) ScalarValueObject(ITypeSymbol type)
	{
		if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct or TypeKind.Class } named || IsUnion(named))
		{
			return (false, null);
		}

		foreach (var attribute in named.GetAttributes())
		{
			if (attribute.AttributeClass is not { Name: "ScalarAttribute", Arity: <= 1 } attributeClass)
			{
				continue;
			}

			if (attributeClass.IsGenericType && attributeClass.TypeArguments.Length == 1)
			{
				return (true, attributeClass.TypeArguments[0]);
			}

			string? propertyName = null;

			foreach (var argument in attribute.ConstructorArguments)
			{
				if (argument.Kind == TypedConstantKind.Type && argument.Value is ITypeSymbol valueType)
				{
					return (true, valueType);
				}

				propertyName ??= argument.Value as string;
			}

			// The manual form: the author declares the value under the property the attribute names, and
			// the generator adds a forwarding Value this analyzer cannot see.
			return (
				true,
				(PublicInstanceProperty(named, propertyName ?? "Value") ?? PublicInstanceProperty(named, "Value"))?.Type
			);
		}

		foreach (var contract in named.AllInterfaces)
		{
			if (contract is { Name: "IScalarValueObject", TypeArguments.Length: 2 })
			{
				return (true, contract.TypeArguments[1]);
			}
		}

		if (named.TypeKind == TypeKind.Struct && !named.IsGenericType)
		{
			var value = PublicInstanceProperty(named, "Value");

			if (value is not null && value.Type.SpecialType != SpecialType.System_Object)
			{
				return (true, value.Type);
			}
		}

		return (false, null);
	}

	static IPropertySymbol? PublicInstanceProperty(INamedTypeSymbol type, string name) =>
		type.GetMembers(name)
			.OfType<IPropertySymbol>()
			.FirstOrDefault(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public);

	/// <summary>
	/// Whether a type is a C# union: declared with <c>union</c>, or a hand-written struct marked
	/// <c>[Union]</c> or implementing <c>IUnion</c>.
	/// </summary>
	/// <remarks>
	/// Checked before anything else, because a union's generated <c>Value</c> accessor gives it exactly
	/// the shape of a scalar value object. EF Core cannot store one without a value converter, and the
	/// converter is not visible to this analyzer, so the column's database type is <see cref="Unknown"/>.
	/// </remarks>
	public static bool IsUnion(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		if (IsUnionDeclaration(unwrapped))
		{
			return true;
		}

		foreach (var attribute in unwrapped.GetAttributes())
		{
			if (
				attribute.AttributeClass is { Name: "UnionAttribute" } attributeClass
				&& attributeClass.ContainingNamespace.ToDisplayString() == CompilerServicesNamespace
			)
			{
				return true;
			}
		}

		return unwrapped.AllInterfaces.Any(i =>
			i.Name == "IUnion" && i.ContainingNamespace.ToDisplayString() == CompilerServicesNamespace
		);
	}

	/// <summary>Roslyn's own answer, for a type declared with the <c>union</c> keyword.</summary>
	/// <remarks>
	/// <c>ITypeSymbol.IsUnion</c> is marked experimental, so it may be renamed or removed in a later
	/// compiler — and this analyzer runs inside whatever compiler the consumer has. The call is kept in a
	/// method of its own so a missing member fails when this method is compiled, where it is caught, rather
	/// than taking the whole analyzer down; the attribute and interface checks still recognise a union.
	/// </remarks>
	static bool IsUnionDeclaration(ITypeSymbol type)
	{
		try
		{
			return ReadIsUnion(type);
		}
		catch (MissingMemberException)
		{
			return false;
		}
	}

#pragma warning disable RSEXPERIMENTAL006 // Evaluation-only API; isolated and guarded above.
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	static bool ReadIsUnion(ITypeSymbol type) => type.IsUnion;
#pragma warning restore RSEXPERIMENTAL006

	/// <summary>
	/// How a type is mapped as a complex value object, or <see cref="ComplexMapping.None"/> when it is not one.
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	/// <item>Purview's <c>[ValueObject]</c>: its <c>EFMapping</c>, else the assembly's
	/// <c>[ValueObjectDefaults(EFMapping = ...)]</c>, else <c>ComplexType</c> — the library's own default,
	/// so a bare <c>[ValueObject]</c> is flattened. <c>Json</c> is one JSON column; <c>None</c> leaves the
	/// mapping to the application, which this analyzer cannot see.</item>
	/// <item>EF Core's <c>[ComplexType]</c>, which is always flattened.</item>
	/// <item>EF Core's <c>[Owned]</c>: a single owned reference shares its owner's table, with columns
	/// named <c>{Navigation}_{Property}</c> — the same shape as a flattened complex type.</item>
	/// </list>
	/// A scalar value object is never a complex one.
	/// </remarks>
	public static ComplexMapping ComplexValueObjectMapping(ITypeSymbol type)
	{
		if (Unwrap(type) is not INamedTypeSymbol named || named.SpecialType != SpecialType.None || IsValueObject(named))
		{
			return ComplexMapping.None;
		}

		foreach (var attribute in named.GetAttributes())
		{
			switch (attribute.AttributeClass?.Name)
			{
				case "ValueObjectAttribute" or "ValueObject":
					return MappingOf(
						EnumMemberName(attribute, "EFMapping") ?? AssemblyDefaultMapping(named) ?? "ComplexType"
					);

				case "ComplexTypeAttribute"
				or "ComplexType"
				or "OwnedAttribute"
				or "Owned":
					return ComplexMapping.Flattened;

				default:
					break;
			}
		}

		return ComplexMapping.None;
	}

	static ComplexMapping MappingOf(string member) =>
		member switch
		{
			"ComplexType" => ComplexMapping.Flattened,
			"Json" => ComplexMapping.Json,
			_ => ComplexMapping.None,
		};

	static string? AssemblyDefaultMapping(INamedTypeSymbol type)
	{
		foreach (var attribute in type.ContainingAssembly?.GetAttributes() ?? [])
		{
			if (attribute.AttributeClass?.Name is "ValueObjectDefaultsAttribute" or "ValueObjectDefaults")
			{
				return EnumMemberName(attribute, "EFMapping");
			}
		}

		return null;
	}

	/// <summary>
	/// The member name of an enum-typed named argument, or <see langword="null"/> when it was not set.
	/// </summary>
	/// <remarks>
	/// The compiler reports an enum constant as its underlying value, so the value is mapped back to the
	/// member that declares it; a constant that is already the member name is accepted as is.
	/// </remarks>
	static string? EnumMemberName(AttributeData attribute, string argumentName)
	{
		foreach (var argument in attribute.NamedArguments)
		{
			if (argument.Key != argumentName || argument.Value.Value is not { } value)
			{
				continue;
			}

			if (argument.Value.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
			{
				var member = enumType
					.GetMembers()
					.OfType<IFieldSymbol>()
					.FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));

				if (member is not null)
				{
					return member.Name;
				}
			}

			return value.ToString();
		}

		return null;
	}

	/// <summary>Determines whether a property type maps to a column rather than a navigation.</summary>
	public static bool IsScalar(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		if (unwrapped.TypeKind == TypeKind.Enum || IsValueObject(unwrapped) || IsUnion(unwrapped))
		{
			return true;
		}

		// Arrays of bytes are mapped to a blob column, but other arrays are not supported.
		return unwrapped is IArrayTypeSymbol array
			? array.ElementType.SpecialType == SpecialType.System_Byte
			: Resolve(unwrapped) != Unknown;
	}

	/// <summary>Unwraps <c>Nullable&lt;T&gt;</c> to <c>T</c>, leaving other types untouched.</summary>
	public static ITypeSymbol Unwrap(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
			? named.TypeArguments[0]
			: type;

	/// <summary>Determines whether a property type is an optional (nullable) one.</summary>
	public static bool IsNullable(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
		|| (type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.NotAnnotated);
}
