var builder = DistributedApplication.CreateBuilder(args);

// Add LikeC4 visualization to the application. This will allow us to visualize the components and their relationships in a C4 model.
var c4 = builder.AddAspireC4(configure: static opts =>
	opts.WithTitle("AspireC4 Test App")
		.WithViewTitle("AspireC4 Architecture")
		.WithViewDescription(
			@"
This **LikeC4** view was automatically generated from the **Aspire** resource graph, using the **AspireC4** hosting extension.

For more details on all of these tools and components, see:

- [Aspire](https://aspire.dev/)
- [LikeC4](https://likec4.dev/)
- [AspireC4](https://kjl.dev/projects/aspirec4/)
"
		)
);

// Allow Dockerfile-based test environments (e.g. Dockerfile.e2e-npm) to switch the LikeC4
// server to a local JavaScript package manager CLI instead of the Docker container.
var cliRuntimeStr = Environment.GetEnvironmentVariable("ASPIREC4_CLI_RUNTIME");
if (cliRuntimeStr is not null && Enum.TryParse<LocalCLIRuntime>(cliRuntimeStr, ignoreCase: true, out var cliRuntime))
	c4.WithLocalCLI(cliRuntime);

// This is to configure certain parts of the AppHost and AspireC4 purely for this example test app.
c4.ConfigureAspireC4TestHost();

// Azure managed resources (containers when local).
var azureManagerRedis = builder
	.AddAzureManagedRedis("azure-redis")
	// Run as container when local
	.RunAsContainer(c =>
		c.WithRedisCommander(c =>
			c.WithLikeC4Details(options =>
				options.WithLabel("Redis Commander").WithSummary("Local Redis Web Interface")
			)
		)
	)
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(opts =>
		opts.WithLabel("Azure Redis")
			.WithTechnology("Azure Redis")
			.WithDescription(
				@"A **Managed Azure** Redis instance allowing fast access to previously cached data and values.

Used with the **Cache Aside** pattern, where the application can check Redis for cached data before falling back to the primary data store (Postgres in this case).

Cache usage will be non-critical and short-lived, ideal for session caching or caching frequently accessed data that doesn't require strong consistency.

Callers must:

- Assume cache is empty
- Populate with a TTL (Time To Live) to prevent stale data
- Ensure keys follow the pattern: `{service}:{key}`
"
			)
			.WithSummary("Short term caching, used for cross-instance caching")
			.WithLink(
				"https://learn.microsoft.com/azure/azure-cache-for-redis/cache-overview",
				"Learn more about Azure Redis"
			)
			.WithLink("https://azure.com/", "Learn more about Azure")
			.WithLink("https://redis.io/", "Learn more about Redis")
	);

var azurePostgres = builder
	.AddAzurePostgresFlexibleServer("azure-postgres")
	// Run as container when local
	.RunAsContainer(c =>
		c.WithPgWeb(pC =>
			pC.WithLikeC4Details(options => options.WithLabel("PgWeb").WithSummary("Local Postgres Web Interface"))
		)
	)
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(static c4 =>
		c4.WithLabel("Azure Postgres")
			.WithSummary("Azure Managed Postgres Flexible Server")
			.WithDescription("An **Azure Managed** Postgres instance for testing")
			.WithLink(
				"https://learn.microsoft.com/azure/postgresql/flexible-server/overview",
				"Learn more about Azure Postgres Flexible Server"
			)
			.WithLink("https://www.postgresql.org/", "Learn more about Postgres")
			.WithLink("https://azure.com/", "Learn more about Azure")
			.WithMetadata([
				("Azure SKU", "Flexible Server x 1 (NON-PROD)"),
				("Azure SKU", "Flexible Server x 2 (PROD)"),
				("Use Case", "Primary data store"),
			])
	);

// Local Dev/ Sync versions...
var localRedis = builder
	.AddRedis("local-redis")
	.WithLikeC4Details(opts =>
		opts.WithDescription(
				@"For testing **locally**, uses Redis as a container.

When using Azure Managed Redis with `.RunAsContainer()`, the application will differenciate between that and a real Redis resource using `.AddRedis(...)` and pick the correct icon/ technology."
			)
			.WithSummary("Local redis for development")
			.WithLink("https://redis.io/", "Learn more about Redis")
			.WithTag("local-dev")
	)
	.WithLikeC4Group(AppLikeC4Registry.Groups.LocalDevSyncGroup);

var localPostgres = builder
	.AddPostgres("local-postgres")
	.WithLikeC4Details(opts =>
		opts.WithDescription("For testing Azure Postgres vs. local Postgres")
			.WithSummary("Local Postgres for development")
			.WithLink("https://www.postgresql.org/", "Learn more about Postgres")
			// Naming this something else will create a diagnostic 'suggestion' as it's not in the AppLikeC4Registry
			// ... you can always use AppLikeC4Registry.Tags.LocalDev to prevent typos.
			.WithTag("local-dev")
	)
	.WithLikeC4Group("Local Dev/ Sync Group");

// Our app...
var nodeApp = builder
	.AddNodeApp("node-app", "../../../samples/node-app", "index.ts")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(options =>
		options
			.WithLabel("Sample Node App")
			.WithDescription("A sample Node.js application that connects to Azure Redis and Azure Postgres")
	)
	.WithNpm(install: true)
	.WithHttpEndpoint(env: "PORT")
	.WithUrlForEndpoint("http", url => url.Url = "/health")
	// These references will be used to generate the connections in the C4 model,
	// _and_ ensure that the application has the connection strings for these dependencies by internally
	// calling `.WithReference(...)`.
	.WithLikeC4Reference(
		azureManagerRedis,
		opts => opts.WithLabel("Caches sessions").WithTechnology("Redis Protocol").WithKind("RESP")
	)
	.WithLikeC4Reference(
		localRedis,
		opts => opts.WithLabel("Caches  sessions (local)").WithTechnology("Redis Protocol").WithKind("RESP")
	)
	.WithLikeC4Reference(
		azurePostgres,
		opts => opts.WithLabel("Persists data").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
	)
	.WithLikeC4Reference(
		localPostgres,
		opts => opts.WithLabel("Persists data (local)").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
	)
	.WaitFor(azureManagerRedis)
	.WaitFor(localRedis)
	.WaitFor(azurePostgres)
	.WaitFor(localPostgres);

localPostgres.WithLikeC4Reference(
	azurePostgres,
	opts => opts.WithLabel("syncs with").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
);
localRedis.WithLikeC4Reference(
	azureManagerRedis,
	opts => opts.WithLabel("syncs with").WithTechnology("Redis Protocol").WithKind("RESP")
);

// Test adding some parameters , including one from configuration.
// These should be automatically hidden from the LikeC4 model as they are not relevant to the architecture and would just add noise to the visualisation.
var testingParam = builder
	.AddParameter("testing-resource-parameter", "This should be hidden from the LikeC4 model", false, false)
	.WithDescription("This should be hidden from the LikeC4 model");
var testParamFromConfig = builder
	.AddParameterFromConfiguration("testing-resource-parameter-from-config", "DOTNET_ENVIRONMENT", false)
	.WithDescription("This should be hidden from the LikeC4 model");

testingParam.WithReferenceRelationship(nodeApp);
testParamFromConfig.WithReferenceRelationship(nodeApp);

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
