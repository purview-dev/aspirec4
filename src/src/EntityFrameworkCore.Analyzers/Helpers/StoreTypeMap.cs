using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;

/// <summary>
/// Maps CLR types to the store type shown in the ER diagram.
/// </summary>
/// <remarks>
/// These names are deliberately generic rather than provider-specific: the generator cannot know which
/// provider the AppHost will configure, and the diagram is documentation rather than DDL. An explicit
/// <c>[Column(TypeName = "...")]</c> or <c>HasColumnType("...")</c> always wins over this map.
/// </remarks>
static class StoreTypeMap
{
	/// <summary>The store type used when the CLR type is not recognised.</summary>
	public const string Unknown = "unknown";

	/// <summary>Resolves the display store type for a property's CLR type.</summary>
	/// <param name="type">The property type, which may be <c>Nullable&lt;T&gt;</c>.</param>
	public static string Resolve(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		if (unwrapped.TypeKind == TypeKind.Enum)
		{
			return "int";
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
				_ => Unknown,
			};
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
	public static bool IsValueObject(ITypeSymbol type) =>
		type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } named
		&& named
			.GetMembers("Value")
			.OfType<IPropertySymbol>()
			.Any(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public);

	/// <summary>The scalar value-object type a property is backed by, or <see langword="null"/>.</summary>
	public static ITypeSymbol? ValueObjectType(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		return IsValueObject(unwrapped) ? unwrapped : null;
	}

	/// <summary>The real database type for a property, resolving a scalar value object to its primitive.</summary>
	public static string ColumnStoreType(ITypeSymbol type)
	{
		var valueObject = ValueObjectType(type);

		return valueObject is not null && ValueObjectUnderlying(valueObject) is { } underlying
			? Resolve(underlying)
			: Resolve(type);
	}

	static ITypeSymbol? ValueObjectUnderlying(ITypeSymbol valueObject) =>
		valueObject
			.GetMembers("Value")
			.OfType<IPropertySymbol>()
			.FirstOrDefault(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public)
			?.Type;

	/// <summary>
	/// Whether a type is mapped as an EF Core complex type — a Purview complex value object.
	/// </summary>
	/// <remarks>Matched by attribute name, so no dependency on the value-object package is taken.</remarks>
	public static bool IsComplexValueObject(ITypeSymbol type)
	{
		foreach (var attribute in Unwrap(type).GetAttributes())
		{
			if (attribute.AttributeClass?.Name is not ("ValueObjectAttribute" or "ValueObject"))
			{
				continue;
			}

			if (
				IsNamedEnumMember(
					attribute.NamedArguments.FirstOrDefault(a => a.Key == "EFMapping").Value,
					"ComplexType"
				)
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Whether a typed constant is the named member of its enum type.
	/// </summary>
	/// <remarks>
	/// The compiler reports either the member or its underlying value depending on how the constant was
	/// written, so both are accepted.
	/// </remarks>
	static bool IsNamedEnumMember(TypedConstant constant, string member)
	{
		if (constant.Value is null)
		{
			return false;
		}

		if (string.Equals(constant.Value.ToString(), member, StringComparison.Ordinal))
			return true;

		// The constant is not the member name, so check whether it is the underlying value of the member.
		return constant.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
			&& enumType
				.GetMembers()
				.OfType<IFieldSymbol>()
				.Any(f => f.Name == member && f.HasConstantValue && Equals(f.ConstantValue, constant.Value));
	}

	/// <summary>Determines whether a property type maps to a column rather than a navigation.</summary>
	public static bool IsScalar(ITypeSymbol type)
	{
		var unwrapped = Unwrap(type);

		if (unwrapped.TypeKind == TypeKind.Enum || IsValueObject(unwrapped))
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
