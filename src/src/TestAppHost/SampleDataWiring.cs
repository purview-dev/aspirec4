using Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// Wires up the sample Entity Framework Core models and their d2 ER diagram.
/// </summary>
/// <remarks>
/// Kept out of <c>AppHost.cs</c> so the top-level program stays under the CA1506 class-coupling
/// limit, matching how the rest of this sample's optional configuration is organised.
/// </remarks>
static class SampleDataWiring
{
	/// <summary>
	/// Adds the ER diagram and the service that owns the sample EF Core models.
	/// </summary>
	/// <remarks>
	/// The <c>DbContext</c> types live in libraries this project references rather than in the
	/// executable itself — the usual layout for a real service — so AspireC4 locates them by scanning
	/// the project's build output rather than just its own assembly. No per-project opt-in is needed:
	/// the diagram discovers every project resource automatically. The <c>WithEntityFrameworkModel()</c>
	/// call is kept here only to label the project's group.
	/// </remarks>
	public static IResourceBuilder<ProjectResource> AddSampleDataService(
		this IDistributedApplicationBuilder builder,
		IResourceBuilder<AspireC4Resource> c4
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(c4);

		c4.WithEntityFrameworkDiagram(configure: static options => options.Title = "AspireC4 Sample Schema");

		return builder
			.AddProject<Projects.TestAppHost_DataService>("data-service")
			.WithEntityFrameworkModel(options => options.Label = "Sample Data Service")
			.WithLikeC4Details(options =>
				options
					.WithLabel("Sample Data Service")
					.WithDescription("Owns the simple and complex sample **Entity Framework Core** models.")
					.WithSummary("Sample EF Core models")
			);
	}
}
