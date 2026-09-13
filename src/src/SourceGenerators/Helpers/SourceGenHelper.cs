using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;
using Aspire.Hosting.AspireC4.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspire.Hosting.SourceGenerators.Helpers;

static partial class SourceGenHelper
{
	public static IncrementalValueProvider<StrictValidatorGenerationModel> CreateGenerationPipeline(
		IncrementalGeneratorInitializationContext context
	)
	{
		var strictMode = IncrementalPipeline.PropertyValueProvider(
			context,
			PropertyLibrary.AspireC4Strict,
			ParseStrictMode
		);

		var dslDefinition = context
			.AdditionalTextsProvider.Where(static f => LikeC4DSLHelpers.IsDSLFile(f.Path))
			.Select(static (f, ct) => LikeC4DSLHelpers.ParseDSLFile(f, ct))
			.WithTrackingName("GetAddtionalLikeC4DSLFiles");

		var generationContext =
			IncrementalPipeline.DefaultGenerationContextValueProvider<LikeC4StrictValidatorGenerator>(
				context,
				PropertyLibrary.DisableSourceGenerator
			);

		var registryTargets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			TypeLibrary.Aspire.Hosting.AspireC4.LikeC4RegistryAttribute,
			static (ctx, cancellationToken) => BuildRegistryTarget(ctx, cancellationToken),
			trackingName: "GetLikeC4RegistryTarget"
		);

		var tagCallSites = CreateCallSiteProvider(context, "WithTag");
		var kindCallSites = CreateCallSiteProvider(context, "WithKind");
		var groupCallSites = CreateCallSiteProvider(context, "WithLikeC4Group");
		var metadataCallSites = CreateMetadataCallSiteProvider(context);

		var model = generationContext
			.CollectWith(registryTargets, static (ctx, targets, _) => new StrictValidatorGenerationModel(ctx, targets))
			.CollectWith(
				tagCallSites,
				static (modelContext, tagSites, _) => modelContext with { TagCallSites = tagSites }
			)
			.CollectWith(
				kindCallSites,
				static (modelContext, kindSites, _) => modelContext with { KindCallSites = kindSites }
			)
			.CollectWith(
				groupCallSites,
				static (modelContext, groupSites, _) => modelContext with { GroupCallSites = groupSites }
			)
			.CollectWith(
				metadataCallSites,
				static (modelContext, metadataSites, _) => modelContext with { MetadataCallSites = metadataSites }
			)
			.CollectWith(
				dslDefinition,
				static (modelContext, dslDefinitions, ct) =>
					modelContext with
					{
						DSLDefinition = LikeC4DSLHelpers.MergeDSLDefinitions(dslDefinitions),
					}
			)
			.CombineWith(strictMode, static (modelContext, value, _) => modelContext with { StrictMode = value });

		return model;
	}

	static GeneratorResult<LikeC4RegistryTarget> BuildRegistryTarget(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var likeC4RegistryTarget = LikeC4RegistryAttributeData.FromAttributeData(context.TargetSymbol);
		var defaultSeverity = EnumLibrary.SeverityValues.Get(likeC4RegistryTarget.Strict);

		var getRegistryMembers = CollectRegistryMembers(
			(INamedTypeSymbol)context.TargetSymbol,
			defaultSeverity,
			cancellationToken
		);

		return GeneratorResult<LikeC4RegistryTarget>.Create(
			new LikeC4RegistryTarget(
				context.TargetSymbol.ToDisplayString(),
				SourceLocation.FromLocation(
					context.TargetSymbol.Locations.FirstOrDefault(static location => location.IsInSource)
				),
				defaultSeverity,
				getRegistryMembers,
				new EquatableArray<DuplicateRegistryType>(
					FindDuplicateRegistryTypes((INamedTypeSymbol)context.TargetSymbol)
				)
			)
		);
	}

	static StrictModeSettings ParseStrictMode(string? value)
	{
		if (
			value is null
			|| string.IsNullOrWhiteSpace(value)
			|| value.Equals("off", StringComparison.OrdinalIgnoreCase)
		)
			return default;

		if (value.Equals("suggestion", StringComparison.OrdinalIgnoreCase))
			return new(DiagnosticSeverity.Info, false);
		if (value.Equals("warning", StringComparison.OrdinalIgnoreCase))
			return new(DiagnosticSeverity.Warning, false);
		if (value.Equals("allincludingmetadata", StringComparison.OrdinalIgnoreCase))
			return new(DiagnosticSeverity.Error, true);
		if (
			value.Equals("error", StringComparison.OrdinalIgnoreCase)
			|| value.Equals("true", StringComparison.OrdinalIgnoreCase)
			|| value.Equals("yes", StringComparison.OrdinalIgnoreCase)
			|| value.Equals("all", StringComparison.OrdinalIgnoreCase)
		)
			return new(DiagnosticSeverity.Error, false);

		// If the value is unrecognized, return default settings (off)
		return default;
	}

#pragma warning disable PSGFR11 // No marker attribute exists for these fluent call sites; syntax is the only trigger.
	static IncrementalValuesProvider<CallSiteInfo> CreateCallSiteProvider(
		IncrementalGeneratorInitializationContext context,
		string methodName
	)
	{
		return context
			.SyntaxProvider.CreateSyntaxProvider(
				predicate: (node, _) => IsTargetInvocation(node, methodName),
				transform: ExtractCallSiteInfo
			)
			.Where(static v => v.HasValue)
			.Select(static (v, _) => v!.Value);
	}

	/// <summary>
	/// Collects the first argument (key) of every <c>.WithMetadata(key, value)</c> call site.
	/// </summary>
	static IncrementalValuesProvider<CallSiteInfo> CreateMetadataCallSiteProvider(
		IncrementalGeneratorInitializationContext context
	)
	{
		return context
			.SyntaxProvider.CreateSyntaxProvider(
				predicate: static (node, _) => IsTargetInvocation(node, "WithMetadata"),
				transform: static (ctx, ct) => ExtractCallSiteInfo(ctx, ct)
			)
			.Where(static v => v.HasValue)
			.Select(static (v, _) => v!.Value);
	}
#pragma warning restore PSGFR11

#pragma warning disable format
	static bool IsTargetInvocation(SyntaxNode node, string methodName) =>
		node
			is InvocationExpressionSyntax
			{
				ArgumentList.Arguments.Count: > 0,
				Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: var name },
			}
		&& name == methodName;
#pragma warning restore format

	static CallSiteInfo? ExtractCallSiteInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
	{
		var invocation = (InvocationExpressionSyntax)ctx.Node;
		var firstArg = invocation.ArgumentList.Arguments[0].Expression;
		var constant = ctx.SemanticModel.GetConstantValue(firstArg, ct);

		return constant.HasValue && constant.Value is string value
			? new(value, SourceLocation.FromLocation(firstArg.GetLocation()))
			: null;
	}
}
