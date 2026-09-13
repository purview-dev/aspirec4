using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;
using Aspire.Hosting.AspireC4.SourceGenerators.Models;
using Aspire.Hosting.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>
/// Validates LikeC4 call-site string arguments against pre-declared definitions.
/// </summary>
/// <remarks>
/// Two validation modes:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>DSL file mode</b>: activated when <c>&lt;AspireC4Strict&gt;...&lt;/AspireC4Strict&gt;</c> is set
///       in the consuming project to a non-off severity. <c>.c4</c>/<c>.likec4</c> additional files are parsed for
///       <c>specification</c> block declarations (<c>tag</c>, <c>element</c>, <c>relationship</c>).
///       All <c>.WithTag()</c> and <c>.WithKind()</c> call-site values are validated against those.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Class-based mode</b>: a class annotated with <c>[LikeC4Registry]</c> (any accessibility,
///       any nesting level) provides <c>public const string</c> fields inside nested static classes
///       named <c>Tags</c>, <c>ElementKinds</c>, <c>RelationshipKinds</c>, <c>Groups</c>, and/or
///       <c>MetadataKeys</c>, or directly on the class via <c>[KnownType(LikeC4RegistryType.X)]</c>.
///       Only one such class is allowed per assembly.
///     </description>
///   </item>
/// </list>
/// Both modes may be active simultaneously; allowed sets are merged.
/// </remarks>
[Generator]
public sealed partial class LikeC4StrictValidatorGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterEmbeddedAttribute<LikeC4StrictValidatorGenerator>();

		context.RegisterPostInitializationOutput(ctx =>
		{
			foreach (var (HintName, Source) in MarkerAttributeEmitter.EmitMarkAttribute())
				ctx.AddSource(HintName, Source);
		});

		// Combine everything and validate.
		var pipeline = SourceGenHelper.CreateGenerationPipeline(context);
		context.RegisterSourceOutput(
			pipeline,
			static (ctx, model) =>
			{
				if (model.Context.Settings.IsSourceGeneratorDisabled)
				{
					model.Context.Debug(
						$"{nameof(LikeC4StrictValidatorGenerator)} is disabled via the MSBuild '{PropertyLibrary.DisableSourceGenerator}' property."
					);

					return;
				}

				Validate(ctx, model);
			}
		);
	}

	static DiagnosticDescriptor WithSeverity(DiagnosticDescriptor descriptor, DiagnosticSeverity severity) =>
		severity == descriptor.DefaultSeverity
			? descriptor
			: new DiagnosticDescriptor(
				descriptor.Id,
				descriptor.Title,
				descriptor.MessageFormat,
				descriptor.Category,
				severity,
				descriptor.IsEnabledByDefault,
				descriptor.Description,
				descriptor.HelpLinkUri
			);

	[SuppressMessage(
		"Maintainability",
		"CA1502:Avoid excessive complexity",
		Justification = "Validation intentionally combines the supported registry and DSL severity scopes."
	)]
	static void Validate(SourceProductionContext ctx, StrictValidatorGenerationModel model)
	{
		foreach (var result in model.Targets)
		{
			foreach (var diagnostic in result.Diagnostics)
				ctx.ReportDiagnostic(diagnostic.ToDiagnostic());
		}

		var targets = model
			.Targets.Where(static result => result.HasValue)
			.Select(static result => result.Value)
			.ToArray();
		if (targets.Length > 1)
		{
			for (var index = 1; index < targets.Length; index++)
			{
				ctx.ReportDiagnostic(
					Diagnostic.Create(
						DiagnosticLibrary.MultipleRegistryClassesDefined,
						targets[index].Location.ToLocation(),
						targets[index].DisplayName
					)
				);
			}
		}

		foreach (var target in targets)
		{
			foreach (var duplicate in target.DuplicateRegistryTypes)
			{
				ctx.ReportDiagnostic(
					Diagnostic.Create(
						DiagnosticLibrary.DuplicateTypeDeclaration,
						duplicate.Location.ToLocation(),
						duplicate.TypeName
					)
				);
			}
		}

		var hasDslValidation = model.StrictMode.IsEnabled && model.DSLDefinition.HasAny;
		var hasRegistryValidation = targets.Length > 0;
		if (!hasDslValidation && !hasRegistryValidation)
			return;

		var primaryTarget = hasRegistryValidation ? targets[0] : default;
		var registrySeverityDefinition = hasRegistryValidation
			? primaryTarget.DefaultSeverity
			: EnumLibrary.SeverityValues.Inherit;
		var registryExplicit = registrySeverityDefinition != EnumLibrary.SeverityValues.Inherit;
		var isExplicitlyEnabled = registryExplicit || model.StrictMode.IsEnabled;

		var registrySeverity = ResolveSeverity(
			registrySeverityDefinition,
			model.StrictMode.Severity ?? (hasRegistryValidation ? DiagnosticSeverity.Info : null)
		);

		IEnumerable<RegistrySpecDefinition> GetSpecifications(RegistryTypeDefinition type) =>
			hasRegistryValidation
			&& primaryTarget.Specifications.FirstOrDefault(specifications => specifications.Type == type) is { } group
				? group.Definitions
				: [];

		var tagSpecifications = GetSpecifications(EnumLibrary.RegistryTypeValues.Tag).ToArray();
		var elementSpecifications = GetSpecifications(EnumLibrary.RegistryTypeValues.ElementKind).ToArray();
		var relationshipSpecifications = GetSpecifications(EnumLibrary.RegistryTypeValues.RelationshipKind).ToArray();
		var groupSpecifications = GetSpecifications(EnumLibrary.RegistryTypeValues.Group).ToArray();
		var metadataSpecifications = GetSpecifications(EnumLibrary.RegistryTypeValues.MetadataKey).ToArray();

		var allowedTags = BuildAllowedSet(
			hasDslValidation ? model.DSLDefinition.Tags : [],
			tagSpecifications.Select(static definition => definition.SpecName)
		);
		var allowedKinds = BuildAllowedSet(
			hasDslValidation ? model.DSLDefinition.ElementKinds.Concat(model.DSLDefinition.RelationshipKinds) : [],
			elementSpecifications
				.Select(static definition => definition.SpecName)
				.Concat(relationshipSpecifications.Select(static definition => definition.SpecName))
		);
		var allowedGroups = BuildAllowedSet([], groupSpecifications.Select(static definition => definition.SpecName));
		var allowedMetadata = BuildAllowedSet(
			[],
			metadataSpecifications
				.Select(static definition => definition.SpecName)
				.Select(NormaliseMetadataKeyForComparison)
		);

		var tagSeverity = ResolveTypeSeverity(tagSpecifications, registrySeverity);
		var elementSeverity = GetTypeSeverityDefinition(elementSpecifications);
		var relationshipSeverity = GetTypeSeverityDefinition(relationshipSpecifications);
		var kindSeverity = ResolveSeverity(CombineSeverity(elementSeverity, relationshipSeverity), registrySeverity);
		var groupSeverity = ResolveTypeSeverity(groupSpecifications, registrySeverity);
		var metadataDefinition = GetTypeSeverityDefinition(metadataSpecifications);
		var metadataSeverity =
			metadataDefinition != EnumLibrary.SeverityValues.Inherit
				? ResolveSeverity(metadataDefinition, registrySeverity)
				: (model.StrictMode.IncludesMetadata ? registrySeverity : null);

		ReportUndeclared(
			ctx,
			allowedTags,
			tagSeverity,
			isExplicitlyEnabled,
			DiagnosticLibrary.UndeclaredTag,
			model.TagCallSites,
			static value => value
		);
		ReportUndeclared(
			ctx,
			allowedKinds,
			kindSeverity,
			isExplicitlyEnabled,
			DiagnosticLibrary.UndeclaredKind,
			model.KindCallSites,
			static value => value
		);
		ReportUndeclared(
			ctx,
			allowedGroups,
			groupSeverity,
			isExplicitlyEnabled,
			DiagnosticLibrary.UndeclaredGroup,
			model.GroupCallSites,
			static value => value
		);
		ReportUndeclared(
			ctx,
			allowedMetadata,
			metadataSeverity,
			isExplicitlyEnabled,
			DiagnosticLibrary.UndeclaredMetadataKey,
			model.MetadataCallSites,
			NormaliseMetadataKeyForComparison
		);
	}

	static SeverityDefinition GetTypeSeverityDefinition(IEnumerable<RegistrySpecDefinition> specifications)
	{
		var severity = EnumLibrary.SeverityValues.Inherit;
		foreach (var specification in specifications)
		{
			if (specification.Severity.Value > severity.Value)
				severity = specification.Severity;
		}
		return severity;
	}

	static SeverityDefinition CombineSeverity(SeverityDefinition first, SeverityDefinition second) =>
		first == EnumLibrary.SeverityValues.Off || second == EnumLibrary.SeverityValues.Off
			? EnumLibrary.SeverityValues.Off
			: (first.Value >= second.Value ? first : second);

	static DiagnosticSeverity? ResolveTypeSeverity(
		IEnumerable<RegistrySpecDefinition> specifications,
		DiagnosticSeverity? inherited
	) => ResolveSeverity(GetTypeSeverityDefinition(specifications), inherited);

	static DiagnosticSeverity? ResolveSeverity(SeverityDefinition severity, DiagnosticSeverity? inherited)
	{
		if (severity == EnumLibrary.SeverityValues.Off)
			return null;
		if (severity == EnumLibrary.SeverityValues.Suggestion)
			return DiagnosticSeverity.Info;
		if (severity == EnumLibrary.SeverityValues.Warning)
			return DiagnosticSeverity.Warning;
		if (severity == EnumLibrary.SeverityValues.Error)
			return DiagnosticSeverity.Error;

		// Inherit
		return inherited;
	}

	static void ReportUndeclared(
		SourceProductionContext ctx,
		HashSet<string> allowed,
		DiagnosticSeverity? severity,
		bool isExplicitlyEnabled,
		DiagnosticDescriptor descriptor,
		IEnumerable<CallSiteInfo> callSites,
		Func<string, string> normalize
	)
	{
		if (severity is not { } resolvedSeverity || (allowed.Count == 0 && !isExplicitlyEnabled))
			return;

		var resolvedDescriptor = WithSeverity(descriptor, resolvedSeverity);
		foreach (var callSite in callSites)
		{
			if (!allowed.Contains(normalize(callSite.Value)))
			{
				ctx.ReportDiagnostic(
					Diagnostic.Create(resolvedDescriptor, callSite.Location.ToLocation(), callSite.Value)
				);
			}
		}
	}

	static HashSet<string> BuildAllowedSet(IEnumerable<string> primary, IEnumerable<string> secondary)
	{
		HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);

		foreach (var v in primary)
			set.Add(v);

		foreach (var v in secondary)
			set.Add(v);

		return set;
	}

	/// <summary>
	/// Normalises a metadata key for registry comparison by replacing every character that is
	/// not a letter, digit, hyphen, or underscore with <c>_</c>.  The resulting string is then
	/// compared case-insensitively, so <c>"Azure SKU"</c>, <c>"azure sku"</c>, <c>"AZURE_sku"</c>,
	/// and <c>"Azure_SKU"</c> all resolve to the same key.
	/// Mirrors the runtime logic in <c>ModelBuilder.NormaliseMetadataKey</c>.
	/// </summary>
	internal static string NormaliseMetadataKeyForComparison(string key)
	{
		if (string.IsNullOrEmpty(key))
			return key;

		var chars = key.ToCharArray();
		for (var i = 0; i < chars.Length; i++)
		{
			var c = chars[i];
			if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
				chars[i] = '_';
		}

		return new string(chars);
	}
}
