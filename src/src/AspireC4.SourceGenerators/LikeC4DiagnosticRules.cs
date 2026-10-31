using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>
/// Provides the diagnostic descriptors shared between the source generator and the analyzer.
/// Each rule is tagged with the component that owns it, based on the data sources it needs:
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>
///     <term><b>Generator</b> (<see cref="LikeC4StrictValidatorGenerator"/>)</term>
///     <description>
///       Rules tagged <see cref="DiagnosticRuleSource.RequiresBothSources"/> need the merged view of both
///       the DSL-file (AdditionalText <c>.c4</c>) definitions <em>and</em> the <c>[LikeC4Registry]</c>
///       class definitions. Only the generator can access AdditionalText files, so it owns these.
///     </description>
///   </item>
///   <item>
///     <term><b>Analyzer</b> (<see cref="LikeC4RegistryAnalyzer"/>)</term>
///     <description>
///       Rules tagged <see cref="DiagnosticRuleSource.ClassOnly"/> depend solely on <c>[LikeC4Registry]</c>
///       class definitions, which analyzers can read from the compilation. The analyzer owns these.
///     </description>
///   </item>
/// </list>
/// </remarks>
static class LikeC4DiagnosticRules
{
	/// <summary>Indicates which component emits a diagnostic rule.</summary>
	enum DiagnosticRuleSource
	{
		/// <summary>The rule needs both DSL-file and class definitions; emitted by the generator.</summary>
		RequiresBothSources,
		/// <summary>The rule needs only class definitions; emitted by the analyzer.</summary>
		ClassOnly,
	}

