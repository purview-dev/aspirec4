using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

/// <summary>A technology recognised from a resource, with the LikeC4 icon that depicts it.</summary>
/// <param name="Name">The technology label shown on the element, e.g. <c>Azure Storage</c>.</param>
/// <param name="Icon">
/// The icon, e.g. <c>azure:storage-accounts</c>, or <see langword="null"/> for a technology LikeC4 has no
/// icon for. A recognised technology never falls back to fuzzy matching: a near miss — Seq drawn as
/// Sequelize — is worse than no icon.
/// </param>
/// <param name="IsOverride">
/// Whether a <see cref="TechnologyOverride"/> the user registered supplied it. An override's icon is
/// explicit, so it applies even with auto icons off, and an override without one has its icon matched
/// from <paramref name="Name"/> rather than left empty.
/// </param>
readonly record struct KnownTechnology(string Name, string? Icon, bool IsOverride = false);

/// <summary>
/// Recognises what a resource actually is — from its project's dependencies, its Aspire resource type, or
/// its container image — before the fuzzy icon matcher is left to guess from names.
/// </summary>
/// <remarks>
/// <para>
/// The fuzzy matcher scores name tokens against icon names, so it can only be as good as the names it is
/// given. <c>AzureStorageResource</c> offers just "storage", which scores Azure Storage Mover as well as a
/// storage account; a Vite app offers just "vite", whatever framework it builds. This catalogue holds the
/// answers for the resources Aspire and its community toolkit actually create, and is consulted first:
/// </para>
/// <list type="number">
///   <item><description>
///     the user's own <see cref="TechnologyOverride"/> rules, so anything below that gets a resource wrong
///     can be corrected once for the whole application;
///   </description></item>
///   <item><description>
///     an app's framework, read from its own project: <c>package.json</c>, <c>pyproject.toml</c> or
///     <c>requirements.txt</c> in a JavaScript or Python app's working directory — so an Astro site on
///     Vite reads as Astro — or a .NET project's SDK, packages and framework references, so an ASP.NET Core
///     service is not just ".NET". A package-manager installer is named by its package manager;
///   </description></item>
///   <item><description>
///     the resource's type, walking its base types, for the hidden original first — so an Azure resource
///     run locally is still the Azure service, labelled with what is running it (<c>Azurite</c>, an
///     emulator, or a local container);
///   </description></item>
///   <item><description>its container image.</description></item>
/// </list>
/// <para>
/// Anything unrecognised falls through to the fuzzy matcher exactly as before. Every icon here is checked
/// against the bundled manifest by a unit test, so refreshing the manifest cannot silently orphan one.
/// </para>
/// </remarks>
static class TechnologyCatalog
{
	// ── Resource types ─────────────────────────────────────────────────────────────────────────
	//
	// Keyed by the normalised type name (see TypeKey): the class name without its "Resource" suffix, or a
	// snapshot resource type with its dots removed — "AzureSqlServerResource" and "Azure.SqlServer" are
	// both "AzureSqlServer". Base types are walked, so a subclass of a known type is recognised too.

