using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Icons;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4;

[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "This class is complex because it handles many different resource types and state changes. However, I will come back to this at somepoint."
)]
public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_WithNoResources_ReturnsEmptyModel()
	{
		// Arrange
		// Act
		var model = ModelBuilder.Build([]);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ProjectResource_MapsToComponentKind()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Component);
		await Assert.That(model.Elements[0].Name).IsEqualTo("api");
	}

	[Test]
	public async Task Build_ContainerResource_MapsToContainerKind()
	{
		// Arrange
		var resource = CreateContainerResource("redis");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Container);
	}

	[Test]
	public async Task Build_ExecutableResource_MapsToExecutableKind()
	{
		// Arrange
		ExecutableResource resource = new("worker", "dotnet", ".");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Executable);
	}

	[Test]
	public async Task Build_ResourceWithConnectionString_MapsToDatabaseKind()
	{
		// Arrange
		TestDatabaseResource resource = new("db");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Database);
	}

	[Test]
	public async Task Build_UnknownResource_MapsToSystemKind()
	{
		// Arrange
		TestSystemResource resource = new("ext");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.System);
	}

	[Test]
	public async Task Build_ResourceWithExcludeAnnotation_IsSkipped()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new ExcludeFromLikeC4Annotation());

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_HiddenResource_IsSkipped()
	{
		// Arrange
		TestSystemResource resource = new("hidden");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "System",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_ResourceWithNodeDetailsAnnotation_UsesAnnotationValues()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API Service")
				.WithTechnology("ASP.NET Core")
				.WithDescription("Handles HTTP requests")
				.WithIcon("bootstrap:gear")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Label).IsEqualTo("API Service");
		await Assert.That(element.Technology).IsEqualTo("ASP.NET Core");
		await Assert.That(element.Description).IsEqualTo("Handles HTTP requests");
		await Assert.That(element.Icon).IsEqualTo("bootstrap:gear");
	}

	[Test]
	public async Task Build_ResourceWithNoNodeDetailsAnnotation_UsesResourceNameAsLabel()
	{
		// Arrange
		var resource = CreateProjectResource("my-api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Label).IsEqualTo("my-api");
	}

	[Test]
	public async Task Build_ProjectResource_InfersDotnetIcon()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_AzureResource_InfersBundledAzureIcon()
	{
		// Arrange
		TestSystemResource resource = new("redis");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Azure.Redis", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_AzureSurrogateContainer_InfersAzureIconFromName()
	{
		// Arrange
		// RunAsContainer() produces a ContainerResource named after the Azure resource (e.g. "azure-redis")
		var resource = CreateContainerResource("azure-redis");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_PlainRedisContainer_UsesGenericTechIcon()
	{
		// Arrange
		var resource = CreateContainerResource("redis");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_PostgresResource_WithAzureTechnology_InfersAzureIcon()
	{
		// Arrange
		// When the user labels the technology "Azure Postgres", the cloud phase detects "azure"
		// and infers an azure postgres icon instead of the generic tech:postgresql.
		var resource = CreateContainerResource("postgres");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("Postgres")
				.WithTechnology("Azure Postgres")
				.WithDescription("Managed PostgreSQL")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon!).StartsWith("azure:");
		await Assert.That(model.Elements[0].Icon!).Contains("postgre");
	}

	[Test]
	public async Task Build_ContainerWithLibraryRedisImage_InfersRedisIcon()
	{
		// Arrange
		// Regression: "library/redis" was matching "heroku-redis" due to Jaccard tie-breaking.
		// "library" is a stop token, leaving query ["redis"] which should score "redis" at 1.0
		// and "heroku-redis" at 0.5 (unmatched "heroku" token penalty).
		var resource = CreateContainerResource("myredis");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "library/redis" });

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_ContainerWithLibraryPostgresImage_InfersPostgresqlIcon()
	{
		// Arrange
		// Regression: "library/postgres" was matching "testing-library" because "library"
		// dominated the score. Stop tokens now strip "library", leaving query ["postgres"]
		// which prefix-matches "postgresql" at 0.64 and non-prefix-matches "postgraphile" at 0.40.
		var resource = CreateContainerResource("mypostgres");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "library/postgres" });

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:postgresql");
	}

	[Test]
	public async Task Build_NodeAppInstallerExecutable_InfersNodejsIcon()
	{
		// Arrange
		// Regression: "node-app-installer" → queryTokens were ["node", "installer"] because
		// "installer" was not a stop token. The 2-token query diluted the score: 0.533 / 2 = 0.267
		// (below MinScore 0.35). Adding "installer" to QueryStopTokens leaves ["node"] → 0.533 ✓.
		ExecutableResource resource = new("node-app-installer", "node", ".");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_JavaScriptInstallerResource_InfersNodejsIcon()
	{
		// Arrange
		// Regression: JavaScriptInstallerResource (the real type behind .WithPnpm() in
		// Aspire.Hosting.JavaScript) was returning tech:java.  CamelCase-splitting
		// "JavaScriptInstallerResource" yields "java" + "script", and the lone "java" token
		// exactly matched tech:java at score 0.5.
		// Fix: the tokeniser now merges adjacent "java"+"script" bigrams into "javascript"
		// and a TokenAlias redirects "javascript" → "node", scoring tech:nodejs instead.
		// Using a neutral resource name ("js-tools") so the type-name path is the primary signal.
		JavaScriptInstallerResource resource = new("js-tools");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_PnpmInstallerResource_InfersPnpmIcon()
	{
		// Arrange
		// Best-overall scoring: when the resource name identifies a specific package manager
		// (e.g. "pnpm-installer"), its exact match (score 1.0) correctly beats the generic
		// type-name inference ("nodejs" at 0.533).  The result is more accurate — a pnpm
		// installer really should show the pnpm icon.
		JavaScriptInstallerResource resource = new("pnpm-installer");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:pnpm");
	}

	[Test]
	public async Task Build_JavaApplicationResource_StillInfersJavaIcon()
	{
		// Arrange
		// Regression guard: the "java"+"script" bigram merge must not affect genuine Java
		// resources where "java" appears without a following "script" token.
		TestJavaAppResource resource = new("java-app");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot { ResourceType = "JavaApplication", Properties = [] }
			)
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:java");
	}

	[Test]
	public async Task Build_AzurePostgresRunAsContainer_InfersAzurePostgresIcon()
	{
		// Arrange
		// Regression: "azure-postgres" container (from RunAsContainer()) was matching tech:postgresql
		// instead of an azure icon. The hidden Azure resource's type name provides additional query
		// tokens ("flexible", "server") that allow the matcher to score the correct azure icon.
		var visibleContainer = CreateContainerResource("azure-postgres");
		visibleContainer.Annotations.Add(new ContainerImageAnnotation { Image = "library/postgres" });

		AzurePostgresFlexibleServerResource hiddenAzureResource = new("azure-postgres");
		hiddenAzureResource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "AzurePostgresFlexibleServer",
					IsHidden = true,
					Properties = [],
				}
			)
		);

		// Act
		var model = ModelBuilder.Build([visibleContainer, hiddenAzureResource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-database-postgre-sql-server");
	}

	[Test]
	public async Task Build_AzureManagedRedisRunAsContainer_InfersAzureManagedRedisIcon()
	{
		// Arrange
		// Same pattern as Azure Postgres: visible ContainerResource backed by a hidden Azure resource.
		// The hidden resource's type name ("AzureManagedRedisResource") contributes "managed" and "redis"
		// as query tokens, scoring a perfect match against "azure-managed-redis".
		var visibleContainer = CreateContainerResource("azure-redis");
		visibleContainer.Annotations.Add(new ContainerImageAnnotation { Image = "library/redis" });

		AzureManagedRedisResource hiddenAzureResource = new("azure-redis");
		hiddenAzureResource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "AzureManagedRedis",
					IsHidden = true,
					Properties = [],
				}
			)
		);

		// Act
		var model = ModelBuilder.Build([visibleContainer, hiddenAzureResource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_NodeAppExecutable_InfersNodejsIcon()
	{
		// Arrange
		// Regression: "node-app" was matching "node-sass" because both "node" and "sass" (unmatched)
		// gave a lower score than the corrected unmatched-token penalty.
		// After stop tokens strip "app", query is ["node"]; "nodejs" wins at 0.533 over "node-sass" at 0.5.
		ExecutableResource resource = new("node-app", "node", ".");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_WithProjectAutoIconsDisabled_DoesNotInferIcon()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], autoIconsEnabled: false);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsNull();
	}

	[Test]
	public async Task Build_WithPerResourceAutoIconDisabled_DoesNotInferIcon()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API")
				.WithTechnology(".NET")
				.WithDescription("HTTP API")
				.WithAutoIcon(false)
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsNull();
	}

	[Test]
	public async Task Build_WithPerResourceAutoIconEnabled_OverridesProjectSetting()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API").WithTechnology(".NET").WithDescription("HTTP API").WithAutoIcon(true)
		);

		// Act
		var model = ModelBuilder.Build([resource], autoIconsEnabled: false);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_ExplicitIcon_IsUsedWhenAutoIconsDisabled()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API")
				.WithTechnology(".NET")
				.WithDescription("HTTP API")
				.WithIcon("bootstrap:gear")
				.WithAutoIcon(false)
		);

		// Act
		var model = ModelBuilder.Build([resource], autoIconsEnabled: false);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("bootstrap:gear");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_OverridesAutoInference()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		static string? Resolver(IconResolverContext ctx) => ctx.Resource is ProjectResource ? "tech:dotnet" : null;

		// Act
		var model = ModelBuilder.Build([resource], iconResolvers: [Resolver]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_NullResultFallsBackToAutoInference()
	{
		// Arrange
		var resource = CreateContainerResource("redis");

		// Resolver explicitly declines; auto-inference should still pick tech:redis.
		static string? Resolver(IconResolverContext _) => null;

		// Act
		var model = ModelBuilder.Build([resource], iconResolvers: [Resolver]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_ContextExposesHiddenOriginal()
	{
		// Arrange
		IResource? capturedHidden = null;

		string? Resolver(IconResolverContext ctx)
		{
			capturedHidden = ctx.HiddenOriginal;
			return null;
		}

		var visibleContainer = CreateContainerResource("azure-redis");
		AzureManagedRedisResource hiddenAzureResource = new("azure-redis");
		ResourceSnapshotAnnotation snapshot = new(
			new CustomResourceSnapshot
			{
				ResourceType = "AzureManagedRedisResource",
				Properties = [],
				IsHidden = true,
			}
		);
		hiddenAzureResource.Annotations.Add(snapshot);

		// Act
		ModelBuilder.Build([hiddenAzureResource, visibleContainer], iconResolvers: [Resolver]);

		// Assert
		await Assert.That(capturedHidden).IsTypeOf<AzureManagedRedisResource>();
	}

	[Test]
	public async Task Build_WithMultipleCustomIconResolvers_FirstNonNullWins()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		List<int> callOrder = [];

		string? First(IconResolverContext _)
		{
			callOrder.Add(1);
			return null;
		}

		string? Second(IconResolverContext _)
		{
			callOrder.Add(2);
			return "tech:custom";
		}

		string? Third(IconResolverContext _)
		{
			callOrder.Add(3);
			return "tech:should-not-reach";
		}

		// Act
		var model = ModelBuilder.Build([resource], iconResolvers: [First, Second, Third]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:custom");
		await Assert.That(callOrder).IsEquivalentTo([1, 2]);
	}

	[Test]
	public async Task Build_NodeAnnotation_WithHashPrefixedTag_NormalizesTagInModel()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithTag("#external"));

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Tags).Contains("external");
		await Assert.That(model.Elements[0].Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task Build_ResourceRelationshipAnnotation_CreatesRelationship()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestDatabaseResource db = new("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		var rel = model.Relationships[0];
		await Assert.That(rel.SourceName).IsEqualTo("api");
		await Assert.That(rel.TargetName).IsEqualTo("db");
	}

	[Test]
	public async Task Build_WaitForRelationship_IsSkipped()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestDatabaseResource db = new("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "WaitFor"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_DuplicateRelationship_IsDeduped()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestDatabaseResource db = new("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Build_RelationshipToExcludedTarget_IsSkipped()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestSystemResource hidden = new("infra");
		hidden.Annotations.Add(new ExcludeFromLikeC4Annotation());
		api.Annotations.Add(new ResourceRelationshipAnnotation(hidden, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, hidden]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ResourceWithParent_SetsParentName()
	{
		// Arrange
		var parent = CreateContainerResource("postgres");
		TestChildResource child = new("postgres-db", parent);

		// Act
		var model = ModelBuilder.Build([parent, child]);

		var childElement = model.Elements.Single(e => e.Name == "postgres-db");
		// Assert
		await Assert.That(childElement.ParentName).IsEqualTo("postgres");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_OverridesLabel()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("Reads from"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Reads from");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsNavigateTo()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithNavigateTo("db-detail-flow"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].NavigateTo).IsEqualTo("db-detail-flow");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsTechnologyAndDescription()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("db")
				.WithTechnology("PostgreSQL")
				.WithDescription("Stores user records")
		);

		// Act
		var model = ModelBuilder.Build([api, db]);

		var rel = model.Relationships[0];
		// Assert
		await Assert.That(rel.Technology).IsEqualTo("PostgreSQL");
		await Assert.That(rel.Description).IsEqualTo("Stores user records");
		await Assert.That(rel.Label).IsNull();
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_MatchesSurrogateName()
	{
		// Arrange
		// The WithLikeC4Reference target name is the hidden Azure resource name;
		// the effective target is the surrogate container — both share the same name "redis".
		TestSystemResource hiddenAzureRedis = new("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var visibleContainerRedis = CreateContainerResource("redis");

		ExecutableResource nodeApp = new("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "Reference"));
		nodeApp.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("redis")
				.WithLabel("Caches sessions")
				.WithTechnology("Redis Protocol")
		);

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		var rel = model.Relationships[0];
		// Assert
		await Assert.That(rel.Label).IsEqualTo("Caches sessions");
		await Assert.That(rel.Technology).IsEqualTo("Redis Protocol");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_LastAnnotationWins()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("First"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("Last"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Last");
	}

	[Test]
	public async Task Build_NonReferenceRelationshipType_SetsLabel()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestSystemResource queue = new("queue");
		api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Publishes"));

		// Act
		var model = ModelBuilder.Build([api, queue]);

		// Assert
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Publishes");
	}

	[Test]
	public async Task Build_ReferenceToHiddenResourceWithVisibleSurrogate_ResolvesViaSurrogateName()
	{
		// Arrange
		// Simulates AddAzureManagedRedis("redis").RunAsContainer():
		// - The original Azure resource is hidden (IsHidden = true)
		// - A container surrogate with the same name "redis" is visible
		// - The node-app's WithReference points to the hidden Azure resource
		TestSystemResource hiddenAzureRedis = new("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var visibleContainerRedis = CreateContainerResource("redis");

		ExecutableResource nodeApp = new("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "Reference"));

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].SourceName).IsEqualTo("node-app");
		await Assert.That(model.Relationships[0].TargetName).IsEqualTo("redis");
	}

	[Test]
	public async Task Build_WaitForToHiddenSurrogate_IsSkipped()
	{
		// Arrange
		// WaitFor relationships to a hidden Azure resource should still be skipped,
		// not accidentally resolved and included via the surrogate.
		TestSystemResource hiddenAzureRedis = new("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);
		var visibleContainerRedis = CreateContainerResource("redis");

		ExecutableResource nodeApp = new("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "WaitFor"));

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ReferenceToHiddenResourceWithNoSurrogate_IsSkipped()
	{
		// Arrange
		// If the Azure resource is hidden and there is no visible surrogate with the same
		// name, the relationship should be dropped entirely (not produce a broken edge).
		TestSystemResource hiddenResource = new("orphaned-azure-resource");
		hiddenResource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Something",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		ExecutableResource nodeApp = new("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenResource, "Reference"));

		// Act
		var model = ModelBuilder.Build([hiddenResource, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_MultipleReferencesToSurrogateSameName_DeduplicatesRelationship()
	{
		// Arrange
		// Both a WaitFor (hidden) and a Reference (hidden) to the same Azure resource should
		// produce exactly one relationship to the surrogate (WaitFor is filtered; Reference resolves).
		TestSystemResource hiddenAzureRedis = new("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);
		var visibleContainerRedis = CreateContainerResource("redis");

		ExecutableResource nodeApp = new("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "Reference"));
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "WaitFor"));

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].SourceName).IsEqualTo("node-app");
		await Assert.That(model.Relationships[0].TargetName).IsEqualTo("redis");
	}

	[Test]
	public async Task Build_WithResourceStates_ElementsReflectStates()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		Dictionary<string, string?> states = new(StringComparer.OrdinalIgnoreCase)
		{
			["api"] = KnownResourceStates.Running,
			["db"] = KnownResourceStates.FailedToStart,
		};

		// Act
		var model = ModelBuilder.Build([api, db], states);

		var apiElement = model.Elements.Single(e => e.Name == "api");
		var dbElement = model.Elements.Single(e => e.Name == "db");
		// Assert
		await Assert.That(apiElement.State).IsEqualTo(KnownResourceStates.Running);
		await Assert.That(dbElement.State).IsEqualTo(KnownResourceStates.FailedToStart);
	}

	[Test]
	public async Task Build_WithNoResourceStates_ElementsDefaultToNull()
	{
		// Arrange
		var api = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([api]);

		// Assert
		await Assert.That(model.Elements[0].State).IsNull();
	}

	[Test]
	public async Task Build_WithPartialResourceStates_NullForMissingEntries()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		Dictionary<string, string?> states = new(StringComparer.OrdinalIgnoreCase)
		{
			["api"] = KnownResourceStates.Starting,
		};

		// Act
		var model = ModelBuilder.Build([api, db], states);

		var dbElement = model.Elements.Single(e => e.Name == "db");
		// Assert
		await Assert.That(dbElement.State).IsNull();
	}

	[Test]
	public async Task GetVisibleResourceNames_ExcludesHiddenAndAnnotatedResources()
	{
		// Arrange
		var visible = CreateProjectResource("api");

		TestSystemResource hidden = new("infra");
		hidden.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Internal",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var excluded = CreateContainerResource("excluded");
		excluded.Annotations.Add(new ExcludeFromLikeC4Annotation());

		var names = ModelBuilder.GetVisibleResourceNames([visible, hidden, excluded]);

		// Act
		// Assert
		await Assert.That(names).Contains("api");
		await Assert.That(names).DoesNotContain("infra");
		await Assert.That(names).DoesNotContain("excluded");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_IsEmittedWithoutResourceRelationshipAnnotation()
	{
		// Arrange
		// Simulates postgres.WithLikeC4Reference(azurePostgres, opts => ...) where no
		// WithReference was called — a purely diagram-level relationship.
		var local = CreateContainerResource("postgres");
		var azure = CreateContainerResource("azure-postgres");
		local.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("azure-postgres")
				.WithLabel("syncs with")
				.WithTechnology("PostgreSQL / JDBC")
		);

		// Act
		var model = ModelBuilder.Build([local, azure]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		var rel = model.Relationships[0];
		await Assert.That(rel.SourceName).IsEqualTo("postgres");
		await Assert.That(rel.TargetName).IsEqualTo("azure-postgres");
		await Assert.That(rel.Label).IsEqualTo("syncs with");
		await Assert.That(rel.Technology).IsEqualTo("PostgreSQL / JDBC");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_IsDeduplicatedWhenResourceRelationshipAlsoPresent()
	{
		// Arrange
		// If WithReference AND WithLikeC4Reference are both called, only one edge should appear.
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("Queries").WithTechnology("SQL"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Queries");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsKind()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestSystemResource queue = new("queue");
		api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("queue").WithKind("async"));

		// Act
		var model = ModelBuilder.Build([api, queue]);

		// Assert
		await Assert.That(model.Relationships[0].Kind).IsEqualTo("async");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_PropagatesKind()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestSystemResource queue = new("queue");
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("queue")
				.WithLabel("Publishes")
				.WithTechnology("AMQP")
				.WithKind("async")
		);

		// Act
		var model = ModelBuilder.Build([api, queue]);

		// Assert
		await Assert.That(model.Relationships[0].Kind).IsEqualTo("async");
	}

	[Test]
	public async Task Build_RelationshipWithNoKind_KindIsNull()
	{
		// Arrange
		var api = CreateProjectResource("api");
		TestDatabaseResource db = new("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].Kind).IsNull();
	}

	[Test]
	public async Task Build_AwsLambdaResource_InfersAwsIcon()
	{
		// Arrange
		TestSystemResource resource = new("fn");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "AWS.Lambda", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("aws:lambda");
	}

	[Test]
	public async Task Build_GcpPubSubResource_InfersGcpIcon()
	{
		// Arrange
		TestSystemResource resource = new("queue");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "GCP.PubSub", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("gcp:pub-sub");
	}

	[Test]
	public async Task Build_RabbitMqContainer_InfersTechIcon()
	{
		// Arrange
		var resource = CreateContainerResource("rabbitmq");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:rabbitmq");
	}

	[Test]
	public async Task Build_MongoDbContainer_InfersTechIcon()
	{
		// Arrange
		var resource = CreateContainerResource("mongodb");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mongodb");
	}

	[Test]
	public async Task Build_ResourceWithDSLIdAnnotation_UsesAnnotationIdAsElementName()
	{
		// Arrange
		var resource = CreateContainerResource("aspirec4-server");
		resource.Annotations.Add(new LikeC4DSLIdAnnotation("aspirec4"));

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Name).IsEqualTo("aspirec4");
	}

	[Test]
	public async Task Build_ResourceWithDSLIdAnnotation_UsesAnnotationIdInRelationshipSourceName()
	{
		// Arrange
		var server = CreateContainerResource("aspirec4-server");
		server.Annotations.Add(new LikeC4DSLIdAnnotation("aspirec4"));

		var api = CreateProjectResource("api");
		api.Annotations.Add(new ResourceRelationshipAnnotation(server, "Reference"));

		// Act
		var model = ModelBuilder.Build([server, api]);

		// Assert
		var rel = model.Relationships.Single(r => r.TargetName == "aspirec4");
		await Assert.That(rel.SourceName).IsEqualTo("api");
		await Assert.That(rel.TargetName).IsEqualTo("aspirec4");
	}

	[Test]
	public async Task Build_ResourceWithDSLIdAnnotation_UsesAnnotationIdInRelationshipTargetName()
	{
		// Arrange
		var server = CreateContainerResource("aspirec4-server");
		server.Annotations.Add(new LikeC4DSLIdAnnotation("aspirec4"));

		var api = CreateProjectResource("api");
		server.Annotations.Add(new ResourceRelationshipAnnotation(api, "Reference"));

		// Act
		var model = ModelBuilder.Build([server, api]);

		// Assert
		var rel = model.Relationships.Single(r => r.SourceName == "aspirec4");
		await Assert.That(rel.SourceName).IsEqualTo("aspirec4");
		await Assert.That(rel.TargetName).IsEqualTo("api");
	}

	[Test]
	public async Task Build_ResourceWithoutDSLIdAnnotation_UsesResourceNameAsElementName()
	{
		// Arrange
		var resource = CreateContainerResource("myservice");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Name).IsEqualTo("myservice");
	}
}
