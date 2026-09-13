using Microsoft.CodeAnalysis.Text;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

static class MarkerAttributeEmitter
{
	public static IEnumerable<(string HintName, SourceText Source)> EmitMarkAttribute()
	{
		yield return (
			GetHintName(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute),
			LikeC4RegistryAttribute()
		);
		yield return (GetHintName(TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttribute), KnownTypeAttribute());
		yield return (GetHintName(TypeLibrary.Aspire.Hosting.AspireC4.SeverityAttribute), SeverityAttribute());
		yield return (GetHintName(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryType), LikeC4RegistryType());
		yield return (GetHintName(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity), LikeC4Severity());
	}

	static string GetHintName(TypeIdentity type) => $"{type.Name}.g.cs";

	static SourceText LikeC4RegistryAttribute()
	{
		var writer = CreateCodeWriter(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute);

		writer
			.XmlSummary(
				"Marks a static class as the single source of truth for LikeC4 registry values",
				"(tags, element kinds, relationship kinds, groups, metadata keys).",
				"Only one class per assembly may carry this attribute."
			)
			.XmlRemarks(
				"Declare values as <c>public const string</c> fields inside nested static classes",
				"named <c>Tags</c>, <c>ElementKinds</c>, <c>RelationshipKinds</c>, <c>Groups</c>,",
				$"or <c>MetadataKeys</c>, OR directly on the class with {XmlSee(TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttribute)}."
			)
			.AttributeClass(
				new(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute),
				AttributeTargets.Class,
				bodyWriter =>
					bodyWriter
						.XmlSummary(
							$"Registry-level diagnostic severity override. Default is {XmlSee(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity.StaticMember(EnumLibrary.SeverityValues.Inherit.Name))}."
						)
						.Property(
							new(
								"Strict",
								TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity,
								TypeDeclarationAccessibility.Public
							)
							{
								IsInitOnly = true,
								Initializer = TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity.StaticMember(
									EnumLibrary.SeverityValues.Inherit.Name
								),
							}
						)
			);

		return writer;
	}

	static SourceText KnownTypeAttribute()
	{
		var writer = CreateCodeWriter(TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttribute);
		return writer
			.XmlSummary(
				"Marks a <c>public string constant</c> field as a known LikeC4 registry value of a specific type."
			)
			.AttributeClass(
				new(TypeLibrary.Aspire.Hosting.AspireC4.KnownTypeAttribute)
				{
					PrimaryConstructorParameters =
					[
						new("type", TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryType),
					],
				},
				AttributeTargets.Field,
				bodyWriter =>
				{
					bodyWriter
						.XmlSummary("The registry type this constant belongs to.")
						.Property(
							new(
								"Type",
								TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryType,
								TypeDeclarationAccessibility.Public
							)
							{
								Initializer = "type",
							}
						);

					bodyWriter
						.XmlSummary(
							"Per-type severity override.",
							$"Default is {XmlSee(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity.StaticMember(EnumLibrary.SeverityValues.Inherit.Name))}."
						)
						.Property(
							new(
								"Strict",
								TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity,
								TypeDeclarationAccessibility.Public
							)
							{
								IsInitOnly = true,
								Initializer = TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity.StaticMember(
									EnumLibrary.SeverityValues.Inherit.Name
								),
							}
						);
				}
			);
	}

	static SourceText SeverityAttribute()
	{
		var writer = CreateCodeWriter(TypeLibrary.Aspire.Hosting.AspireC4.SeverityAttribute);
		return writer
			.XmlSummary(
				$"Marks a registry class, nested under another with {XmlSee(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute)},",
				"with a default {XmlSee(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity)}."
			)
			.AttributeClass(
				new(TypeLibrary.Aspire.Hosting.AspireC4.SeverityAttribute)
				{
					PrimaryConstructorParameters =
					[
						new("severity", TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity),
					],
				},
				AttributeTargets.Class,
				bodyWriter =>
					bodyWriter
						.XmlSummary("The diagnostic severity for this registry class.")
						.Property(
							new(
								"Severity",
								TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity,
								TypeDeclarationAccessibility.Public
							)
							{
								Initializer = "severity",
							}
						)
			);
	}

	static SourceText LikeC4RegistryType()
	{
		var writer = CreateCodeWriter(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryType);
		return writer
			.XmlSummary("Identifies which LikeC4 registry type a constant belongs to.")
			.Enum(
				new(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryType),
				[
					new(EnumLibrary.RegistryTypeValues.Tag.Name, EnumLibrary.RegistryTypeValues.Tag.Value)
					{
						XmlSummary = ["The constant is a LikeC4 tag (used with <c>.WithTag()</c>)."],
					},
					new(
						EnumLibrary.RegistryTypeValues.ElementKind.Name,
						EnumLibrary.RegistryTypeValues.ElementKind.Value
					)
					{
						XmlSummary = ["The constant is a LikeC4 element kind (used with <c>.WithKind()</c>)."],
					},
					new(
						EnumLibrary.RegistryTypeValues.RelationshipKind.Name,
						EnumLibrary.RegistryTypeValues.RelationshipKind.Value
					)
					{
						XmlSummary = ["The constant is a LikeC4 relationship kind (used with <c>.WithKind()</c>)."],
					},
					new(EnumLibrary.RegistryTypeValues.Group.Name, EnumLibrary.RegistryTypeValues.Group.Value)
					{
						XmlSummary = ["The constant is a LikeC4 group (used with <c>.WithLikeC4Group()</c>)."],
					},
					new(
						EnumLibrary.RegistryTypeValues.MetadataKey.Name,
						EnumLibrary.RegistryTypeValues.MetadataKey.Value
					)
					{
						XmlSummary = ["The constant is a LikeC4 metadata key (used with <c>.WithMetadata()</c>)."],
					},
				]
			);
	}

	static SourceText LikeC4Severity()
	{
		var writer = CreateCodeWriter(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity);
		return writer
			.XmlSummary("Controls the diagnostic severity for a registry class or type.")
			.Enum(
				new(TypeLibrary.Aspire.Hosting.AspireC4.LikeC4Severity),
				[
					new(EnumLibrary.SeverityValues.Inherit.Name, EnumLibrary.SeverityValues.Inherit.Value)
					{
						XmlSummary =
						[
							"Inherits severity from the parent scope (registry → MSBuild → default Suggestion",
							$"when [{TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute}] exists).",
						],
					},
					new(EnumLibrary.SeverityValues.Off.Name, EnumLibrary.SeverityValues.Off.Value)
					{
						XmlSummary = ["Disables validation for this scope entirely."],
					},
					new(EnumLibrary.SeverityValues.Suggestion.Name, EnumLibrary.SeverityValues.Suggestion.Value)
					{
						XmlSummary = ["Emits an IDE suggestion (hidden diagnostic)."],
					},
					new(EnumLibrary.SeverityValues.Warning.Name, EnumLibrary.SeverityValues.Warning.Value)
					{
						XmlSummary = ["Emits a compiler warning."],
					},
					new(EnumLibrary.SeverityValues.Error.Name, EnumLibrary.SeverityValues.Error.Value)
					{
						XmlSummary = ["Emits a compiler error."],
					},
				]
			);
	}

	static CodeWriter CreateCodeWriter(TypeIdentity type)
	{
		CodeWriter writer = new(
			GenerationSettings.Create<LikeC4StrictValidatorGenerator>(PropertyLibrary.DisableSourceGenerator)
		);

		return writer.AutoGeneratedHeader().FileScopedNamespace(type);
	}
}