	static readonly Dictionary<string, KnownTechnology> ByType = new(StringComparer.OrdinalIgnoreCase)
	{
		// Azure: storage
		["AzureStorage"] = new("Azure Storage", "azure:storage-accounts"),
		["AzureStorageAccount"] = new("Azure Storage", "azure:storage-accounts"),
		["AzureBlobStorage"] = new("Azure Blob Storage", "azure:blob-block"),
		["AzureBlobStorageContainer"] = new("Azure Blob Storage", "azure:storage-container"),
		["AzureQueueStorage"] = new("Azure Queue Storage", "azure:storage-queue"),
		["AzureQueueStorageQueue"] = new("Azure Queue Storage", "azure:storage-queue"),
		["AzureTableStorage"] = new("Azure Table Storage", "azure:table"),
		["AzureDataLakeStorage"] = new("Azure Data Lake Storage", "azure:data-lake-storage-gen1"),
		["AzureFileShare"] = new("Azure Files", "azure:storage-azure-files"),
		// Azure: data
		["AzureCosmosDB"] = new("Azure Cosmos DB", "azure:azure-cosmos-db"),
		["AzureCosmosDBDatabase"] = new("Azure Cosmos DB", "azure:azure-cosmos-db"),
		["AzureCosmosDBContainer"] = new("Azure Cosmos DB", "azure:azure-cosmos-db"),
		["AzureSqlServer"] = new("Azure SQL", "azure:sql-server"),
		["AzureSqlDatabase"] = new("Azure SQL Database", "azure:sql-database"),
		["AzurePostgresFlexibleServer"] = new(
			"Azure Database for PostgreSQL",
			"azure:azure-database-postgre-sql-server"
		),
		["AzurePostgresFlexibleServerDatabase"] = new(
			"Azure Database for PostgreSQL",
			"azure:azure-database-postgre-sql-server"
		),
		["AzureMySqlFlexibleServer"] = new("Azure Database for MySQL", "azure:azure-database-my-sql-server"),
		["AzureManagedRedis"] = new("Azure Managed Redis", "azure:azure-managed-redis"),
		["AzureRedisCache"] = new("Azure Cache for Redis", "azure:cache-redis"),
		["AzureKustoCluster"] = new("Azure Data Explorer", "azure:azure-data-explorer-clusters"),
		["AzureKustoReadWriteDatabase"] = new("Azure Data Explorer", "azure:azure-data-explorer-clusters"),
		["AzureSearch"] = new("Azure AI Search", "azure:cognitive-search"),
		// Azure: messaging
		["AzureServiceBus"] = new("Azure Service Bus", "azure:azure-service-bus"),
		["AzureServiceBusQueue"] = new("Azure Service Bus", "azure:azure-service-bus"),
		["AzureServiceBusTopic"] = new("Azure Service Bus", "azure:azure-service-bus"),
		["AzureServiceBusSubscription"] = new("Azure Service Bus", "azure:azure-service-bus"),
		["AzureEventHubs"] = new("Azure Event Hubs", "azure:event-hubs"),
		["AzureEventHub"] = new("Azure Event Hubs", "azure:event-hubs"),
		["AzureEventHubConsumerGroup"] = new("Azure Event Hubs", "azure:event-hubs"),
		["AzureSignalR"] = new("Azure SignalR", "azure:signal-r"),
		["AzureWebPubSub"] = new("Azure Web PubSub", null),
		// Azure: AI
		["AzureOpenAI"] = new("Azure OpenAI", "azure:azure-open-ai"),
		["AzureOpenAIDeployment"] = new("Azure OpenAI", "azure:azure-open-ai"),
		["AzureAIFoundry"] = new("Azure AI Foundry", "azure:ai-studio"),
		["AzureAIFoundryDeployment"] = new("Azure AI Foundry", "azure:ai-studio"),
		// Azure: security, configuration and monitoring
		["AzureKeyVault"] = new("Azure Key Vault", "azure:key-vaults"),
		["AzureAppConfiguration"] = new("Azure App Configuration", "azure:app-configuration"),
		["AzureApplicationInsights"] = new("Application Insights", "azure:application-insights"),
		["AzureLogAnalyticsWorkspace"] = new("Log Analytics", "azure:log-analytics-workspaces"),
		["AzureUserAssignedIdentity"] = new("Managed Identity", "azure:managed-identities"),
		// Azure: hosting and networking
		["AzureFunctionsProject"] = new("Azure Functions", "azure:function-apps"),
		["AzureContainerAppEnvironment"] = new("Azure Container Apps", "azure:container-apps-environments"),
		["AzureAppServiceEnvironment"] = new("Azure App Service", "azure:app-services"),
		["AzureContainerRegistry"] = new("Azure Container Registry", "azure:container-registries"),
		["AzureVirtualNetwork"] = new("Azure Virtual Network", "azure:virtual-networks"),
		// Databases and caches
		["Redis"] = new("Redis", "tech:redis"),
		["Valkey"] = new("Valkey", null),
		["Garnet"] = new("Garnet", null),
		["PostgresServer"] = new("PostgreSQL", "tech:postgresql"),
		["PostgresDatabase"] = new("PostgreSQL", "tech:postgresql"),
		["SqlServerServer"] = new("SQL Server", "tech:microsoft-sql-server"),
		["SqlServerDatabase"] = new("SQL Server", "tech:microsoft-sql-server"),
		["MySqlServer"] = new("MySQL", "tech:mysql"),
		["MySqlDatabase"] = new("MySQL", "tech:mysql"),
		["MongoDBServer"] = new("MongoDB", "tech:mongodb"),
		["MongoDBDatabase"] = new("MongoDB", "tech:mongodb"),
		["OracleDatabaseServer"] = new("Oracle Database", "tech:oracle"),
		["OracleDatabase"] = new("Oracle Database", "tech:oracle"),
		["Elasticsearch"] = new("Elasticsearch", "tech:elasticsearch"),
		["QdrantServer"] = new("Qdrant", "tech:qdrant"),
		["MilvusServer"] = new("Milvus", "tech:milvus"),
		["MilvusDatabase"] = new("Milvus", "tech:milvus"),
		["Sqlite"] = new("SQLite", "tech:sqlite"),
		["Meilisearch"] = new("Meilisearch", "tech:meilisearch"),
		// Messaging
		["RabbitMQServer"] = new("RabbitMQ", "tech:rabbitmq"),
		["KafkaServer"] = new("Apache Kafka", "tech:kafka"),
		["NatsServer"] = new("NATS", "tech:nats"),
		// Observability, identity and infrastructure
		["Seq"] = new("Seq", null),
		["Keycloak"] = new("Keycloak", null),
		["Yarp"] = new("YARP", "tech:dotnet"),
		["Ollama"] = new("Ollama", null),
		["OllamaModel"] = new("Ollama", null),
		["MinioContainer"] = new("MinIO", null),
		["MailPitContainer"] = new("Mailpit", null),
		["DockerComposeEnvironment"] = new("Docker Compose", "tech:docker"),
		["KubernetesEnvironment"] = new("Kubernetes", "tech:kubernetes"),
		// Application runtimes. Frameworks read from a project's own files outrank these.
		["ViteApp"] = new("Vite", "tech:vite"),
		["NodeApp"] = new("Node.js", "tech:nodejs"),
		["JavaScriptApp"] = new("Node.js", "tech:nodejs"),
		["BunApp"] = new("Bun", "tech:bun"),
		["DenoApp"] = new("Deno", "tech:deno"),
		["PythonApp"] = new("Python", "tech:python"),
		["PythonProject"] = new("Python", "tech:python"),
		["UvicornApp"] = new("Python", "tech:python"),
		["GolangAppExecutable"] = new("Go", "tech:go"),
		["JavaAppExecutable"] = new("Java", "tech:java"),
		["JavaAppContainer"] = new("Java", "tech:java"),
		["RustAppExecutable"] = new("Rust", "tech:rust"),
	};

	// ── Container images ───────────────────────────────────────────────────────────────────────
	//
	// Matched against the image without its registry, "library/" namespace, tag or digest. A pattern with a
	// slash must match the whole path; one without matches the last segment, so "bitnami/redis" is Redis.
	// An emulator entry names what is doing the emulating, which labels an Azure resource run locally.

