using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>
/// Proves the strict-validator pipeline caches correctly stage-by-stage. An unchanged input must keep
/// every framework stage <c>Cached</c>/<c>Unchanged</c>, and a targeted change must mark only the stages
/// whose inputs actually changed <c>Modified</c> while unrelated stages stay cached.
/// </summary>
public sealed class LikeC4StrictValidatorGeneratorCacheTests
	: TUnitSourceGeneratorTestBase<LikeC4StrictValidatorGenerator, LikeC4StrictValidatorGeneratorTestOptions>
{
	const string Source = """
		using Aspire.Hosting.AspireC4;
		namespace TestApp;

		[LikeC4Registry]
		internal class MyRegistry
		{
			public static class Tags { public const string External = "external"; }
		}

		class Setup
		{
			static void Configure()
			{
				var a = new object();
				a.WithTag("external");
			}
		}
		""";

	const string ChangedSource = """
		using Aspire.Hosting.AspireC4;
		namespace TestApp;

		[LikeC4Registry]
		internal class RenamedRegistry
		{
			public static class Tags { public const string Internal = "internal"; }
		}

		class Setup
		{
			static void Configure()
			{
				var a = new object();
				a.WithTag("external");
			}
		}
		""";

	// The framework-named stages that must stay Cached/Unchanged when their inputs do not change. The
	// generator emits its own marker attributes via post-initialization output, so Roslyn's internal
	// ForAttributeWithMetadataName Compilation step is legitimately Modified on an identical rerun.
	static readonly string[] FrameworkStages =
	[
		"GetMSBuildPropertyValue_AspireC4Strict",
		"GetGenerationConfiguration",
		"GetGenerationContext_EmptyCapabilities",
		"GetLikeC4RegistryTarget",
	];

	static ImmutableDictionary<string, ImmutableArray<StepReason>> StepReasons(IncrementalCacheRun run)
	{
		var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<StepReason>>();
		foreach (var pair in run.Steps)
			builder[pair.Key] =
			[
				.. pair.Value.SelectMany(static step => step.Outputs.Select(static output => output.Reason)),
			];
		return builder.ToImmutable();
	}

	static bool IsCachedOrUnchanged(ImmutableArray<StepReason> reasons) =>
		reasons.All(static reason => reason is StepReason.Cached or StepReason.Unchanged);

	[Test]
	public async Task FirstRun_AllStagesAreNew(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync([Source], cancellationToken: cancellationToken);

		var first = StepReasons(result.Runs[0]);
		await Assert.That(first).IsNotEmpty();
		await Assert.That(first.Values.SelectMany(static r => r).All(static r => r == StepReason.New)).IsTrue();
	}

	[Test]
	public async Task IdenticalRerun_AllFrameworkStagesCachedOrUnchanged(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync([Source], cancellationToken: cancellationToken);

		var second = StepReasons(result.Runs[1]);
		await Assert
			.That(
				FrameworkStages.All(stage => second.TryGetValue(stage, out var reasons) && IsCachedOrUnchanged(reasons))
			)
			.IsTrue();
	}

	[Test]
	public async Task SourceChange_MarksAttributeStageModified_PropertyStagesStayCached(
		CancellationToken cancellationToken
	)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([ChangedSource])],
			cancellationToken: cancellationToken
		);

		var second = StepReasons(result.Runs[1]);
		await Assert.That(second["GetLikeC4RegistryTarget"]).Contains(StepReason.Modified);
		await Assert.That(IsCachedOrUnchanged(second["GetMSBuildPropertyValue_AspireC4Strict"])).IsTrue();
		await Assert.That(IsCachedOrUnchanged(second["GetGenerationConfiguration"])).IsTrue();
	}

	[Test]
	public async Task PropertyChange_MarksPropertyStageModified_AttributeStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		var result = await GenerateIncrementalAsync(
			[
				new IncrementalRunInput([Source]),
				new IncrementalRunInput([Source], [("build_property." + PropertyLibrary.AspireC4Strict, "error")]),
			],
			cancellationToken: cancellationToken
		);

		var second = StepReasons(result.Runs[1]);
		await Assert.That(second["GetMSBuildPropertyValue_AspireC4Strict"]).Contains(StepReason.Modified);
		await Assert.That(IsCachedOrUnchanged(second["GetLikeC4RegistryTarget"])).IsTrue();
	}
}
