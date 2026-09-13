using System.ComponentModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
static class AspireC4Extensions
{
	public static IResourceBuilder<AspireC4Resource> ConfigureAspireC4TestHost(
		this IResourceBuilder<AspireC4Resource> builder
	)
	{
		var likeC4ResourcePath = Path.Combine(
			Path.GetDirectoryName(typeof(TestAppHostProgram).Assembly.Location)!,
			"likec4"
		);
		var extensionsDir = Path.Combine(likeC4ResourcePath, "extensions");
		var imagesDir = Path.Combine(likeC4ResourcePath, "images");
		builder.ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
		{
			// Register hand-authored extension files (custom styles, views, model extensions).
			// The files sit next to the TestAppHost assembly so they are available in both the normal
			// run context (when the TestAppHost is launched directly) and the integration-test context
			// (where the TestAppHost assembly is copied to the test output directory).
			if (Directory.Exists(extensionsDir))
				opts.WithAdditionalDSLFolder(extensionsDir);

			// There are some assets in the repo root that we'll include.
			if (Directory.Exists(imagesDir))
			{
				// Overriding the default '@' to point to our copied in test apps folder
				opts.WithImageAliasFolder("@", imagesDir);
			}

			// Just for the sake of this demo, we'll include the AspireC4 internal resource in the diagram
			// as we reference it from the additional DSLs.
			opts.WithIncludeAspireC4InternalResource(true);

			// The internal Azure environment (resource-group) container is provisioning infrastructure,
			// not architecture — keep it out of the diagram. Without this it maps to the `system`
			// element kind, which collides with the `element system` declaration in extend.c4.
#pragma warning disable ASPIREAZURE001 // AzureEnvironmentResource is experimental
			opts.WithExcludedResourceType<AzureEnvironmentResource>();
#pragma warning restore ASPIREAZURE001
		});

		// We're adding LikeC4 pazzazz to the LikeC4 server resource for this demo...
		builder.ConfigureServer(s =>
			// Add some custom details to the LikeC4 diagram for this resource - this is optional, but it shows how you can add custom links, icons and descriptions to the diagram for your Aspire resources.
			s.WithLikeC4Details(opts =>
				opts.WithLabel("AspireC4")
					.WithSummary(
						"Describe your Aspire orchestration as a live LikeC4 system architecture diagram - auto generated"
					)
					// This icon supports both light and dark mode in one...
					.WithIcon("@/likec4/likec4-logo.svg")
					.WithLink("https://purview.dev/projects/aspirec4", "Learn more about AspireC4")
					.WithLink("https://github.com/purview-dev/aspirec4/", "AspireC4 on GitHub")
					.WithLink("https://github.com/kieronlanning", "Connect with the author on GitHub")
			)
		);

		return builder;
	}
}