	static readonly (string Pattern, KnownTechnology Technology, string? Emulates)[] ByImage =
	[
		("azure-storage/azurite", new("Azure Storage", "azure:storage-accounts"), "Azurite"),
		("cosmosdb/linux/azure-cosmos-emulator", new("Azure Cosmos DB", "azure:azure-cosmos-db"), "emulator"),
		("cosmosdb/linux/azure-cosmos-emulator-vnext", new("Azure Cosmos DB", "azure:azure-cosmos-db"), "emulator"),
		("azure-messaging/servicebus-emulator", new("Azure Service Bus", "azure:azure-service-bus"), "emulator"),
		("azure-messaging/eventhubs-emulator", new("Azure Event Hubs", "azure:event-hubs"), "emulator"),
		("signalr/signalr-emulator", new("Azure SignalR", "azure:signal-r"), "emulator"),
		("azure-sql-edge", new("Azure SQL Edge", "azure:azure-sql-edge"), null),
		("mssql/server", new("SQL Server", "tech:microsoft-sql-server"), null),
		("mssql/rhel/server", new("SQL Server", "tech:microsoft-sql-server"), null),
		("postgres", new("PostgreSQL", "tech:postgresql"), null),
		("postgresql", new("PostgreSQL", "tech:postgresql"), null),
		("postgis/postgis", new("PostgreSQL (PostGIS)", "tech:postgresql"), null),
		("pgvector/pgvector", new("PostgreSQL (pgvector)", "tech:postgresql"), null),
		("redis", new("Redis", "tech:redis"), null),
		("redis/redis-stack", new("Redis Stack", "tech:redis"), null),
		("redis/redis-stack-server", new("Redis Stack", "tech:redis"), null),
		("redis/redisinsight", new("RedisInsight", "tech:redis"), null),
		("rediscommander/redis-commander", new("Redis Commander", "tech:redis"), null),
		("joeferner/redis-commander", new("Redis Commander", "tech:redis"), null),
		("valkey/valkey", new("Valkey", null), null),
		("microsoft/garnet", new("Garnet", null), null),
		("mysql", new("MySQL", "tech:mysql"), null),
		("mariadb", new("MariaDB", "tech:mariadb"), null),
		("mongo", new("MongoDB", "tech:mongodb"), null),
		("mongodb/mongodb-community-server", new("MongoDB", "tech:mongodb"), null),
		("mongo-express", new("Mongo Express", "tech:mongodb"), null),
		("rabbitmq", new("RabbitMQ", "tech:rabbitmq"), null),
		("confluentinc/confluent-local", new("Apache Kafka", "tech:kafka"), null),
		("confluentinc/cp-kafka", new("Apache Kafka", "tech:kafka"), null),
		("apache/kafka", new("Apache Kafka", "tech:kafka"), null),
		("kafka", new("Apache Kafka", "tech:kafka"), null),
		("nats", new("NATS", "tech:nats"), null),
		("elasticsearch/elasticsearch", new("Elasticsearch", "tech:elasticsearch"), null),
		("elasticsearch", new("Elasticsearch", "tech:elasticsearch"), null),
		("kibana/kibana", new("Kibana", "tech:kibana"), null),
		("qdrant/qdrant", new("Qdrant", "tech:qdrant"), null),
		("milvusdb/milvus", new("Milvus", "tech:milvus"), null),
		("getmeili/meilisearch", new("Meilisearch", "tech:meilisearch"), null),
		("typesense/typesense", new("Typesense", "tech:typesense"), null),
		("memcached", new("Memcached", "tech:memcached"), null),
		("cassandra", new("Apache Cassandra", "tech:cassandra"), null),
		("neo4j", new("Neo4j", "tech:neo4j"), null),
		("influxdb", new("InfluxDB", "tech:influxdb"), null),
		("gvenzl/oracle-free", new("Oracle Database", "tech:oracle"), null),
		("gvenzl/oracle-xe", new("Oracle Database", "tech:oracle"), null),
		("database/free", new("Oracle Database", "tech:oracle"), null),
		("dpage/pgadmin4", new("pgAdmin", "tech:postgresql"), null),
		("sosedoff/pgweb", new("pgweb", "tech:postgresql"), null),
		("ollama/ollama", new("Ollama", null), null),
		("keycloak/keycloak", new("Keycloak", null), null),
		("datalust/seq", new("Seq", null), null),
		("grafana/grafana", new("Grafana", "tech:grafana"), null),
		("grafana/otel-lgtm", new("Grafana LGTM", "tech:grafana"), null),
		("prom/prometheus", new("Prometheus", "tech:prometheus"), null),
		("jaegertracing/all-in-one", new("Jaeger", "tech:jaeger"), null),
		("jaegertracing/jaeger", new("Jaeger", "tech:jaeger"), null),
		("otel/opentelemetry-collector", new("OpenTelemetry Collector", "tech:opentelemetry-icon"), null),
		("otel/opentelemetry-collector-contrib", new("OpenTelemetry Collector", "tech:opentelemetry-icon"), null),
		("nginx", new("NGINX", "tech:nginx"), null),
		("traefik", new("Traefik", "tech:traefik-proxy"), null),
		("minio/minio", new("MinIO", null), null),
		("axllent/mailpit", new("Mailpit", null), null),
		("dotnet/aspire-dashboard", new("Aspire Dashboard", null), null),
		("dotnet/nightly/yarp", new("YARP", "tech:dotnet"), null),
		("dotnet/yarp", new("YARP", "tech:dotnet"), null),
		("likec4/likec4", new("LikeC4", null), null),
	];

