namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

[Generate(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttributeFullName)]
readonly partial record struct LikeC4RegistryAttributeData(
	[Property(DefaultValue = "Inherit", IsEnum = true)] string Strict
);

[Generate(TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttributeFullName)]
readonly partial record struct KnownTypesAttributeData(
	[Argument(IsEnum = true, Name = "type", DefaultValue = "Tag")] string Type,
	[Property("Inherit", IsEnum = true)] string Strict
);

[Generate(TypeLibrary.Aspire.Hosting.AspireC4.SeverityAttributeFullName)]
readonly partial record struct SeverityAttributeData(
	[Argument(IsEnum = true, Name = "severity", DefaultValue = "Inherit")] string Severity
);

readonly record struct RegistryTypeDefinition(string Name, int Value, EquatableArray<string> ValidTypeNames)
{
	public string FullName => TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryType.MetadataFullName + "." + Name;

	public static readonly RegistryTypeDefinition Empty;
}

readonly record struct SeverityDefinition(string Name, int Value)
{
	public string FullName => TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity.MetadataFullName + "." + Name;

	public static readonly SeverityDefinition Empty;
}
