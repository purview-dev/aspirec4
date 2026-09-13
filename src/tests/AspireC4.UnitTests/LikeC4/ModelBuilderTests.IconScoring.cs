using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_RabbitMQTypeName_InfersRabbitmqIcon()
	{
		// Arrange
		// Regression: "RabbitMQContainerResource" previously normalised to "rabbit mqcontainer resource"
		// because the uppercase-uppercase-lowercase boundary (MQ→C) was not split.
		// Fix: NormalizeForIconLookup now detects the transition and produces
		// "rabbit mq container resource" → stop ["container","resource"] → queryTokens ["rabbit","mq"].
		// effectiveQueryLength = 1 (only "rabbit" ≥ MinContainmentLength=3; "mq" is excluded from
		// the denominator but "rabbit" prefix-matches "rabbitmq" at 0.6/1 = 0.6 → tech:rabbitmq.
		RabbitMQContainerResource resource = new("my-queue");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:rabbitmq");
	}

	[Test]
	public async Task Build_MySQLTypeName_InfersMysqlIcon()
	{
		// Arrange
		// The CamelCase fix for uppercase-run boundaries produces "my sql database resource"
		// from "MySQLDatabaseResource" (previously "my sqldatabase resource").
		// In practice, MySQL resources are named "mysql" or similar — the resource name is the
		// primary signal and produces an exact match for tech:mysql.
		MySQLDatabaseResource resource = new("mysql");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mysql");
	}

	[Test]
	public async Task Build_BestOverallScoring_ResourceNameWinsOverNoisyTypeName()
	{
		// Arrange
		// Best-overall scoring: even when a noisy early candidate (type FullName) produces
		// query tokens that score marginally above MinScore for a wrong icon, the clean resource
		// name candidate scores higher overall and wins.
		// This resource has a generic type name but a clear resource name "mongodb".
		GenericDatabaseContainerResource resource = new("mongodb");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mongodb");
	}

	[Test]
	public async Task Build_NumericOnlyTokensFiltered_FromIconMatching()
	{
		// Arrange
		// Pure numeric tokens like "7" or "16" (e.g. from Docker tag tokenisation of
		// "redis-7" or version-suffixed names) must not inflate queryTokens.Length
		// and dilute the score of legitimate tokens.
		// Resource name "redis-7" → tokens ["redis","7"] → filter "7" → ["redis"] → exact match.
		var resource = CreateContainerResource("redis-7");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "redis" });

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	// Named to simulate a RabbitMQ container resource so the type-name candidate path is exercised.
	sealed class RabbitMQContainerResource(string name) : Resource(name);

	// Named to simulate a MySQL database resource.
	sealed class MySQLDatabaseResource(string name) : Resource(name);

	// Generic type name — the resource name provides the icon signal.
	sealed class GenericDatabaseContainerResource(string name) : Resource(name);

	// Named to match the real Aspire.Hosting.JavaScript.NodeAppResource so that the icon
	// matcher tokenises "Node" + "App" separately, and "JavaScript" in the parent namespace
	// is tokenised separately too — producing duplicate "node" tokens before the Distinct() fix.
	sealed class NodeAppResource(string name) : Resource(name);
}
