namespace Aspire.Hosting.AspireC4.LikeC4;

sealed partial class IconInferenceTests
{
	public static IEnumerable<IconTestScenario> Scenarios() =>
		[
			new(() => (CreateSnapshotResource("redis", "Azure.ManagedRedis"), null), "azure:azure-managed-redis"),
			new(
				() =>
					(
						CreateContainerResource("azure-redis"),
						AddHiddenSnapshot<AzureManagedRedisResource>(new("azure-redis"), "AzureManagedRedis")
					),
				"azure:azure-managed-redis"
			),
			new(
				() =>
					(
						CreateContainerResource("azure-cache"),
						AddHiddenSnapshot<AzureRedisCacheResource>(new("azure-cache"), "AzureRedisCache")
					),
				"azure:cache-redis"
			),
			// Recognised by TechnologyCatalog rather than scored: the tokens ["sql", "server"] tie between
			// "sql-server" and "arc-sql-server" (Azure Arc's hybrid SQL), and the tie used to go to Arc.
			new(() => (CreateSnapshotResource("sqlserver", "Azure.SqlServer"), null), "azure:sql-server"),
			new(
				() =>
					(
						CreateContainerResource("azure-sql"),
						AddHiddenSnapshot<AzureSqlServerResource>(new("azure-sql"), "AzureSqlServer")
					),
				"azure:sql-server"
			),
			new(
				() => (CreateSnapshotResource("postgres", "Azure.PostgresFlexibleServer"), null),
				"azure:azure-database-postgre-sql-server"
			),
			new(
				() =>
					(
						CreateContainerResource("azure-postgres"),
						AddHiddenSnapshot<AzurePostgresFlexibleServerResource>(
							new("azure-postgres"),
							"AzurePostgresFlexibleServer"
						)
					),
				"azure:azure-database-postgre-sql-server"
			),
			new(() => (CreateSnapshotResource("servicebus", "Azure.ServiceBus"), null), "azure:azure-service-bus"),
			new(() => (CreateSnapshotResource("cosmos", "Azure.CosmosDb"), null), "azure:azure-cosmos-db"),
			new(() => (CreateSnapshotResource("keyvault", "Azure.KeyVault"), null), "azure:key-vaults"),
			new(() => (CreateSnapshotResource("eventhubs", "Azure.EventHubs"), null), "azure:event-hubs"),
			new(() => (CreateSnapshotResource("storage", "Azure.StorageAccount"), null), "azure:storage-accounts"),
			new(() => (CreateContainerResource("redis"), null), "tech:redis"),
			new(() => (CreateContainerResource("postgres"), null), "tech:postgresql"),
			new(() => (CreateContainerResource("rabbitmq"), null), "tech:rabbitmq"),
			new(() => (CreateContainerResource("mongodb"), null), "tech:mongodb"),
			new(() => (CreateContainerResource("mysql"), null), "tech:mysql"),
		];
}