	// ── Application frameworks ─────────────────────────────────────────────────────────────────
	//
	// In priority order: a meta-framework before the UI library it builds on, and both before the build
	// tool — an Astro site depends on Vite, and a Next.js app on React, but neither is what it is.

	static readonly (string Package, KnownTechnology Technology)[] JavaScriptFrameworks =
	[
		("astro", new("Astro", "tech:astro")),
		("next", new("Next.js", "tech:nextjs")),
		("nuxt", new("Nuxt", "tech:nuxt")),
		("@sveltejs/kit", new("SvelteKit", "tech:svelte-kit")),
		("@remix-run/react", new("Remix", "tech:remix")),
		("@remix-run/node", new("Remix", "tech:remix")),
		("gatsby", new("Gatsby", "tech:gatsby")),
		("@docusaurus/core", new("Docusaurus", "tech:docusaurus")),
		("@11ty/eleventy", new("Eleventy", "tech:eleventy")),
		("@builder.io/qwik", new("Qwik", "tech:qwik")),
		("@angular/core", new("Angular", "tech:angular")),
		("@nestjs/core", new("NestJS", "tech:nestjs")),
		("hono", new("Hono", "tech:hono")),
		("fastify", new("Fastify", "tech:fastify")),
		("express", new("Express", "tech:express")),
		("koa", new("Koa", "tech:koa")),
		("solid-js", new("SolidJS", "tech:solidjs")),
		("vue", new("Vue", "tech:vue")),
		("react", new("React", "tech:react")),
		("svelte", new("Svelte", "tech:svelte")),
		("vite", new("Vite", "tech:vite")),
	];

	static readonly (string Package, KnownTechnology Technology)[] PythonFrameworks =
	[
		("django", new("Django", "tech:django")),
		("fastapi", new("FastAPI", "tech:fastapi-icon")),
		("flask", new("Flask", "tech:flask")),
		("streamlit", new("Streamlit", "tech:streamlit")),
		("gradio", new("Gradio", "tech:gradio")),
	];

	/// <summary>
	/// A .NET project's flavour, from its packages, framework references and MSBuild SDKs — in priority
	/// order, so a gRPC or YARP service on ASP.NET Core reads as what it is rather than as ASP.NET Core.
	/// The icon stays the .NET one for every flavour, because that is the runtime; the label says the rest.
	/// </summary>
	static readonly (string Package, KnownTechnology Technology)[] DotnetFrameworks =
	[
		("Microsoft.Azure.Functions.Worker", new("Azure Functions", "azure:function-apps")),
		("Microsoft.NET.Sdk.Functions", new("Azure Functions", "azure:function-apps")),
		("Microsoft.Orleans.Server", new("Orleans", "tech:dotnet")),
		("Yarp.ReverseProxy", new("YARP", "tech:dotnet")),
		("Grpc.AspNetCore", new("ASP.NET Core gRPC", "tech:dotnet")),
		("Microsoft.NET.Sdk.BlazorWebAssembly", new("Blazor WebAssembly", "tech:dotnet")),
		("Microsoft.AspNetCore.Components.WebAssembly", new("Blazor WebAssembly", "tech:dotnet")),
		("Microsoft.NET.Sdk.Web", new("ASP.NET Core", "tech:dotnet")),
		("Microsoft.AspNetCore.App", new("ASP.NET Core", "tech:dotnet")),
		("Microsoft.NET.Sdk.Worker", new(".NET Worker Service", "tech:dotnet")),
	];

	/// <summary>A JavaScript runtime other than Node, named by the command that runs the app.</summary>
	static readonly Dictionary<string, KnownTechnology> Runtimes = new(StringComparer.OrdinalIgnoreCase)
	{
		["bun"] = new("Bun", "tech:bun"),
		["bunx"] = new("Bun", "tech:bun"),
		["deno"] = new("Deno", "tech:deno"),
	};

	/// <summary>The package manager an installer resource runs, named by its command.</summary>
	static readonly Dictionary<string, KnownTechnology> PackageManagers = new(StringComparer.OrdinalIgnoreCase)
	{
		["npm"] = new("npm", "tech:npm"),
		["npx"] = new("npm", "tech:npm"),
		["pnpm"] = new("pnpm", "tech:pnpm"),
		["yarn"] = new("Yarn", "tech:yarn"),
		["bun"] = new("Bun", "tech:bun"),
		["deno"] = new("Deno", "tech:deno"),
		["pip"] = new("pip", "tech:pypi"),
		["pip3"] = new("pip", "tech:pypi"),
		["poetry"] = new("Poetry", "tech:python-poetry"),
		["uv"] = new("uv", null),
	};

	/// <summary>Executables that run a JavaScript app; a project they run is read for its framework.</summary>
	static readonly HashSet<string> JavaScriptCommands = new(StringComparer.OrdinalIgnoreCase)
	{
		"node",
		"npm",
		"npx",
		"pnpm",
		"yarn",
		"bun",
		"bunx",
		"deno",
	};

	static readonly HashSet<string> PythonCommands = new(StringComparer.OrdinalIgnoreCase)
	{
		"python",
		"python3",
		"py",
		"uv",
		"uvicorn",
		"gunicorn",
	};

	/// <summary>
	/// Project files read for their dependencies, keyed by path, so a regeneration does not re-read an
	/// unchanged file. A rewritten file has a new timestamp and is read again.
	/// </summary>
	static readonly ConcurrentDictionary<string, (DateTime Written, ProjectPackages? Packages)> ProjectFiles = new(
		StringComparer.OrdinalIgnoreCase
	);

