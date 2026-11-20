using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

/// <summary>
/// The user's technology overrides correct what the catalogue gets wrong, and name what it does not know,
/// once for the whole AppHost.
/// </summary>
public sealed partial class TechnologyCatalogTests
{
	static TechnologyOverride[] CreateOverrides(params TechnologyOverride[] overrides) => overrides;

	static TechnologyOverride CreateOverride(
		TechnologyOverrideMatch match,
		string pattern,
		string technology = "Custom",
		string? icon = "tech:custom"
	) => new(match, pattern, technology, icon);

	[Test]
	public async Task Detect_LikeC4Image_IsLikeC4()
	{
		// Act
		var detected = TechnologyCatalog.Detect(
			WithImage(new ContainerResource("aspirec4"), "ghcr.io/likec4/likec4:1.40")
		);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("LikeC4", null));
	}

	[Test]
	public async Task Detect_ImageOverride_BeatsTheCatalogue()
	{
		// Arrange
		var seq = WithImage(new ContainerResource("seq"), "docker.io/datalust/seq:2024.3");
		var overrides = CreateOverrides(
			CreateOverride(
				TechnologyOverrideMatch.ContainerImage,
				"datalust/seq:latest",
				"Seq logging",
				"@/icons/seq.svg"
			)
		);

		// Act
		var detected = TechnologyCatalog.Detect(seq, overrides: overrides);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Seq logging", "@/icons/seq.svg", IsOverride: true));
	}

	[Test]
	public async Task Detect_ImageOverrideWithoutASlash_MatchesAnyNamespace()
	{
		// Arrange
		var widget = WithImage(new ContainerResource("widget"), "registry.acme.io/team/widget:2");
		var overrides = CreateOverrides(CreateOverride(TechnologyOverrideMatch.ContainerImage, "widget"));

		// Act
		var detected = TechnologyCatalog.Detect(widget, overrides: overrides);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("Custom");
	}

	[Test]
	[Arguments("RedisResource")]
	[Arguments("Redis")]
	[Arguments("Aspire.Hosting.AspireC4.LikeC4.Icons.TechnologyCatalogTests+RedisResource")]
	public async Task Detect_TypeOverride_MatchesABaseTypeByShortOrFullName(string pattern)
	{
		// Arrange
		var overrides = CreateOverrides(CreateOverride(TechnologyOverrideMatch.ResourceType, pattern, "Cache"));

		// Act
		var detected = TechnologyCatalog.Detect(new CachingRedisResource("cache"), overrides: overrides);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("Cache");
	}

	[Test]
	public async Task Detect_TypeOverride_MatchesTheHiddenOriginal()
	{
		// Arrange
		var container = WithImage(new ContainerResource("azure-postgres"), "library/postgres:17");
		var overrides = CreateOverrides(
			CreateOverride(TechnologyOverrideMatch.ResourceType, "AzurePostgresFlexibleServer", "Primary store")
		);

		// Act
		var detected = TechnologyCatalog.Detect(
			container,
			new AzurePostgresFlexibleServerResource("azure-postgres"),
			overrides
		);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("Primary store");
	}

	[Test]
	public async Task Detect_PackageOverride_BeatsTheBuiltInFramework()
	{
		// Arrange
		using var project = CreatePackageJson("@tanstack/react-start", "react", "vite");
		var overrides = CreateOverrides(
			CreateOverride(TechnologyOverrideMatch.Package, "@tanstack/react-start", "TanStack Start", "tech:react")
		);

		// Act
		var detected = TechnologyCatalog.Detect(new ViteAppResource("site", project.Path), overrides: overrides);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("TanStack Start");
	}

	[Test]
	public async Task Detect_PackageOverride_MatchesADotnetPackageAndSdk()
	{
		// Arrange
		using var project = CreateDotnetProject(
			sdk: "Microsoft.NET.Sdk.Web",
			items: "<PackageReference Include=\"MassTransit\" />"
		);
		var resource = CreateProjectResource("orders", Path.Combine(project.Path, "Service.csproj"));

		// Act
		var byPackage = TechnologyCatalog.Detect(
			resource,
			overrides: CreateOverrides(CreateOverride(TechnologyOverrideMatch.Package, "masstransit", "MassTransit"))
		);
		var bySdk = TechnologyCatalog.Detect(
			resource,
			overrides: CreateOverrides(
				CreateOverride(TechnologyOverrideMatch.Package, "Microsoft.NET.Sdk.Web", "Web API")
			)
		);

		// Assert
		await Assert.That(byPackage?.Name).IsEqualTo("MassTransit");
		await Assert.That(bySdk?.Name).IsEqualTo("Web API");
	}

	[Test]
	public async Task Detect_PackageOverride_NeverMatchesAnInstaller()
	{
		// Arrange
		using var project = CreatePackageJson("astro");
		var overrides = CreateOverrides(CreateOverride(TechnologyOverrideMatch.Package, "astro", "Docs site"));

		// Act
		var detected = TechnologyCatalog.Detect(
			new JavaScriptInstallerResource("site-installer", project.Path),
			overrides: overrides
		);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("npm");
	}

	[Test]
	public async Task Detect_Overrides_PreferAPackageToATypeToAnImage()
	{
		// Arrange — the image rule is registered first, but a package is the most specific evidence.
		using var project = CreatePackageJson("express");
		ViteAppResource site = new("site", project.Path);
		site.Annotations.Add(new ContainerImageAnnotation { Image = "acme/site" });
		var overrides = CreateOverrides(
			CreateOverride(TechnologyOverrideMatch.ContainerImage, "acme/site", "By image"),
			CreateOverride(TechnologyOverrideMatch.ResourceType, "ViteApp", "By type"),
			CreateOverride(TechnologyOverrideMatch.Package, "express", "By package")
		);

		// Act
		var detected = TechnologyCatalog.Detect(site, overrides: overrides);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("By package");
	}

	[Test]
	public async Task Detect_NoOverrideMatches_FallsBackToTheCatalogue()
	{
		// Arrange
		var overrides = CreateOverrides(CreateOverride(TechnologyOverrideMatch.ContainerImage, "acme/other"));

		// Act
		var detected = TechnologyCatalog.Detect(
			WithImage(new ContainerResource("db"), "postgres:17"),
			overrides: overrides
		);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("PostgreSQL", "tech:postgresql"));
	}

	// Through the model builder

	[Test]
	public async Task Build_OverrideWithoutAnIcon_MatchesOneFromItsLabel()
	{
		// Arrange
		var widget = WithImage(new ContainerResource("widget"), "acme/widget");
		var overrides = CreateOverrides(
			new TechnologyOverride(TechnologyOverrideMatch.ContainerImage, "acme/widget", "Redis")
		);

		// Act
		var element = ModelBuilder.Build([widget], technologyOverrides: overrides).Elements.Single();

		// Assert
		await Assert.That(element.Technology).IsEqualTo("Redis");
		await Assert.That(element.Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_OverrideIcon_AppliesWithAutoIconsOff()
	{
		// Arrange
		var widget = WithImage(new ContainerResource("widget"), "acme/widget");
		var overrides = CreateOverrides(
			CreateOverride(TechnologyOverrideMatch.ContainerImage, "acme/widget", "Widget", "@/icons/widget.svg")
		);

		// Act
		var element = ModelBuilder
			.Build([widget], autoIconsEnabled: false, technologyOverrides: overrides)
			.Elements.Single();

		// Assert
		await Assert.That(element.Icon).IsEqualTo("@/icons/widget.svg");
	}

	[Test]
	public async Task Build_ResourceDetails_StillBeatAnOverride()
	{
		// Arrange
		var widget = WithImage(new ContainerResource("widget"), "acme/widget");
		widget.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("widget").WithTechnology("Hand-picked").WithIcon("tech:nginx")
		);
		var overrides = CreateOverrides(CreateOverride(TechnologyOverrideMatch.ContainerImage, "acme/widget"));

		// Act
		var element = ModelBuilder.Build([widget], technologyOverrides: overrides).Elements.Single();

		// Assert
		await Assert.That(element.Technology).IsEqualTo("Hand-picked");
		await Assert.That(element.Icon).IsEqualTo("tech:nginx");
	}
}
