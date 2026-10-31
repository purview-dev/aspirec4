using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>
/// Class-based LikeC4 registry analyzer. Reports the <c>[LikeC4Registry]</c>-only diagnostics
/// (ASPIREC4003, ASPIREC4004, ASPIREC4005, ASPIREC4006) — i.e. the rules that only require the
/// class definitions and do <em>not</em> need the AdditionalText (<c>.c4</c>) DSL files.
/// The DSL-file-dependent rules (ASPIREC4001, ASPIREC4002) are owned by
/// <see cref="LikeC4StrictValidatorGenerator"/>. See <see cref="LikeC4DiagnosticRules"/> for ownership tags.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LikeC4RegistryAnalyzer : DiagnosticAnalyzer
{
	const string WithGroupMethodName = "WithLikeC4Group";
	const string WithMetadataMethodName = "WithMetadata";

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(
			LikeC4DiagnosticRules.MultipleDefinitionsClasses,
			LikeC4DiagnosticRules.UndeclaredGroup,
			LikeC4DiagnosticRules.DuplicateTypeDeclaration,
			LikeC4DiagnosticRules.UndeclaredMetadataKey
		);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			// Honour the same opt-out as the generator.
			var options = startContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
			if (
				TryGetOption(options, "build_property.DisableAspireC4SourceGenerator") is { } disabledVal
				&& string.Equals(disabledVal, "true", StringComparison.OrdinalIgnoreCase)
			)
				return;

			var globalStrict = LikeC4DiagnosticRules.ParseGlobalStrict(
				TryGetOption(options, "build_property.AspireC4Strict")
			);

			var state = new RegistryState(startContext.Compilation);
			var config = new Lazy<ValidationConfig>(() => BuildConfig(state, globalStrict));

			startContext.RegisterSyntaxNodeAction(
				context => AnalyzeInvocation(context, state, config),
				SyntaxKind.InvocationExpression
			);

			startContext.RegisterCompilationEndAction(context => ReportStructural(context, state));
		});
	}

	// --- Configuration ---

	sealed class ValidationConfig(
		HashSet<string> groups,
		HashSet<string> metadata,
		DiagnosticSeverity? groupSeverity,
		DiagnosticSeverity? metadataSeverity,
		bool isExplicitlyEnabled
	)
	{
		public HashSet<string> Groups { get; } = groups;
		public HashSet<string> Metadata { get; } = metadata;
		public DiagnosticSeverity? GroupSeverity { get; } = groupSeverity;
		public DiagnosticSeverity? MetadataSeverity { get; } = metadataSeverity;
		public bool IsExplicitlyEnabled { get; } = isExplicitlyEnabled;
	}

	static ValidationConfig BuildConfig(
		RegistryState state,
		(DiagnosticSeverity? Severity, bool IncludesMetadata) globalStrict
	)
	{
		var registries = state.Registries;
		bool hasClassValidation = registries.Length > 0;
		var primaryDef = hasClassValidation ? registries[0] : null;

		int registryRaw = primaryDef?.RegistryStrictMode ?? LikeC4DiagnosticRules.SeverityInherit;
		bool registryExplicit = registryRaw != LikeC4DiagnosticRules.SeverityInherit;
		bool globalExplicit = globalStrict.Severity is not null;
		bool isExplicitlyEnabled = registryExplicit || globalExplicit;

		var registrySeverity = LikeC4DiagnosticRules.ResolveRegistrySeverity(
			registryRaw,
			globalStrict.Severity,
			hasClassValidation
		);

		int groupsTypeRaw = primaryDef?.GroupsTypeStrictMode ?? LikeC4DiagnosticRules.SeverityInherit;
		var groupsSeverity = LikeC4DiagnosticRules.ResolveTypeSeverity(groupsTypeRaw, registrySeverity);

		int metadataTypeRaw = primaryDef?.MetadataKeysTypeStrictMode ?? LikeC4DiagnosticRules.SeverityInherit;
		var metadataSeverity =
			metadataTypeRaw != LikeC4DiagnosticRules.SeverityInherit
				? LikeC4DiagnosticRules.ResolveTypeSeverity(metadataTypeRaw, registrySeverity)
				: (globalStrict.IncludesMetadata ? registrySeverity : null);

		var allowedGroups = LikeC4StrictValidatorGenerator.BuildAllowedSet(
			[],
			registries.SelectMany(static d => d.Groups)
		);
		var allowedMetadata = LikeC4StrictValidatorGenerator.BuildAllowedSet(
			[],
			registries
				.SelectMany(static d => d.MetadataKeys)
				.Select(LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison)
		);

		return new ValidationConfig(
			allowedGroups,
			allowedMetadata,
			groupsSeverity,
			metadataSeverity,
			isExplicitlyEnabled
		);
	}

	// --- Call-site validation (ASPIREC4004 / ASPIREC4006) ---

	static void AnalyzeInvocation(
		SyntaxNodeAnalysisContext context,
		RegistryState state,
		Lazy<ValidationConfig> configLazy
	)
	{
		if (context.Node is not InvocationExpressionSyntax invocation)
			return;

		if (
			invocation.ArgumentList.Arguments.Count == 0
			|| invocation.Expression is not MemberAccessExpressionSyntax member
		)
			return;

		var methodName = member.Name.Identifier.Text;
		if (methodName != WithGroupMethodName && methodName != WithMetadataMethodName)
			return;

		var firstArg = invocation.ArgumentList.Arguments[0].Expression;
		var constant = context.SemanticModel.GetConstantValue(firstArg, context.CancellationToken);
		if (!constant.HasValue || constant.Value is not string value)
			return;

		var config = configLazy.Value;

		if (methodName == WithGroupMethodName && config.GroupSeverity is { } groupSeverity)
		{
			if ((config.Groups.Count > 0 || config.IsExplicitlyEnabled) && !config.Groups.Contains(value))
			{
				var descriptor = LikeC4DiagnosticRules.WithSeverity(
					LikeC4DiagnosticRules.UndeclaredGroup,
					groupSeverity
				);
				context.ReportDiagnostic(Diagnostic.Create(descriptor, firstArg.GetLocation(), value));
			}

			return;
		}

		if (methodName == WithMetadataMethodName && config.MetadataSeverity is { } metadataSeverity)
		{
			if (
				(config.Metadata.Count > 0 || config.IsExplicitlyEnabled)
				&& !config.Metadata.Contains(LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison(value))
			)
			{
				var descriptor = LikeC4DiagnosticRules.WithSeverity(
					LikeC4DiagnosticRules.UndeclaredMetadataKey,
					metadataSeverity
				);
				context.ReportDiagnostic(Diagnostic.Create(descriptor, firstArg.GetLocation(), value));
			}
		}
	}

	// --- Structural validation (ASPIREC4003 / ASPIREC4005) ---

	static void ReportStructural(CompilationAnalysisContext context, RegistryState state)
	{
		var registries = state.Registries;

		if (registries.Length > 1)
		{
			for (int i = 1; i < registries.Length; i++)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						LikeC4DiagnosticRules.MultipleDefinitionsClasses,
						registries[i].Location,
						registries[i].DisplayName
					)
				);
			}
		}

		foreach (var def in registries)
		{
			foreach (var (typeName, dupLocation) in def.DuplicateTypeDeclarations)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(LikeC4DiagnosticRules.DuplicateTypeDeclaration, dupLocation, typeName)
				);
			}
		}
	}

	// --- Registry discovery ---

	/// <summary>
	/// Lazily discovers all <c>[LikeC4Registry]</c>-annotated classes in the compilation and extracts
	/// their <see cref="ClassDefinitions"/>. Discovery is independent of the analyzer's symbol-action
	/// scheduling, so it is safe under concurrent execution.
	/// </summary>
	sealed class RegistryState(Compilation compilation)
	{
		readonly Lazy<ImmutableArray<ClassDefinitions>> _registries = new(() => FindRegistryDefinitions(compilation));
		public ImmutableArray<ClassDefinitions> Registries => _registries.Value;

		static ImmutableArray<ClassDefinitions> FindRegistryDefinitions(Compilation compilation)
		{
			var builder = ImmutableArray.CreateBuilder<ClassDefinitions>();

			var registryAttribute = compilation.GetTypeByMetadataName(
				"Aspire.Hosting.AspireC4.LikeC4RegistryAttribute"
			);
			if (registryAttribute is null)
				return builder.ToImmutable();

			foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
			{
				var attributes = type.GetAttributes();
				bool isRegistry = attributes.Any(a =>
					registryAttribute is not null
					&& SymbolEqualityComparer.Default.Equals(a.AttributeClass, registryAttribute)
				);
				if (!isRegistry)
					continue;

				builder.Add(
					LikeC4StrictValidatorGenerator.ExtractClassDefinitions(type, attributes, CancellationToken.None)
				);
			}

			return builder.ToImmutable();
		}

		static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
		{
			foreach (var type in ns.GetTypeMembers())
			{
				foreach (var nested in GetAllNamedTypes(type))
					yield return nested;
			}

			foreach (var childNs in ns.GetNamespaceMembers())
			{
				foreach (var nested in GetAllNamedTypes(childNs))
					yield return nested;
			}
		}

		static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamedTypeSymbol type)
		{
			yield return type;
			foreach (var nested in type.GetTypeMembers())
			{
				foreach (var child in GetAllNamedTypes(nested))
					yield return child;
			}
		}
	}

	// --- Shared helpers ---

	static string? TryGetOption(AnalyzerConfigOptions options, string key) =>
		options.TryGetValue(key, out var value) ? value : null;
}
