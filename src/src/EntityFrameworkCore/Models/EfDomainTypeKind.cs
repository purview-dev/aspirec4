using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>
/// What kind of domain type a column's declared type is.
/// </summary>
/// <remarks>
/// They are distinct: a value object is a domain wrapper, an enum is a fixed set of values, and a union
/// is one of a fixed set of types. Recording which one a column carries lets a renderer treat them
/// differently — the <c>enum</c> and <c>union</c> markers, for instance.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EfDomainTypeKind
{
	/// <summary>A scalar value object: a type wrapping a single value, such as a strongly-typed id.</summary>
	ValueObject,

	/// <summary>
	/// A complex value object: flattened into its owner's table, or held in one JSON column when it is
	/// mapped to JSON.
	/// </summary>
	ComplexValueObject,

	/// <summary>An enum, stored as its underlying primitive.</summary>
	Enum,

	/// <summary>
	/// A C# union, holding one of a fixed set of case types. EF Core needs a value converter to store
	/// one, so its database type is whatever that converter produces.
	/// </summary>
	Union,
}

/// <summary>
/// Reads and writes <see cref="EfDomainTypeKind"/> by name, reading a name it does not know as
/// <see langword="null"/>.
/// </summary>
/// <remarks>
/// Applied to <see cref="EfColumn.DomainTypeKind"/>. The model is written by one component and read by another, and they need not be the same version: a
/// service built with a newer analyzer can carry a kind this AppHost has never heard of. A strict
/// converter throws, and a parse failure discards the whole model, so one unfamiliar column would empty
/// the diagram. Read leniently, that column simply shows no domain type.
/// </remarks>
sealed class EfDomainTypeKindConverter : JsonConverter<EfDomainTypeKind?>
{
	public override bool HandleNull => true;

	public override EfDomainTypeKind? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}

		if (reader.TokenType == JsonTokenType.Number)
		{
			return reader.TryGetInt32(out var number) && Enum.IsDefined(typeof(EfDomainTypeKind), number)
				? (EfDomainTypeKind)number
				: null;
		}

		return
			Enum.TryParse<EfDomainTypeKind>(reader.GetString(), ignoreCase: true, out var kind) && Enum.IsDefined(kind)
			? kind
			: null;
	}

	public override void Write(Utf8JsonWriter writer, EfDomainTypeKind? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);

		if (value is { } kind)
		{
			writer.WriteStringValue(kind.ToString());
		}
		else
		{
			writer.WriteNullValue();
		}
	}
}
