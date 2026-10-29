using System.Collections.Immutable;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

public sealed class AspireC4EfModelGeneratorCacheTests
	: TUnitSourceGeneratorTestBase<AspireC4EfModelGenerator, AspireC4EfModelGeneratorTestOptions>
{
	const string Source = """
		using Microsoft.EntityFrameworkCore;
		namespace Sample;
		public class User { public int Id { get; set; } }
		public class AppDbContext : DbContext
		{
			public DbSet<User> Users { get; set; }
		}
		""";

	static ImmutableArray<StepReason> StepReasons(IncrementalCacheRun run, string stage) =>
		[.. run.Steps[stage].SelectMany(step => step.Outputs.Select(output => output.Reason))];

	static bool IsCachedOrUnchanged(ImmutableArray<StepReason> reasons) =>
		reasons.All(reason => reason is StepReason.Cached or StepReason.Unchanged);

	[Test]
	public async Task RunGenerator_FirstRun_ContextStagesAreNew(CancellationToken cancellationToken)
	{
		// Arrange
		string[] sources = [Source];

		// Act
		var result = await GenerateIncrementalAsync(sources, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(StepReasons(result.Runs[0], "AnalyseEfContext")).Contains(StepReason.New);
		await Assert.That(StepReasons(result.Runs[0], "CollectEfContexts")).Contains(StepReason.New);
	}

	[Test]
	public async Task RunGenerator_IdenticalRerun_ContextStagesRemainCached(CancellationToken cancellationToken)
	{
		// Arrange
		string[] sources = [Source];

		// Act
		var result = await GenerateIncrementalAsync(sources, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(IsCachedOrUnchanged(StepReasons(result.Runs[1], "AnalyseEfContext"))).IsTrue();
		await Assert.That(IsCachedOrUnchanged(StepReasons(result.Runs[1], "CollectEfContexts"))).IsTrue();
	}

	[Test]
	public async Task RunGenerator_UnrelatedSourceChange_ContextStagesRemainCached(CancellationToken cancellationToken)
	{
		// Arrange
		const string unrelatedSource = "namespace Other; public class Unrelated { }";
		const string changedSource = "namespace Other; public class Renamed { }";

		// Act
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source, unrelatedSource]), new IncrementalRunInput([Source, changedSource])],
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(IsCachedOrUnchanged(StepReasons(result.Runs[1], "AnalyseEfContext"))).IsTrue();
		await Assert.That(IsCachedOrUnchanged(StepReasons(result.Runs[1], "CollectEfContexts"))).IsTrue();
	}

	[Test]
	public async Task RunGenerator_ModelChange_ContextStagesAreModified(CancellationToken cancellationToken)
	{
		// Arrange
		var changedSource = Source.Replace("Users", "People", StringComparison.Ordinal);

		// Act
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([changedSource])],
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(StepReasons(result.Runs[1], "AnalyseEfContext")).Contains(StepReason.Modified);
		await Assert.That(StepReasons(result.Runs[1], "CollectEfContexts")).Contains(StepReason.Modified);
	}

	[Test]
	public async Task RunGenerator_ContextRemoved_AggregateIsModified(CancellationToken cancellationToken)
	{
		// Arrange
		const string noContextSource = "namespace Sample;";

		// Act
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([noContextSource])],
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(StepReasons(result.Runs[1], "AnalyseEfContext")).Contains(StepReason.Removed);
		await Assert.That(StepReasons(result.Runs[1], "CollectEfContexts")).Contains(StepReason.Modified);
	}

	[Test]
	public async Task RunGenerator_ReorderedContextFiles_AggregateRemainsUnchanged(CancellationToken cancellationToken)
	{
		// Arrange
		const string otherContext = """
			using Microsoft.EntityFrameworkCore;
			namespace Other;
			public class Event { public int Id { get; set; } }
			public class EventDbContext : DbContext
			{
				public DbSet<Event> Events { get; set; }
			}
			""";

		// Act
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source, otherContext]), new IncrementalRunInput([otherContext, Source])],
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(IsCachedOrUnchanged(StepReasons(result.Runs[1], "CollectEfContexts"))).IsTrue();
	}
}
