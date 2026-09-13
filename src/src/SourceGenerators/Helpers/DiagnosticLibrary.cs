using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

static class DiagnosticLibrary
{
	/// <summary>Emitted when a <c>.WithTag()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredTag = new(
		id: "ASPIREC4001",
		title: "Undeclared LikeC4 tag",
		messageFormat: "Tag '{0}' is not declared. Add it to a 'specification {{ tag {0} }}' block in a .c4 additional file, "
			+ $"or as 'public const string' in the 'Tags' nested class of your {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute} class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All tags passed to WithTag() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict has a non-off severity), or as public const string fields in the Tags nested class "
			+ $"of a {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}-annotated class."
	);

	/// <summary>
	/// Emitted when a <c>.WithKind()</c> argument is not declared in any active element-kind or
	/// relationship-kind definition source.
	/// </summary>
	public static readonly DiagnosticDescriptor UndeclaredKind = new(
		id: "ASPIREC4002",
		title: "Undeclared LikeC4 element or relationship kind",
		messageFormat: "Kind '{0}' is not declared. Add it to a 'specification {{ element {0} }}' or "
			+ "'specification {{ relationship {0} }}' block in a .c4 additional file, "
			+ $"or as 'public const string' in 'ElementKinds' or 'RelationshipKinds' nested class of your {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute} class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All kinds passed to WithKind() must be declared in the LikeC4 specification block of an additional "
			+ ".c4 file (when AspireC4Strict has a non-off severity), or as public const string fields in the ElementKinds or "
			+ $"RelationshipKinds nested class of a {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}-annotated class."
	);

	/// <summary>Emitted when more than one class per assembly carries <c>[LikeC4Registry]</c>.</summary>
	public static readonly DiagnosticDescriptor MultipleRegistryClassesDefined = new(
		id: "ASPIREC4003",
		title: $"Multiple {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute} classes",
		messageFormat: $"Only one class per assembly may carry {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}. Duplicate found: '{{0}}'.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: $"Only one class per assembly may be annotated with {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}. "
			+ "Consolidate all tag, element-kind, relationship-kind, group, and metadata-key definitions into a single class."
	);

	/// <summary>Emitted when a <c>.WithLikeC4Group()</c> argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredGroup = new(
		id: "ASPIREC4004",
		title: "Undeclared LikeC4 group",
		messageFormat: $"Group '{{0}}' is not declared. Add it as 'public const string' in the 'Groups' nested class of your {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute} class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All group names passed to WithLikeC4Group() must be declared as public const string fields "
			+ $"in the Groups nested class of a {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}-annotated class."
	);

	/// <summary>Emitted when a <c>.WithMetadata()</c> key argument is not declared in the active definitions.</summary>
	public static readonly DiagnosticDescriptor UndeclaredMetadataKey = new(
		id: "ASPIREC4006",
		title: "Undeclared LikeC4 metadata key",
		messageFormat: $"Metadata key '{{0}}' is not declared. Add it as 'public const string' in the 'MetadataKeys' nested class of your {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute} class.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "All metadata keys passed to WithMetadata() must be declared as public const string fields "
			+ $"in the MetadataKeys nested class of a {TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}-annotated class. "
			+ "Keys are compared after normalising whitespace and punctuation to underscores, and are case-insensitive."
	);

	/// <summary>
	/// Emitted when a registry type is declared both via a named nested class <em>and</em>
	/// via individual <c>[KnownType]</c> attributes on constants.
	/// </summary>
	public static readonly DiagnosticDescriptor DuplicateTypeDeclaration = new(
		id: "ASPIREC4005",
		title: "Duplicate LikeC4 registry type declaration",
		messageFormat: $"Registry type '{{0}}' is declared both as a nested class and via {TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttribute} attributes. Use only one declaration approach per type.",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: $"A registry type (e.g. Tag, Group) must be declared either as a nested static class "
			+ $"(e.g. 'public static class Tags {{ ... }}') OR via {TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttribute} attributes on individual constants, not both."
	);

	/// <summary>
	/// Emitted when a registry type is declared both via a named nested class <em>and</em>
	/// via individual <c>[KnownType]</c> attributes on constants.
	/// </summary>
	public static readonly DiagnosticDescriptor UnknownRegistryType = new(
		id: "ASPIREC4007",
		title: "The declared class is an unknown registry type",
		messageFormat: "Registry type '{0}' is an unknown name. Valid names are Tag, ElementKind, RelationshipKind, Group, or MetadataKey (with purals accepted).",
		category: "AspireC4",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: @"Valid registry type names are:

- Tag or Tags
- ElementKind, ElementKinds, Element, or Elements
- RelationshipKind, RelationshipKinds, Relationship, or Relationships
- Group or Groups
- MetadataKey or MetadataKeys

Multiple uses of the same registry type are not permitted."
	);
}