	/// <summary>SOURCE: Generator — RequiresBothSources. Emitted when a <c>.WithTag()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredTag = new(
		id: "ASPIREC4001",
		title: "Undeclared LikeC4 tag",
		messageFormat: "Tag '{0}' is not declared. Add it to a 'specification {{ tag {0} }}' block in a .c4 additional file, "
			+ "or as 'public const string' in the 'Tags' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All tags passed to WithTag() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict has a non-off severity), or as public const string fields in the Tags nested class "
			+ "of a [LikeC4Registry]-annotated class."
	);

	/// <summary>
	/// SOURCE: Generator — RequiresBothSources. Emitted when a <c>.WithKind()</c> argument is not declared in any
	/// active element-kind or relationship-kind definition source.
	/// </summary>
	public static readonly DiagnosticDescriptor UndeclaredKind = new(
		id: "ASPIREC4002",
		title: "Undeclared LikeC4 element or relationship kind",
		messageFormat: "Kind '{0}' is not declared. Add it to a 'specification {{ element {0} }}' or "
			+ "'specification {{ relationship {0} }}' block in a .c4 additional file, "
			+ "or as 'public const string' in 'ElementKinds' or 'RelationshipKinds' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All kinds passed to WithKind() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict has a non-off severity), or as public const string fields in the ElementKinds or "
			+ "RelationshipKinds nested class of a [LikeC4Registry]-annotated class."
	);

	/// <summary>SOURCE: Analyzer — ClassOnly. Emitted when more than one class per assembly carries <c>[LikeC4Registry]</c>.</summary>
	public static readonly DiagnosticDescriptor MultipleDefinitionsClasses = new(
		id: "ASPIREC4003",
		title: "Multiple [LikeC4Registry] classes",
		messageFormat: "Only one class per assembly may carry [LikeC4Registry]. Duplicate found: '{0}'.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Only one class per assembly may be annotated with [LikeC4Registry]. "
			+ "Consolidate all tag, element-kind, relationship-kind, group, and metadata-key definitions into a single class.",
		customTags: [WellKnownDiagnosticTags.CompilationEnd]
	);

	/// <summary>SOURCE: Analyzer — ClassOnly. Emitted when a <c>.WithLikeC4Group()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredGroup = new(
		id: "ASPIREC4004",
		title: "Undeclared LikeC4 group",
		messageFormat: "Group '{0}' is not declared. Add it as 'public const string' in the 'Groups' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All group names passed to WithLikeC4Group() must be declared as public const string fields "
			+ "in the Groups nested class of a [LikeC4Registry]-annotated class."
	);

	/// <summary>
	/// SOURCE: Analyzer — ClassOnly. Emitted when a registry type is declared both via a named nested class
	/// <em>and</em> via individual <c>[KnownType]</c> attributes on constants.
	/// </summary>
	public static readonly DiagnosticDescriptor DuplicateTypeDeclaration = new(
		id: "ASPIREC4005",
		title: "Duplicate LikeC4 registry type declaration",
		messageFormat: "Registry type '{0}' is declared both as a nested class and via [KnownType] attributes. Use only one declaration approach per type.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A registry type (e.g. Tag, Group) must be declared either as a nested static class "
			+ "(e.g. 'public static class Tags { ... }') OR via [KnownType] attributes on individual constants, not both.",
		customTags: [WellKnownDiagnosticTags.CompilationEnd]
	);

	/// <summary>SOURCE: Analyzer — ClassOnly. Emitted when a <c>.WithMetadata()</c> key argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredMetadataKey = new(
		id: "ASPIREC4006",
		title: "Undeclared LikeC4 metadata key",
		messageFormat: "Metadata key '{0}' is not declared. Add it as 'public const string' in the 'MetadataKeys' nested class of your [LikeC4Registry] class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All metadata keys passed to WithMetadata() must be declared as public const string fields "
			+ "in the MetadataKeys nested class of a [LikeC4Registry]-annotated class. "
			+ "Keys are compared after normalising whitespace and punctuation to underscores, and are case-insensitive."
	);

	// --- Shared severity helpers ---

	/// <summary>Severity constants: 0 = Inherit, 1 = Off, 2 = Suggestion, 3 = Warning, 4 = Error.</summary>
	public const int SeverityInherit = 0;
	public const int SeverityOff = 1;
	public const int SeveritySuggestion = 2;
	public const int SeverityWarning = 3;
	public const int SeverityError = 4;

	/// <summary>
	/// Parses the <c>AspireC4Strict</c> MSBuild value into a global severity and a flag indicating whether
	/// metadata-key validation is included. Mirrors the historical <c>ParseGlobalStrict</c> behaviour.
	/// </summary>
	public static (DiagnosticSeverity? Severity, bool IncludesMetadata) ParseGlobalStrict(string? val)
	{
		if (string.IsNullOrWhiteSpace(val))
			return (null, false);

		var normalized = val!.Trim();
		if (normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
			return (null, false);
		if (normalized.Equals("suggestion", StringComparison.OrdinalIgnoreCase))
			return (DiagnosticSeverity.Info, false);
		if (normalized.Equals("warning", StringComparison.OrdinalIgnoreCase))
			return (DiagnosticSeverity.Warning, false);
		if (
			normalized.Equals("error", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("all", StringComparison.OrdinalIgnoreCase)
		)
			return (DiagnosticSeverity.Error, false);
		if (normalized.Equals("allincludingmetadata", StringComparison.OrdinalIgnoreCase))
			return (DiagnosticSeverity.Error, true);

		return (null, false);
	}

	/// <summary>Returns the descriptor, or a copy with the specified default severity when it differs.</summary>
	public static DiagnosticDescriptor WithSeverity(DiagnosticDescriptor descriptor, DiagnosticSeverity severity) =>
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

	/// <summary>Resolves the registry-wide severity from the <c>[LikeC4Registry(Strict=...)]</c> raw mode.</summary>
	public static DiagnosticSeverity? ResolveRegistrySeverity(
		int registryRaw,
		DiagnosticSeverity? globalStrict,
		bool hasClassValidation
	) =>
		registryRaw switch
		{
			SeverityOff => null,
			SeverityInherit => globalStrict ?? (hasClassValidation ? DiagnosticSeverity.Info : null),
			SeveritySuggestion => DiagnosticSeverity.Info,
			SeverityWarning => DiagnosticSeverity.Warning,
			SeverityError => DiagnosticSeverity.Error,
			_ => null,
		};

	/// <summary>Resolves a per-type severity from the raw mode, inheriting the registry severity when Inherit.</summary>
	public static DiagnosticSeverity? ResolveTypeSeverity(int typeRaw, DiagnosticSeverity? registrySeverity) =>
		typeRaw switch
		{
			SeverityOff => null,
			SeverityInherit => registrySeverity,
			SeveritySuggestion => DiagnosticSeverity.Info,
			SeverityWarning => DiagnosticSeverity.Warning,
			SeverityError => DiagnosticSeverity.Error,
			_ => registrySeverity,
		};
}
