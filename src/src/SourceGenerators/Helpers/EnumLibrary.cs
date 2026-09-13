using Aspire.Hosting.AspireC4.SourceGenerators.Models;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

static partial class EnumLibrary
{
	public static class SeverityValues
	{
		public static readonly SeverityDefinition Inherit = new(nameof(Inherit), 0);

		public static readonly SeverityDefinition Off = new(nameof(Off), 1);

		public static readonly SeverityDefinition Suggestion = new(nameof(Suggestion), 2);

		public static readonly SeverityDefinition Warning = new(nameof(Warning), 3);

		public static readonly SeverityDefinition Error = new(nameof(Error), 4);

		public static SeverityDefinition Get(string name)
		{
			if (MatchesEnumMember(name, Inherit.FullName, Inherit.Name))
				return Inherit;
			if (MatchesEnumMember(name, Off.FullName, Off.Name))
				return Off;
			if (MatchesEnumMember(name, Suggestion.FullName, Suggestion.Name))
				return Suggestion;
			if (MatchesEnumMember(name, Warning.FullName, Warning.Name))
				return Warning;
			if (MatchesEnumMember(name, Error.FullName, Error.Name))
				return Error;

			// If no match is found, return an empty SeverityDefinition
			return SeverityDefinition.Empty;
		}
	}

	public static class RegistryTypeValues
	{
		public static readonly RegistryTypeDefinition Tag = new(nameof(Tag), 0, new(["Tag", "Tags"]));

		public static readonly RegistryTypeDefinition ElementKind = new(
			nameof(ElementKind),
			1,
			new(["ElementKind", "ElementKinds", "Element", "Elements"])
		);

		public static readonly RegistryTypeDefinition RelationshipKind = new(
			nameof(RelationshipKind),
			2,
			new(["RelationshipKind", "RelationshipKinds", "Relationship", "Relationships"])
		);

		public static readonly RegistryTypeDefinition Group = new(nameof(Group), 3, new(["Group", "Groups"]));

		public static readonly RegistryTypeDefinition MetadataKey = new(
			nameof(MetadataKey),
			4,
			new(["MetadataKey", "MetadataKeys"])
		);

		public static RegistryTypeDefinition GetByName(string name)
		{
			if (MatchesEnumMember(name, Tag.FullName, Tag.Name) || Tag.ValidTypeNames.Any(m => m == name))
				return Tag;
			if (
				MatchesEnumMember(name, ElementKind.FullName, ElementKind.Name)
				|| ElementKind.ValidTypeNames.Any(m => m == name)
			)
				return ElementKind;
			if (
				MatchesEnumMember(name, RelationshipKind.FullName, RelationshipKind.Name)
				|| RelationshipKind.ValidTypeNames.Any(m => m == name)
			)
				return RelationshipKind;
			if (MatchesEnumMember(name, Group.FullName, Group.Name) || Group.ValidTypeNames.Any(m => m == name))
				return Group;
			if (
				MatchesEnumMember(name, MetadataKey.FullName, MetadataKey.Name)
				|| MetadataKey.ValidTypeNames.Any(m => m == name)
			)
				return MetadataKey;

			// If no match is found, return an empty RegistryTypeDefinition
			return RegistryTypeDefinition.Empty;
		}
	}

	static bool MatchesEnumMember(string value, string fullName, string memberName) =>
		value == memberName || value == fullName || value.EndsWith("." + memberName, StringComparison.Ordinal);
}
