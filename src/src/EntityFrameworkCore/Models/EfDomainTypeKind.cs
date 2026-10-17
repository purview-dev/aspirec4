using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

/// <summary>
/// What kind of domain type a column's declared type is.
/// </summary>
/// <remarks>
/// The three are distinct: a value type is a struct, a value object is a domain wrapper, and an enum is
/// an enum. Recording which one a column carries lets a renderer treat them differently — the enum
/// marker, for instance.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EfDomainTypeKind
{
	/// <summary>A scalar value object: a struct wrapping a single value, such as a strongly-typed id.</summary>
	ValueObject,

	/// <summary>A complex value object, flattened into its owner's table.</summary>
	ComplexValueObject,

	/// <summary>An enum, stored as its underlying primitive.</summary>
	Enum,
}
