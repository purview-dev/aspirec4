import fs from "node:fs";
import path from "node:path";
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// Add LikeC4 visualization to the application. This will allow us to visualize the components and their relationships in a C4 model.
await builder
	.addAspireC4({
		configure: async (opts) => {
			opts
				.withFormatGeneratedFile({ format: false })
				.withTitle("AspireC4 Test App")
				.withViewTitle("AspireC4 Architecture")
				.withViewDescription(`This **LikeC4** view was automatically generated from the **Aspire** resource graph, using the **AspireC4** hosting extension.

		For more details on all of these tools and components, see:

		- [Aspire](https://aspire.dev/)
		- [LikeC4](https://likec4.dev/)
		- [AspireC4](https://kjl.dev/projects/aspirec4/)`);

			const assetsPath = path.join(process.cwd(), "../../assets/");
			const imagesPath = path.join(assetsPath, "images/");
			const dslPath = path.join(assetsPath, "likec4-extensions/");

			if (fs.existsSync(imagesPath)) {
				opts.withImageAliasFolder("@", imagesPath);
			}

			if (fs.existsSync(dslPath)) {
				opts.withAdditionalDSLFolder(dslPath);
			}

			// Just for the sake of this demo, we'll include the AspireC4 internal resource in the diagram
			// as we reference it from the additional DSLs.
			opts.withIncludeAspireC4InternalResource(true);
		},
	})
	.configureServer(async (resource) => {
		resource.withLikeC4Details({
			configure: async (opts) => {
				opts.withLabel("AspireC4")
					.withSummary(
						"Describe your Aspire orchestration as a live LikeC4 system architecture diagram - auto generated",
					)
					.withIcon("@/likec4/likec4-logo.svg")
					.withLinkNode("https://kjl.dev/projects/aspirec4", {
						title: "Learn more about AspireC4",
					})
					.withLinkNode("https://github.com/kjldev/aspirec4/", {
						title: "AspireC4 on GitHub",
					})
					.withLinkNode("https://github.com/kieronlanning", {
						title: "Connect with the author on GitHub",
					});
			},
		});
	});

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
				.withLinkNode("https://learn.microsoft.com/azure/azure-cache-for-redis/cache-overview", {
					title: "Learn more about Azure Redis",
				})
				.withLinkNode("https://azure.com/", { title: "Learn more about Azure" })
				.withLinkNode("https://redis.io/", { title: "Learn more about Redis" });
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
				.withDescription(`An **Azure Managed** Postgres instance for testing`)
				.withSummary("Azure Managed Postgres Flexible Server")
				.withLinkNode("https://learn.microsoft.com/azure/postgresql/flexible-server/overview", {
					title: "Learn more about Azure Postgres Flexible Server",
				})
				.withLinkNode("https://www.postgresql.org/", {
					title: "Learn more about Postgres",
				})
				.withLinkNode("https://azure.com/", { title: "Learn more about Azure" })
				.withMetadataNode("Azure SKU", "Flexible Server x 1 (NON-PROD)")
				.withMetadataNode("Azure SKU", "Flexible Server x 2 (PROD)")
				.withMetadataNode("Use Case", "Primary data store");
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
				.withLinkNode("https://redis.io/", { title: "Learn more about Redis" })
				.withTag("local-dev");
		},
	})
	.withLikeC4Group("Local Dev/ Sync Group");

const localPostgres = await builder
	.addPostgres("local-postgres")
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withDescription(`For testing Azure Postgres vs. local Postgres`)
				.withSummary("Local Postgres for development")
				.withLinkNode("https://www.postgresql.org/", {
					title: "Learn more about Postgres",
				})
				.withTag("local-dev");
		},
	})
	.withLikeC4Group("Local Dev/ Sync Group");

// Our app...
const _nodeApp = await builder
	.addNodeApp("node-app", "../node-app/", "index.ts")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		configure: async (opts) => {
			opts.withLabel("Sample Node App").withDescription(
				"A sample Node.js application that connects to Azure Redis and Azure Postgres",
			);
		},
	})
	.withBun({ install: true })
	.withHttpEndpoint({ env: "PORT" })
	.withUrlForEndpoint("http", async (url) => {
		url.url = "/health";
	})
	// These references will be used to generate the connections in the C4 model and also ensure that the application waits for these dependencies to be ready before starting.
	.withLikeC4ReferenceWithEnvironmentResource(azureManagerRedis, {
		configure: async (opts) => {
			await opts.withLabel("Caches sessions").withTechnology("Redis Protocol").withKind("RESP");
		},
	})
	.waitFor(azureManagerRedis)
	.withLikeC4ReferenceWithEnvironmentResource(localRedis, {
		configure: async (opts) => {
			await opts.withLabel("Caches  sessions (local)").withTechnology("Redis Protocol").withKind("RESP");
		},
	})
	.waitFor(localRedis)
	.withLikeC4ReferenceWithEnvironmentResource(azurePostgres, {
		configure: async (opts) => {
			await opts.withLabel("Persists data").withTechnology("PostgreSQL / JDBC").withKind("tcp-ip");
		},
	})
	.waitFor(azurePostgres)
	.withLikeC4ReferenceWithEnvironmentResource(localPostgres, {
		configure: async (opts) => {
			await opts.withLabel("Persists data (local)").withTechnology("PostgreSQL / JDBC").withKind("tcp-ip");
		},
	})
	.waitFor(localPostgres);

await localPostgres.withLikeC4ReferenceWithEnvironmentResource(azurePostgres, {
	configure: async (opts) => {
		await opts.withLabel("syncs with").withTechnology("PostgreSQL / JDBC").withKind("tcp-ip");
	},
});
await localRedis.withLikeC4ReferenceWithEnvironmentResource(azureManagerRedis, {
	configure: async (opts) => {
		await opts.withLabel("syncs with").withTechnology("Redis Protocol").withKind("RESP");
	},
});

const app = await builder.build();
await app.run();
