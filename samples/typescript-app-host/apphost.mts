// AspireC4 TypeScript AppHost sample. It builds the same resource graph as the C# sample AppHost
// (src/src/TestAppHost) and the Go and Python samples, and the shared AppHost contract tests assert
// that all of them draw the same architecture diagram. Keep the samples in step.
import fs from "node:fs";
import path from "node:path";
import { createBuilder, LocalCLIRuntime } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const localDevGroup = "Local Dev/ Sync Group";

// Add LikeC4 visualization to the application. This will allow us to visualize the components and their relationships in a C4 model.
const c4 = builder
	.addAspireC4({
		configure: async (opts) => {
			opts.withTitle("AspireC4 Test App").withViewTitle("AspireC4 Architecture")
				.withViewDescription(`This **LikeC4** view was automatically generated from the **Aspire** resource graph, using the **AspireC4** hosting extension.

For more details on all of these tools and components, see:

- [Aspire](https://aspire.dev/)
- [LikeC4](https://likec4.dev/)
- [AspireC4](https://purview.dev/projects/aspirec4/)`);

			const assetsPath = path.join(process.cwd(), "../../assets/");
			const imagesPath = path.join(assetsPath, "images/");
			const dslPath = path.join(assetsPath, "likec4-extensions/");

			if (fs.existsSync(dslPath)) {
				opts.withAdditionalDSLFolder(dslPath);
			}

			if (fs.existsSync(imagesPath)) {
				opts.withImageAliasFolder("@", imagesPath);
			}

			// Just for the sake of this demo, we'll include the AspireC4 internal resource in the diagram
			// as we reference it from the additional DSLs.
			opts.withIncludeAspireC4InternalResource(true);

			// Formatting runs `npx likec4 format`, which scans this whole repository when the sample runs
			// from inside it. Every sample AppHost turns it off for the same reason.
			opts.withFormatGeneratedFile({ format: false });
		},
	})
	// We're adding LikeC4 pazzazz to the LikeC4 server resource for this demo.
	.withServerLikeC4Details({
		configure: async (opts) => {
			opts.withLabel("AspireC4")
				.withSummary(
					"Describe your Aspire orchestration as a live LikeC4 system architecture diagram - auto generated",
				)
				.withIcon("@/likec4/likec4-logo.svg")
				.withLink("https://purview.dev/projects/aspirec4", { title: "Learn more about AspireC4" })
				.withLink("https://github.com/purview-dev/aspirec4/", { title: "AspireC4 on GitHub" })
				.withLink("https://github.com/kieronlanning", { title: "Connect with the author on GitHub" });
		},
	});

// Allow Dockerfile-based test environments to switch the LikeC4 server to a local JavaScript package
// manager CLI instead of the Docker container, as the C# sample does.
const cliRuntime = parseCliRuntime(process.env["ASPIREC4_CLI_RUNTIME"]);
if (cliRuntime !== undefined) {
	await c4.withLocalCLI({ runtime: cliRuntime });
}

// Adds the d2 ER diagram plus the .NET service that owns the sample Entity Framework Core models. The
// ER diagram is built from .NET EF Core code, so it always draws .NET projects, whatever the AppHost's
// language.
await c4.withEntityFrameworkDiagram({
	configure: async (opts) => {
		await opts.title.set("AspireC4 Sample Schema");
	},
});

await builder
	.addDotnetProject("data-service", "../../src/src/TestAppHost.DataService/TestAppHost.DataService.csproj")
	.withEntityFrameworkModel({ label: "Sample Data Service" })
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withLabel("Sample Data Service")
				.withDescription("Owns the simple and complex sample **Entity Framework Core** models.")
				.withSummary("Sample EF Core models");
		},
	});

// Azure managed resources (containers when local).
const azureManagerRedis = await builder
	.addAzureManagedRedis("azure-redis")
	// Run as container when local
	.runAsContainer({
		configureContainer: async (localRedisContainer) => {
			await localRedisContainer.withRedisCommander({
				configureContainer: async (commanderContainer) => {
					await commanderContainer.withLikeC4Details({
						configure: async (opts) => {
							opts.withLabel("Redis Commander").withSummary("Local Redis Web Interface");
						},
					});
				},
			});
		},
	})
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withLabel("Azure Redis")
				.withTechnology("Azure Redis")
				.withDescription(`A **Managed Azure** Redis instance allowing fast access to previously cached data and values.

Used with the **Cache Aside** pattern, where the application can check Redis for cached data before falling back to the primary data store (Postgres in this case).

Cache usage will be non-critical and short-lived, ideal for session caching or caching frequently accessed data that doesn't require strong consistency.

Callers must:

- Assume cache is empty
- Populate with a TTL (Time To Live) to prevent stale data
- Ensure keys follow the pattern: \`{service}:{key}\``)
				.withSummary("Short term caching, used for cross-instance caching")
				.withLink("https://learn.microsoft.com/azure/azure-cache-for-redis/cache-overview", {
					title: "Learn more about Azure Redis",
				})
				.withLink("https://azure.com/", { title: "Learn more about Azure" })
				.withLink("https://redis.io/", { title: "Learn more about Redis" });
		},
	});

