/// <summary>
/// Adds the sample Node.js app, its connections, and the parameters the LikeC4 model must hide.
/// </summary>
/// <remarks>
/// Kept out of <c>AppHost.cs</c> so the top-level program stays under the CA1506 class-coupling limit,
/// as <see cref="SampleDataWiring"/> is. The TypeScript, Go and Python samples make the same calls inline;
/// the shared sample contract asserts that every sample draws the same result.
/// </remarks>
static class SampleNodeAppWiring
{
	/// <summary>Adds <c>node-app</c>, connected to the four stores, and two hidden parameters.</summary>
	public static IResourceBuilder<IResource> AddSampleNodeApp(
		this IDistributedApplicationBuilder builder,
		IResourceBuilder<IResourceWithConnectionString> azureRedis,
		IResourceBuilder<IResourceWithConnectionString> localRedis,
		IResourceBuilder<IResourceWithConnectionString> azurePostgres,
		IResourceBuilder<IResourceWithConnectionString> localPostgres
	)
	{
		ArgumentNullException.ThrowIfNull(builder);

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
			// These references generate the connections in the C4 model, and give the application the
			// connection strings for these dependencies through Aspire's WithReference.
			.WithLikeC4ConnectionReference(azureRedis, opts =>
				opts.WithLabel("Caches sessions").WithTechnology("Redis Protocol").WithKind("RESP")
			)
			.WithLikeC4ConnectionReference(localRedis, opts =>
				opts.WithLabel("Caches  sessions (local)").WithTechnology("Redis Protocol").WithKind("RESP")
			)
			.WithLikeC4ConnectionReference(azurePostgres, opts =>
				opts.WithLabel("Persists data").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
			)
			.WithLikeC4ConnectionReference(localPostgres, opts =>
				opts.WithLabel("Persists data (local)").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
			)
			.WaitFor(azureRedis)
			.WaitFor(localRedis)
			.WaitFor(azurePostgres)
			.WaitFor(localPostgres);

		// Parameters, including one from configuration. They are excluded from the diagram by default:
		// they are not architecture, and their values may be secrets.
		var testingParam = builder
			.AddParameter("testing-resource-parameter", "This should be hidden from the LikeC4 model", false, false)
			.WithDescription("This should be hidden from the LikeC4 model");
		var testParamFromConfig = builder
			.AddParameterFromConfiguration("testing-resource-parameter-from-config", "DOTNET_ENVIRONMENT", false)
			.WithDescription("This should be hidden from the LikeC4 model");

		// Only the C# sample links them: WithReferenceRelationship is not exported to the polyglot
		// AppHosts. The parameters are hidden either way, so every sample still draws the same diagram.
		testingParam.WithReferenceRelationship(nodeApp);
		testParamFromConfig.WithReferenceRelationship(nodeApp);

		return nodeApp;
	}
}
