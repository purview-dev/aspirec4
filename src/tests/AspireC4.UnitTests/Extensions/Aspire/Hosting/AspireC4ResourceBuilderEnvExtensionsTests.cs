using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting;

public sealed class AspireC4ResourceBuilderEnvExtensionsTests
{
	[Test]
	public async Task WithLikeC4ConnectionReference_AddsTheConfiguredRelationship()
	{
		// Arrange
		var (worker, database) = CreateResources();

		// Act
		worker.WithLikeC4ConnectionReference(database, static opts => opts.WithLabel("Reads").WithKind("tcp-ip"));

		// Assert
		var relationship = Relationship(worker);
		await Assert.That(relationship).IsNotNull();
		await Assert.That(relationship!.TargetName).IsEqualTo("db");
		await Assert.That(relationship.Label).IsEqualTo("Reads");
		await Assert.That(relationship.Kind).IsEqualTo("tcp-ip");
	}

	[Test]
	public async Task WithLikeC4ConnectionReference_ReferencesTheConnectionThroughAspire()
	{
		// Arrange
		var (worker, database) = CreateResources();

		// Act
		worker.WithLikeC4ConnectionReference(database);

		// Assert
		await Assert.That(ReferencesThroughAspire(worker, "db")).IsTrue();
	}

	[Test]
	public async Task WithLikeC4ConnectionReference_WithSkipAspireReference_DrawsTheRelationshipOnly()
	{
		// Arrange
		var (worker, database) = CreateResources();

		// Act
		worker.WithLikeC4ConnectionReference(database, skipAspireReference: true);

		// Assert
		await Assert.That(Relationship(worker)).IsNotNull();
		await Assert.That(ReferencesThroughAspire(worker, "db")).IsFalse();
	}

	[Test]
	public async Task WithLikeC4Reference_ConnectionStringOverload_BehavesAsWithLikeC4ConnectionReference()
	{
		// Arrange
		var (worker, database) = CreateResources();

		// Act
		// A named connection-string argument is what selects this overload: with only a target and a
		// callback, C# prefers the relationship-only overload, which needs no omitted optional parameters.
		worker.WithLikeC4Reference(database, static opts => opts.WithLabel("Reads"), optional: false);

		// Assert
		await Assert.That(Relationship(worker)?.Label).IsEqualTo("Reads");
		await Assert.That(ReferencesThroughAspire(worker, "db")).IsTrue();
	}

	static (
		IResourceBuilder<ExecutableResource> Worker,
		IResourceBuilder<IResourceWithConnectionString> Database
	) CreateResources()
	{
		var appBuilder = CreateAppBuilder();
		return (appBuilder.AddExecutable("worker", "dotnet", "."), appBuilder.AddConnectionString("db"));
	}

	static LikeC4RelationshipDetailsAnnotation? Relationship(IResourceBuilder<ExecutableResource> worker) =>
		worker.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().LastOrDefault();

	static bool ReferencesThroughAspire(IResourceBuilder<ExecutableResource> worker, string target) =>
		worker
			.Resource.Annotations.OfType<ResourceRelationshipAnnotation>()
			.Any(r => r.Resource.Name == target && r.Type == "Reference");
}