const azurePostgres = await builder
	.addAzurePostgresFlexibleServer("azure-postgres")
	// Run as container when local
	.runAsContainer({
		configureContainer: async (localPostgresContainer) => {
			await localPostgresContainer.withPgWeb({
				configureContainer: async (pgWebContainer) => {
					await pgWebContainer.withLikeC4Details({
						configure: async (opts) => {
							opts.withLabel("PgWeb").withSummary("Local Postgres Web Interface");
						},
					});
				},
			});
		},
	})
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withLabel("Azure Postgres")
				.withSummary("Azure Managed Postgres Flexible Server")
				.withDescription("An **Azure Managed** Postgres instance for testing")
				.withLink("https://learn.microsoft.com/azure/postgresql/flexible-server/overview", {
					title: "Learn more about Azure Postgres Flexible Server",
				})
				.withLink("https://www.postgresql.org/", { title: "Learn more about Postgres" })
				.withLink("https://azure.com/", { title: "Learn more about Azure" })
				.withMetadata("Azure SKU", "Flexible Server x 1 (NON-PROD)")
				.withMetadata("Azure SKU", "Flexible Server x 2 (PROD)")
				.withMetadata("Use Case", "Primary data store");
		},
	});

// Local Dev/ Sync versions...
const localRedis = await builder
	.addRedis("local-redis")
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withDescription(`For testing **locally**, uses Redis as a container.

When using Azure Managed Redis with \`.RunAsContainer()\`, the application will differentiate between that and a real Redis resource using \`.AddRedis(...)\` and pick the correct icon/ technology.`)
				.withSummary("Local redis for development")
				.withLink("https://redis.io/", { title: "Learn more about Redis" })
				.withTag("local-dev");
		},
	})
	.withLikeC4Group(localDevGroup);

const localPostgres = await builder
	.addPostgres("local-postgres")
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withDescription("For testing Azure Postgres vs. local Postgres")
				.withSummary("Local Postgres for development")
				.withLink("https://www.postgresql.org/", { title: "Learn more about Postgres" })
				.withTag("local-dev");
		},
	})
	.withLikeC4Group(localDevGroup);

// Our app...
await builder
	.addNodeApp("node-app", "../node-app/", "index.ts")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withLabel("Sample Node App").withDescription(
				"A sample Node.js application that connects to Azure Redis and Azure Postgres",
			);
		},
	})
	.withNpm({ install: true })
	.withHttpEndpoint({ env: "PORT" })
	.withUrlForEndpoint("http", async (url) => {
		url.url = "/health";
	})
	// These references generate the connections in the C4 model, and give the application the
	// connection strings for these dependencies through Aspire's WithReference.
	.withLikeC4ConnectionReference(azureManagerRedis, {
		configure: async (opts) => {
			await opts.withLabel("Caches sessions").withTechnology("Redis Protocol").withKind("RESP");
		},
	})
	.withLikeC4ConnectionReference(localRedis, {
		configure: async (opts) => {
			await opts.withLabel("Caches  sessions (local)").withTechnology("Redis Protocol").withKind("RESP");
		},
	})
	.withLikeC4ConnectionReference(azurePostgres, {
		configure: async (opts) => {
			await opts.withLabel("Persists data").withTechnology("PostgreSQL / JDBC").withKind("tcp-ip");
		},
	})
	.withLikeC4ConnectionReference(localPostgres, {
		configure: async (opts) => {
			await opts.withLabel("Persists data (local)").withTechnology("PostgreSQL / JDBC").withKind("tcp-ip");
		},
	})
	.waitFor(azureManagerRedis)
	.waitFor(localRedis)
	.waitFor(azurePostgres)
	.waitFor(localPostgres);

await localPostgres.withLikeC4ConnectionReference(azurePostgres, {
	configure: async (opts) => {
		await opts.withLabel("syncs with").withTechnology("PostgreSQL / JDBC").withKind("tcp-ip");
	},
});
await localRedis.withLikeC4ConnectionReference(azureManagerRedis, {
	configure: async (opts) => {
		await opts.withLabel("syncs with").withTechnology("Redis Protocol").withKind("RESP");
	},
});

// Parameters are hidden from the LikeC4 model automatically: they are not architecture, and their values
// may be secrets.
await builder
	.addParameter("testing-resource-parameter", { value: "This should be hidden from the LikeC4 model" })
	.withDescription("This should be hidden from the LikeC4 model");
await builder
	.addParameterFromConfiguration("testing-resource-parameter-from-config", "DOTNET_ENVIRONMENT")
	.withDescription("This should be hidden from the LikeC4 model");

const app = await builder.build();
await app.run();

function parseCliRuntime(value: string | undefined): LocalCLIRuntime | undefined {
	if (!value) {
		return undefined;
	}

	return Object.values(LocalCLIRuntime).find((runtime) => runtime.toLowerCase() === value.toLowerCase());
}