	/// <summary>Every icon the catalogue can return, for the test that checks each is in the manifest.</summary>
	internal static IEnumerable<string> Icons =>
		ByType
			.Values.Select(t => t.Icon)
			.Concat(ByImage.Select(i => i.Technology.Icon))
			.Concat(JavaScriptFrameworks.Select(f => f.Technology.Icon))
			.Concat(PythonFrameworks.Select(f => f.Technology.Icon))
			.Concat(DotnetFrameworks.Select(f => f.Technology.Icon))
			.Concat(Runtimes.Values.Select(t => t.Icon))
			.Concat(PackageManagers.Values.Select(t => t.Icon))
			.OfType<string>()
			.Distinct(StringComparer.Ordinal);

	/// <summary>Recognises a resource's technology, or returns <see langword="null"/> to leave it to the fuzzy matcher.</summary>
	/// <param name="resource">The resource drawn in the diagram.</param>
	/// <param name="hiddenOriginal">
	/// The resource it stands in for — the Azure resource behind a <c>RunAsContainer()</c> container —
	/// whose type says more about what it is.
	/// </param>
	/// <param name="overrides">The user's rules, consulted before anything built in.</param>
	public static KnownTechnology? Detect(
		IResource resource,
		IResource? hiddenOriginal = null,
		IReadOnlyList<TechnologyOverride>? overrides = null
	)
	{
		ArgumentNullException.ThrowIfNull(resource);

		var project = ProjectOf(resource);

		if (overrides is { Count: > 0 } && Override(resource, hiddenOriginal, project, overrides) is { } overridden)
		{
			return overridden;
		}

		if (Framework(project) is { } framework)
		{
			return framework;
		}

		var image = ImageTechnology(resource) ?? (hiddenOriginal is null ? null : ImageTechnology(hiddenOriginal));

		foreach (var key in TypeKeys(hiddenOriginal).Concat(TypeKeys(resource)))
		{
			if (!ByType.TryGetValue(key, out var known))
			{
				continue;
			}

			// An Azure service running locally is still that service, so its icon stays the Azure one and
			// the label says what is standing in for it: Azurite, an emulator, or a plain container.
			if (known.Name.StartsWith("Azure", StringComparison.Ordinal) && image is { } local)
			{
				return known with { Name = $"{known.Name} ({local.Emulates ?? "container"})" };
			}

			return known;
		}

		if (image is { } match)
		{
			return match.Emulates is null
				? match.Technology
				: match.Technology with
				{
					Name = $"{match.Technology.Name} ({match.Emulates})",
				};
		}

		return null;
	}

	/// <summary>
	/// The first of the user's rules to match: packages, then resource types, then container images, each in
	/// registration order. Taken verbatim — an override names exactly what the user wants shown.
	/// </summary>
	static KnownTechnology? Override(
		IResource resource,
		IResource? hiddenOriginal,
		AppProject project,
		IReadOnlyList<TechnologyOverride> overrides
	)
	{
		// An installer's packages are the app's, not the installer's, so it never matches a package rule.
		if (project.Kind is not ProjectKind.Installer && project.Packages is { } packages)
		{
			foreach (var rule in overrides)
			{
				if (rule.Match == TechnologyOverrideMatch.Package && packages.Contains(rule.Pattern.Trim()))
				{
					return FromRule(rule);
				}
			}
		}

		foreach (var rule in overrides)
		{
			if (
				rule.Match == TechnologyOverrideMatch.ResourceType
				&& (MatchesType(hiddenOriginal, rule.Pattern) || MatchesType(resource, rule.Pattern))
			)
			{
				return FromRule(rule);
			}
		}

		var path = ImagePathOf(resource) ?? (hiddenOriginal is null ? null : ImagePathOf(hiddenOriginal));

		if (path is not null)
		{
			foreach (var rule in overrides)
			{
				if (rule.Match == TechnologyOverrideMatch.ContainerImage && ImageMatches(path, ImagePath(rule.Pattern)))
				{
					return FromRule(rule);
				}
			}
		}

		return null;

		static KnownTechnology FromRule(TechnologyOverride rule) =>
			new(rule.Technology, string.IsNullOrWhiteSpace(rule.Icon) ? null : rule.Icon, IsOverride: true);
	}

