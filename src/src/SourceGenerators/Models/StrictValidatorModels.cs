using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

readonly record struct StrictValidatorGenerationModel(
	GeneratorContext Context,
	EquatableArray<GeneratorResult<LikeC4RegistryTarget>> Targets
)
{
	public StrictModeSettings StrictMode { get; init; }

	public DSLDefinitions DSLDefinition { get; init; }

	public EquatableArray<CallSiteInfo> TagCallSites { get; init; }

	public EquatableArray<CallSiteInfo> KindCallSites { get; init; }

	public EquatableArray<CallSiteInfo> GroupCallSites { get; init; }

	public EquatableArray<CallSiteInfo> MetadataCallSites { get; init; }
}

readonly record struct LikeC4RegistryTarget(
	string DisplayName,
	SourceLocation Location,
	SeverityDefinition DefaultSeverity,
	EquatableArray<RegistrySpecifications> Specifications,
	EquatableArray<DuplicateRegistryType> DuplicateRegistryTypes
);

/// <summary>
/// A registry type with its declared specifications, grouped so the aggregate model remains
/// value-equatable across incremental runs.
/// </summary>
readonly record struct RegistrySpecifications(
	RegistryTypeDefinition Type,
	EquatableArray<RegistrySpecDefinition> Definitions
);

readonly record struct DuplicateRegistryType(string TypeName, SourceLocation Location);

readonly record struct StrictModeSettings(DiagnosticSeverity? Severity, bool IncludesMetadata)
{
	public bool IsEnabled => Severity is not null;
}

readonly record struct RegistrySpecDefinition(string SpecName, SeverityDefinition Severity)
{
	public override int GetHashCode() => SpecName.GetHashCode();
}
