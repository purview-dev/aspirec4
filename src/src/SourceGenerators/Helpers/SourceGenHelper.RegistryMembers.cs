using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;
using Aspire.Hosting.AspireC4.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.SourceGenerators.Helpers;

partial class SourceGenHelper
{
	static EquatableArray<RegistrySpecifications> CollectRegistryMembers(
		INamedTypeSymbol targetSymbol,
		SeverityDefinition defaultSeverity,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers = new()
		{
			{ EnumLibrary.RegistryTypeValues.Tag, [] },
			{ EnumLibrary.RegistryTypeValues.ElementKind, [] },
			{ EnumLibrary.RegistryTypeValues.RelationshipKind, [] },
			{ EnumLibrary.RegistryTypeValues.Group, [] },
			{ EnumLibrary.RegistryTypeValues.MetadataKey, [] },
		};

		foreach (var member in targetSymbol.GetTypeMembers())
		{
			if (member.TypeKind == TypeKind.Class)
				ScanNestedType(defaultSeverity, registryMembers, member);
		}

		foreach (var member in targetSymbol.GetMembers())
		{
			if (member is IFieldSymbol fieldSymbol && IsValidField(fieldSymbol))
			{
				ScanField(registryMembers, fieldSymbol);
			}
		}

		return new EquatableArray<RegistrySpecifications>([
			.. registryMembers
				.Where(m => m.Value.Count > 0)
				.Select(m => new RegistrySpecifications(
					m.Key,
					new EquatableArray<RegistrySpecDefinition>([.. m.Value])
				)),
		]);
	}

	static bool IsValidField(IFieldSymbol fieldSymbol) =>
		fieldSymbol.IsConst && fieldSymbol.Type.SpecialType == SpecialType.System_String;

	static ImmutableArray<DuplicateRegistryType> FindDuplicateRegistryTypes(INamedTypeSymbol targetSymbol)
	{
		HashSet<RegistryTypeDefinition> nestedTypes =
		[
			.. targetSymbol
				.GetTypeMembers()
				.Select(static type => EnumLibrary.RegistryTypeValues.GetByName(type.Name))
				.Where(static type => type != RegistryTypeDefinition.Empty),
		];

		var duplicates = ImmutableArray.CreateBuilder<DuplicateRegistryType>();
		foreach (var field in targetSymbol.GetMembers().OfType<IFieldSymbol>().Where(IsValidField))
		{
			var attribute = KnownTypesAttributeData.FromAttributeData(field);
			if (!attribute.Exists)
				continue;

			var registryType = EnumLibrary.RegistryTypeValues.GetByName(attribute.Type);
			if (nestedTypes.Contains(registryType))
			{
				duplicates.Add(
					new(
						registryType.Name,
						SourceLocation.FromLocation(
							field.Locations.FirstOrDefault(static location => location.IsInSource)
						)
					)
				);
			}
		}

		return duplicates.ToImmutable();
	}

	static bool ScanField(
		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers,
		IFieldSymbol fieldSymbol
	)
	{
		var knownTypeAttribute = KnownTypesAttributeData.FromAttributeData(fieldSymbol);
		if (!knownTypeAttribute.Exists)
			return false;

		var registrationType = EnumLibrary.RegistryTypeValues.GetByName(knownTypeAttribute.Type);
		if (registrationType == RegistryTypeDefinition.Empty)
			return false;

		registryMembers[registrationType]
			.Add(new((string)fieldSymbol.ConstantValue!, EnumLibrary.SeverityValues.Get(knownTypeAttribute.Strict)));

		return true;
	}

	static bool ScanNestedType(
		SeverityDefinition defaultSeverity,
		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers,
		INamedTypeSymbol nestedType
	)
	{
		var registrationType = EnumLibrary.RegistryTypeValues.GetByName(nestedType.Name);
		if (registrationType == RegistryTypeDefinition.Empty)
			return false;

		var severityAttribute = SeverityAttributeData.FromAttributeData(nestedType);
		var severity = severityAttribute.Exists
			? EnumLibrary.SeverityValues.Get(severityAttribute.Severity)
			: defaultSeverity;

		return ScanNestedClassFields(severity, registryMembers[registrationType], nestedType);
	}

	static bool ScanNestedClassFields(
		SeverityDefinition severityDefinition,
		List<RegistrySpecDefinition> specDefinitions,
		INamedTypeSymbol nestedType
	)
	{
		var foundField = false;
		foreach (var member in nestedType.GetMembers())
		{
			if (member is IFieldSymbol fieldSymbol && IsValidField(fieldSymbol))
			{
				specDefinitions.Add(new((string)fieldSymbol.ConstantValue!, severityDefinition));
				foundField = true;
			}
		}

		return foundField;
	}
}