	/// <summary>
	/// Whether a resource's type, a base type, or its dashboard resource type is <paramref name="pattern"/>: a
	/// full name when the pattern has a dot, otherwise a class name with or without its <c>Resource</c> suffix.
	/// </summary>
	internal static bool MatchesType(IResource? resource, string pattern)
	{
		if (resource is null || string.IsNullOrWhiteSpace(pattern))
		{
			return false;
		}

		var trimmed = WithoutArity(pattern.Trim());
		var qualified = trimmed.Contains('.', StringComparison.Ordinal);
		var key = TypeKey(trimmed);

		for (var type = resource.GetType(); type is not null && type != typeof(object); type = type.BaseType)
		{
			var matches = qualified
				? string.Equals(WithoutArity(type.FullName ?? type.Name), trimmed, StringComparison.OrdinalIgnoreCase)
				: string.Equals(TypeKey(type.Name), key, StringComparison.OrdinalIgnoreCase);

			if (matches)
			{
				return true;
			}
		}

		var snapshot = resource
			.Annotations.OfType<ResourceSnapshotAnnotation>()
			.LastOrDefault()
			?.InitialSnapshot.ResourceType;

		return !string.IsNullOrWhiteSpace(snapshot)
			&& (
				string.Equals(snapshot, trimmed, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(TypeKey(snapshot), key, StringComparison.OrdinalIgnoreCase)
			);

		static string WithoutArity(string name)
		{
			var tick = name.IndexOf('`', StringComparison.Ordinal);
			return tick >= 0 ? name[..tick] : name;
		}
	}

	/// <summary>
	/// A type's lookup key: the class name without generic arity or a <c>Resource</c> suffix, or a snapshot
	/// resource type with every non-alphanumeric character removed.
	/// </summary>
	internal static string TypeKey(string name)
	{
		var tick = name.IndexOf('`', StringComparison.Ordinal);
		var bare = tick >= 0 ? name[..tick] : name;
		string key = new([.. bare.Where(char.IsLetterOrDigit)]);

		return key.EndsWith("Resource", StringComparison.Ordinal) && key.Length > "Resource".Length
			? key[..^"Resource".Length]
			: key;
	}

	/// <summary>
	/// The keys a resource answers to, most specific first: its type and each base type, then the resource
	/// type its dashboard snapshot reports.
	/// </summary>
	static IEnumerable<string> TypeKeys(IResource? resource)
	{
		if (resource is null)
		{
			yield break;
		}

		for (var type = resource.GetType(); type is not null && type != typeof(object); type = type.BaseType)
		{
			yield return TypeKey(type.Name);
		}

		var snapshot = resource
			.Annotations.OfType<ResourceSnapshotAnnotation>()
			.LastOrDefault()
			?.InitialSnapshot.ResourceType;

		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			yield return TypeKey(snapshot);
		}
	}

	static string? ImagePathOf(IResource resource)
	{
		var image = resource.Annotations.OfType<ContainerImageAnnotation>().LastOrDefault()?.Image;

		return string.IsNullOrWhiteSpace(image) ? null : ImagePath(image);
	}

	/// <summary>
	/// Whether an image path matches a pattern: the whole path for a pattern with a slash, otherwise its
	/// last segment, so <c>redis</c> matches <c>bitnami/redis</c>.
	/// </summary>
	static bool ImageMatches(string path, string pattern) =>
		pattern.Length > 0
		&& (
			pattern.Contains('/', StringComparison.Ordinal)
				? string.Equals(path, pattern, StringComparison.Ordinal)
				: string.Equals(path[(path.LastIndexOf('/') + 1)..], pattern, StringComparison.Ordinal)
		);

	static (KnownTechnology Technology, string? Emulates)? ImageTechnology(IResource resource)
	{
		if (ImagePathOf(resource) is not { } path)
		{
			return null;
		}

		foreach (var (pattern, technology, emulates) in ByImage)
		{
			if (ImageMatches(path, pattern))
			{
				return (technology, emulates);
			}
		}

		return null;
	}

	/// <summary>
	/// An image reference reduced to its repository path: <c>mcr.microsoft.com/azure-storage/azurite:3</c>
	/// becomes <c>azure-storage/azurite</c>, and <c>docker.io/library/postgres:17</c> becomes <c>postgres</c>.
	/// </summary>
	internal static string ImagePath(string image)
	{
		var path = image.Trim().ToLowerInvariantSafe();
		var digest = path.IndexOf('@', StringComparison.Ordinal);

		if (digest >= 0)
		{
			path = path[..digest];
		}

		// A tag follows the last colon, but only after the last slash: a registry port has one too.
		var colon = path.LastIndexOf(':');

		if (colon > path.LastIndexOf('/'))
		{
			path = path[..colon];
		}

		var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

		// The first segment is a registry when it looks like a host: a dot, a port, or localhost.
		if (
			segments.Count > 1
			&& (
				segments[0].Contains('.', StringComparison.Ordinal)
				|| segments[0].Contains(':', StringComparison.Ordinal)
				|| segments[0] == "localhost"
			)
		)
		{
			segments.RemoveAt(0);
		}

		if (segments.Count > 1 && segments[0] == "library")
		{
			segments.RemoveAt(0);
		}

		return string.Join('/', segments);
	}

	// ── The resource's own project ─────────────────────────────────────────────────────────────

	enum ProjectKind
	{
		None,
		Dotnet,
		JavaScript,
		Python,
		Installer,
	}

	/// <summary>The project a resource is built from, its dependencies, and the command that runs it.</summary>
	readonly record struct AppProject(ProjectKind Kind, ProjectPackages? Packages, string? Command);

	/// <summary>
	/// The project a resource is built from: a .NET project (anything carrying <see cref="IProjectMetadata"/>,
	/// checked first because Aspire 17's <c>DotnetProjectResource</c> is also an executable), or a JavaScript
	/// or Python app in an executable's working directory. Installers run a package manager, not the app.
	/// </summary>
	static AppProject ProjectOf(IResource resource)
	{
		if (
			resource.Annotations.OfType<IProjectMetadata>().LastOrDefault() is { } metadata
			&& !string.IsNullOrWhiteSpace(metadata.ProjectPath)
		)
		{
			return new(ProjectKind.Dotnet, DotnetPackages(metadata.ProjectPath), null);
		}

		if (resource is not ExecutableResource executable || string.IsNullOrWhiteSpace(executable.WorkingDirectory))
		{
			return default;
		}

		var typeNames = TypeKeys(resource).ToList();
		var command = Path.GetFileNameWithoutExtension(executable.Command);
		var directory = executable.WorkingDirectory;

		if (typeNames.Any(n => n.Contains("Installer", StringComparison.OrdinalIgnoreCase)))
		{
			return new(ProjectKind.Installer, null, command);
		}

		var javaScript =
			JavaScriptCommands.Contains(command)
			|| typeNames.Any(n =>
				n.Contains("JavaScript", StringComparison.OrdinalIgnoreCase)
				|| n.Contains("Node", StringComparison.OrdinalIgnoreCase)
				|| n.Contains("Vite", StringComparison.OrdinalIgnoreCase)
				|| n.StartsWith("Bun", StringComparison.OrdinalIgnoreCase)
				|| n.StartsWith("Deno", StringComparison.OrdinalIgnoreCase)
			);

		if (javaScript)
		{
			return new(ProjectKind.JavaScript, Read(Path.Combine(directory, "package.json"), PackageJson), command);
		}

		var python =
			PythonCommands.Contains(command)
			|| typeNames.Any(n =>
				n.Contains("Python", StringComparison.OrdinalIgnoreCase)
				|| n.Contains("Uvicorn", StringComparison.OrdinalIgnoreCase)
			);

		return python
			? new(
				ProjectKind.Python,
				ProjectPackages.Union(
					Read(Path.Combine(directory, "pyproject.toml"), ProjectPackages.FromRequirements),
					Read(Path.Combine(directory, "requirements.txt"), ProjectPackages.FromRequirements)
				),
				command
			)
			: default;
	}

