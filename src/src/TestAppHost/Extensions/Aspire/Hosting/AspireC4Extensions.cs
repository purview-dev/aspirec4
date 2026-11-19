using System.ComponentModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
static class AspireC4Extensions
{
	public static IResourceBuilder<AspireC4Resource> ConfigureAspireC4TestHost(
		this IResourceBuilder<AspireC4Resource> builder
	)
	{
		builder.ApplicationBuilder.AddContainerEnvironment();

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

			// Formatting runs `npx likec4 format`, which walks up and scans this whole repository when the
			// sample runs from inside it. Every sample AppHost turns it off for the same reason.
			opts.WithFormatGeneratedFile(false);

			// The Azure provisioning environment needs no exclusion here: AspireC4 leaves it out of every
			// diagram by default (AspireC4DiagramOptions.ExcludedResourceTypeNames).
		});

		// We're adding LikeC4 pazzazz to the LikeC4 server resource for this demo. WithServerLikeC4Details
		// rather than ConfigureServer, so this reads the same in every sample AppHost: Go and Python
		// cannot use ConfigureServer.
		builder.WithServerLikeC4Details(opts =>
			opts.WithLabel("AspireC4")
				.WithSummary(
					"Describe your Aspire orchestration as a live LikeC4 system architecture diagram - auto generated"
				)
				// This icon supports both light and dark mode in one...
				.WithIcon("@/likec4/likec4-logo.svg")
				.WithLink("https://purview.dev/projects/aspirec4", "Learn more about AspireC4")
				.WithLink("https://github.com/purview-dev/aspirec4/", "AspireC4 on GitHub")
				.WithLink("https://github.com/kieronlanning", "Connect with the author on GitHub")
		);

		return builder;
	}
}
