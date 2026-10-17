namespace Aspire.Hosting.AspireC4.TestAppHost.ComplexData.ValueObjects;

/// <summary>
/// Stands in for <c>Purview.ValueObjects</c>' <c>[ValueObject]</c>.
/// </summary>
/// <remarks>
/// The readers match the attribute and its <c>EFMapping</c> member by name, so a same-shaped stub
/// exercises the same path with no dependency on the value-object package.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ValueObjectAttribute : Attribute
{
	public ValueObjectMapping EFMapping { get; set; }
}

/// <summary>How a value object maps to Entity Framework Core.</summary>
public enum ValueObjectMapping
{
	Scalar,

	ComplexType,
}