	/// <summary>The framework a project is built with, or the package manager an installer runs.</summary>
	static KnownTechnology? Framework(AppProject project) =>
		project.Kind switch
		{
			ProjectKind.Dotnet => FirstIn(DotnetFrameworks, project.Packages),
			ProjectKind.JavaScript => FirstIn(JavaScriptFrameworks, project.Packages)
				?? Lookup(Runtimes, project.Command),
			ProjectKind.Python => FirstIn(PythonFrameworks, project.Packages),
			ProjectKind.Installer => Lookup(PackageManagers, project.Command),
			_ => null,
		};

	static KnownTechnology? FirstIn(
		(string Package, KnownTechnology Technology)[] frameworks,
		ProjectPackages? packages
	)
	{
		if (packages is null)
		{
			return null;
		}

		foreach (var (package, technology) in frameworks)
		{
			if (packages.Contains(package))
			{
				return technology;
			}
		}

		return null;
	}

	static KnownTechnology? Lookup(Dictionary<string, KnownTechnology> table, string? command) =>
		command is not null && table.TryGetValue(command, out var known) ? known : null;

	/// <summary>
	/// A .NET project's dependencies: the restore output's direct packages and framework references, because
	/// an SDK such as Purview.BuildSdk adds them outside the project file, then the project file's own SDKs and
	/// references. A file-based app (<c>app.cs</c>) declares its own in <c>#:sdk</c> and <c>#:package</c>.
	/// </summary>
	static ProjectPackages? DotnetPackages(string projectPath)
	{
		if (projectPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
		{
			return Read(projectPath, ProjectPackages.FromFileBasedApp);
		}

		var directory = Path.GetDirectoryName(projectPath);

		return ProjectPackages.Union(
			directory is null
				? null
				: Read(Path.Combine(directory, "obj", "project.assets.json"), ProjectPackages.FromAssets),
			Read(projectPath, ProjectPackages.FromProjectFile)
		);
	}

	static ProjectPackages? Read(string path, Func<string, ProjectPackages?> parse)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}

			var written = File.GetLastWriteTimeUtc(path);

			if (ProjectFiles.TryGetValue(path, out var cached) && cached.Written == written)
			{
				return cached.Packages;
			}

			var packages = parse(File.ReadAllText(path));

			ProjectFiles[path] = (written, packages);

