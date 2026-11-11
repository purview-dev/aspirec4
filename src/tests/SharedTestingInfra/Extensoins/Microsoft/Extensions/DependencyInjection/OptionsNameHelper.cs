using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class OptionsNameHelper
{
	static readonly string[] DefaultMemberNames =
	[
		"SectionName",
		"ConfigurationSectionName",
		"ConfigurationName",
		"OptionsSectionName",
		"OptionsName",
		"ConfigSectionName",
		"ConfigName",
		"Name",
	];

	static readonly string[] ExcludedOptionsTypeSuffixNames = ["Settings", "Options"];

	public static OptionsBuilder<TOptions> CreateOptionsBuilder<TOptions>() => new();

	public static string GetPropertyPath<TOptions>(
		Expression<Func<TOptions, object?>> expression,
		string seperator = "__"
	)
	{
		ArgumentNullException.ThrowIfNull(expression);

		var body = expression.Body is UnaryExpression unary ? unary.Operand : expression.Body;
		return body is MemberExpression member
			? $"{GetSectionName<TOptions>()}{seperator}{member.Member.Name}"
			: throw new ArgumentException("Expression must be a member access.", nameof(expression));
	}

	public static string GetSectionName<TOptions>()
	{
		var optionsType = typeof(TOptions);

		foreach (var memberName in DefaultMemberNames)
		{
			var value = TryGetStaticStringProperty(optionsType, memberName)
				?? TryGetStaticStringField(optionsType, memberName);

			if (!string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
		}

		var typeName = optionsType.Name;
		if (ExcludedOptionsTypeSuffixNames.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal)))
		{
			typeName = typeName[
				..^ExcludedOptionsTypeSuffixNames
					.First(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal))
					.Length
			];
		}

		return typeName;
	}

	static string? TryGetStaticStringProperty(Type optionsType, string propertyName)
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

		var property = optionsType.GetProperty(propertyName, flags);

		return property is null ? null
			: property.PropertyType == typeof(string)
				? property.GetMethod is null ? null
					: property.GetValue(null) as string
			: null;
	}

	static string? TryGetStaticStringField(Type optionsType, string fieldName)
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

		var field = optionsType.GetField(fieldName, flags);
		return field is null ? null
			: field.FieldType == typeof(string) ? field.GetValue(null) as string
			: null;
	}
}