			return packages;
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
	}

	static ProjectPackages? PackageJson(string text) => ProjectPackages.FromPackageJson(text);

	/// <summary>The first framework, in priority order, among a <c>package.json</c>'s dependencies.</summary>
	internal static KnownTechnology? JavaScriptFramework(string packageJson) =>
		FirstIn(JavaScriptFrameworks, ProjectPackages.FromPackageJson(packageJson));

	/// <summary>
	/// The first framework, in priority order, named as a dependency in a <c>pyproject.toml</c> or a
	/// <c>requirements.txt</c>.
	/// </summary>
	internal static KnownTechnology? PythonFramework(string text) =>
		FirstIn(PythonFrameworks, ProjectPackages.FromRequirements(text));

	/// <summary>The first flavour, in priority order, among a .NET project's dependencies.</summary>
	internal static KnownTechnology? DotnetFramework(ProjectPackages? packages) => FirstIn(DotnetFrameworks, packages);

	/// <summary>
	/// The dependencies a project declares: a set of names (npm, NuGet, MSBuild SDKs), or a Python
	/// requirements text searched for whole names.
	/// </summary>
	internal sealed class ProjectPackages
	{
		readonly HashSet<string> _names;
		readonly string? _requirements;

		ProjectPackages(HashSet<string> names, string? requirements)
		{
			_names = names;
			_requirements = requirements;
		}

		/// <summary>Whether the project depends on <paramref name="package"/>, ignoring case.</summary>
		public bool Contains(string package) =>
			_names.Contains(package)
			|| (_requirements is not null && ContainsRequirement(_requirements, package.ToLowerInvariantSafe()));

		public static ProjectPackages? Union(ProjectPackages? first, ProjectPackages? second)
		{
			if (first is null || second is null)
			{
				return first ?? second;
			}

			HashSet<string> names = new(first._names, StringComparer.OrdinalIgnoreCase);
			names.UnionWith(second._names);

			var requirements = (first._requirements, second._requirements) switch
			{
				(null, var only) => only,
				(var only, null) => only,
				var (a, b) => $"{a}\n{b}",
			};

			return new(names, requirements);
		}

		internal static ProjectPackages FromNames(params IEnumerable<string> names) =>
			new(new(names, StringComparer.OrdinalIgnoreCase), null);

		/// <summary>The names in a <c>package.json</c>'s dependencies, dev dependencies and peer dependencies.</summary>
		public static ProjectPackages? FromPackageJson(string packageJson)
		{
			HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

			try
			{
				using var document = JsonDocument.Parse(
					packageJson,
					new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
				);

				if (document.RootElement.ValueKind != JsonValueKind.Object)
				{
					return null;
				}

				foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
				{
					if (
						document.RootElement.TryGetProperty(section, out var dependencies)
						&& dependencies.ValueKind == JsonValueKind.Object
					)
					{
						foreach (var dependency in dependencies.EnumerateObject())
						{
							names.Add(dependency.Name);
						}
					}
				}
			}
			catch (JsonException)
			{
				return null;
			}

			return new(names, null);
		}

		/// <summary>
		/// A <c>pyproject.toml</c> or <c>requirements.txt</c>, kept as text: both name a dependency at the
		/// start of a requirement string (<c>fastapi[standard]&gt;=0.115</c>, <c>"django&gt;=5"</c>), and only
		/// the name is needed, so it is searched rather than parsed.
		/// </summary>
		public static ProjectPackages FromRequirements(string text) => new([], text.ToLowerInvariantSafe());

		/// <summary>
		/// The direct packages and framework references of every target framework in a restore output
		/// (<c>obj/project.assets.json</c>). Transitive packages are left out: what a project references
		/// itself is what it is.
		/// </summary>
		public static ProjectPackages? FromAssets(string json)
		{
			HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

			try
			{
				using var document = JsonDocument.Parse(
					json,
					new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
				);

				if (
					document.RootElement.ValueKind != JsonValueKind.Object
					|| !document.RootElement.TryGetProperty("project", out var project)
					|| !project.TryGetProperty("frameworks", out var frameworks)
					|| frameworks.ValueKind != JsonValueKind.Object
				)
				{
					return null;
				}

				foreach (var framework in frameworks.EnumerateObject())
				{
					foreach (var section in new[] { "dependencies", "frameworkReferences" })
					{
						if (
							framework.Value.ValueKind == JsonValueKind.Object
							&& framework.Value.TryGetProperty(section, out var entries)
							&& entries.ValueKind == JsonValueKind.Object
						)
						{
							foreach (var entry in entries.EnumerateObject())
							{
								names.Add(entry.Name);
							}
						}
					}
				}
			}
			catch (JsonException)
			{
				return null;
			}

			return new(names, null);
		}

		/// <summary>
		/// A project file's MSBuild SDKs (the <c>Sdk</c> attribute, <c>&lt;Sdk&gt;</c> elements and SDK imports,
		/// each without a <c>/version</c>) and its package and framework references.
		/// </summary>
		public static ProjectPackages? FromProjectFile(string xml)
		{
			HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

			try
			{
				var document = XDocument.Parse(xml);

				AddSdks(names, document.Root?.Attribute("Sdk")?.Value);

				foreach (var element in document.Descendants())
				{
					switch (element.Name.LocalName)
					{
						case "Sdk":
							AddSdks(names, element.Attribute("Name")?.Value);
							break;
						case "Import":
							AddSdks(names, element.Attribute("Sdk")?.Value);
							break;
						case "PackageReference" or "FrameworkReference":
							if (element.Attribute("Include")?.Value is { Length: > 0 } include)
							{
								names.Add(include.Trim());
							}

							break;
						default:
							break;
					}
				}
			}
			catch (XmlException)
			{
				return null;
			}

			return new(names, null);

			static void AddSdks(HashSet<string> names, string? sdks)
			{
				foreach (
					var sdk in (sdks ?? string.Empty).Split(
						';',
						StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
					)
				)
				{
					names.Add(sdk.Split('/')[0].Trim());
				}
			}
		}

		/// <summary>
		/// A file-based app's <c>#:sdk</c> and <c>#:package</c> directives, each without its <c>@version</c>.
		/// With no <c>#:sdk</c>, the app uses <c>Microsoft.NET.Sdk</c>.
		/// </summary>
		public static ProjectPackages FromFileBasedApp(string source)
		{
			HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

			foreach (var raw in source.Split('\n'))
			{
				var line = raw.Trim();

				// Directives come first; the first line of code ends them.
				if (line.Length > 0 && !line.StartsWith('#'))
				{
					break;
				}

				foreach (var directive in new[] { "#:sdk ", "#:package " })
				{
					if (line.StartsWith(directive, StringComparison.OrdinalIgnoreCase))
					{
						var name = line[directive.Length..].Trim().Split('@')[0].Trim();

						if (name.Length > 0)
						{
							names.Add(name);
						}
					}
				}
			}

			return new(names, null);
		}

		/// <summary>
		/// Whether <paramref name="package"/> is named as a whole: nothing that could extend the name may
		/// follow or precede it, so <c>flask</c> does not match <c>flask-cors</c>.
		/// </summary>
		static bool ContainsRequirement(string lower, string package)
		{
			if (package.Length == 0)
			{
				return false;
			}

			for (
				var at = lower.IndexOf(package, StringComparison.Ordinal);
				at >= 0;
				at = lower.IndexOf(package, at + 1, StringComparison.Ordinal)
			)
			{
				var before = at == 0 ? '\n' : lower[at - 1];
				var end = at + package.Length;
				var after = end < lower.Length ? lower[end] : '\n';

				if (!IsNamePart(before) && !IsNamePart(after) && after != '-')
				{
					return true;
				}
			}

			return false;

			static bool IsNamePart(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.';
		}
	}
}
