using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

/// <summary>
/// The catalogue recognises what a resource is before the fuzzy matcher guesses from its name. Stand-in
/// types carry the class names of Aspire's own, which is all the catalogue matches on.
/// </summary>
public sealed partial class TechnologyCatalogTests
{
	// Stand-ins for Aspire's resource types, named as Aspire names them.

	sealed class AzureStorageResource(string name) : Resource(name);

	sealed class AzureBlobStorageResource(string name) : Resource(name);

	sealed class AzureQueueStorageResource(string name) : Resource(name);

	sealed class AzureTableStorageResource(string name) : Resource(name);

	sealed class AzurePostgresFlexibleServerResource(string name) : Resource(name);

	sealed class ViteAppResource(string name, string workingDirectory)
		: ExecutableResource(name, "npm", workingDirectory);

	sealed class JavaScriptInstallerResource(string name, string workingDirectory)
		: ExecutableResource(name, "npm", workingDirectory);

	sealed class JavaScriptInstallerCommandResource(string name, string command, string workingDirectory)
		: ExecutableResource(name, command, workingDirectory);

	sealed class PythonAppResource(string name, string workingDirectory)
		: ExecutableResource(name, "python", workingDirectory);

	/// <summary>A user's own subclass of a known type is still that type.</summary>
	class RedisResource(string name) : ContainerResource(name);

	sealed class CachingRedisResource(string name) : RedisResource(name);

	// Helpers

	static T WithImage<T>(T resource, string image)
		where T : IResource
	{
		resource.Annotations.Add(new ContainerImageAnnotation { Image = image });

		return resource;
	}

	/// <summary>A working directory holding project files, removed when the test is done.</summary>
	sealed class ProjectDirectory : IDisposable
	{
		public ProjectDirectory(string fileName, string content)
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aspirec4-tech-{Guid.NewGuid():N}");
			Directory.CreateDirectory(Path);
			Add(fileName, content);
		}

		public string Path { get; }

		/// <summary>Writes another file, creating its folder, and returns its full path.</summary>
		public string Add(string relativePath, string content)
		{
			var path = System.IO.Path.Combine(Path, relativePath);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
			File.WriteAllText(path, content);

			return path;
		}

		public void Dispose() => Directory.Delete(Path, recursive: true);
	}

	/// <summary>Aspire's project metadata, pointing at a project file in a test directory.</summary>
	sealed class TestProjectMetadata(string projectPath) : IProjectMetadata
	{
		public string ProjectPath { get; } = projectPath;
	}

	/// <summary>A .NET project resource, as <c>AddProject</c> creates one.</summary>
	static ProjectResource CreateProjectResource(string name, string projectPath)
	{
		ProjectResource resource = new(name);
		resource.Annotations.Add(new TestProjectMetadata(projectPath));

		return resource;
	}

	static ProjectDirectory CreateDotnetProject(
		string sdk = "Microsoft.NET.Sdk",
		string? assets = null,
		string items = ""
	)
	{
		ProjectDirectory project = new(
			"Service.csproj",
			$"""<Project Sdk="{sdk}"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>{items}</ItemGroup></Project>"""
		);

		if (assets is not null)
		{
			project.Add(System.IO.Path.Combine("obj", "project.assets.json"), assets);
		}

		return project;
	}

	static string CreateAssets(string dependencies = "", string frameworkReferences = "") =>
		$$"""
			{
			  "version": 3,
			  "project": {
			    "frameworks": {
			      "net10.0": {
			        "dependencies": { {{dependencies}} },
			        "frameworkReferences": { {{frameworkReferences}} }
			      }
			    }
			  }
			}
			""";

	static ProjectDirectory CreatePackageJson(params string[] dependencies) =>
		new(
			"package.json",
			$$"""{ "name": "site", "dependencies": { {{string.Join(", ", dependencies.Select(d => $"\"{d}\": \"^1.0.0\""))}} } }"""
		);

	// The catalogue itself

	[Test]
	public async Task Icons_EveryIcon_IsInTheBundledManifest()
	{
		// The catalogue is curated by hand, so a manifest refresh that renames an icon must fail here
		// rather than leave elements quietly drawn with nothing.
		// Act
		var missing = TechnologyCatalog.Icons.Where(icon => !IconMatcher.IsKnownIcon(icon)).ToList();

		// Assert
		await Assert.That(missing).IsEmpty();
	}

	// Resource types

	[Test]
	public async Task Detect_AzureStorage_IsAStorageAccountNotAStorageMover()
	{
		// Act
		var detected = TechnologyCatalog.Detect(new AzureStorageResource("storage"));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Azure Storage", "azure:storage-accounts"));
	}

	[Test]
	public async Task Detect_AzureStorageRunAsEmulator_SaysAzuriteIsRunningIt()
	{
		// Arrange — RunAsEmulator() puts the Azurite image on the storage resource itself.
		var storage = WithImage(new AzureStorageResource("storage"), "mcr.microsoft.com/azure-storage/azurite:3.34.0");

		// Act
		var detected = TechnologyCatalog.Detect(storage);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Azure Storage (Azurite)", "azure:storage-accounts"));
	}

	[Test]
	public async Task Detect_StorageServices_EachHaveTheirOwnIcon()
	{
		// Act
		var blobs = TechnologyCatalog.Detect(new AzureBlobStorageResource("blobs"));
		var queues = TechnologyCatalog.Detect(new AzureQueueStorageResource("queues"));
		var tables = TechnologyCatalog.Detect(new AzureTableStorageResource("tables"));

		// Assert
		await Assert.That(blobs?.Icon).IsEqualTo("azure:blob-block");
		await Assert.That(queues?.Icon).IsEqualTo("azure:storage-queue");
		await Assert.That(tables?.Icon).IsEqualTo("azure:table");
	}

	[Test]
	public async Task Detect_AzureResourceRunAsContainer_KeepsTheAzureServiceAndSaysSo()
	{
		// Arrange — the visible container stands in for the hidden Azure resource.
		var container = WithImage(new ContainerResource("azure-postgres"), "library/postgres:17");

		// Act
		var detected = TechnologyCatalog.Detect(container, new AzurePostgresFlexibleServerResource("azure-postgres"));

		// Assert
		await Assert
			.That(detected)
			.IsEqualTo(
				new KnownTechnology(
					"Azure Database for PostgreSQL (container)",
					"azure:azure-database-postgre-sql-server"
				)
			);
	}

	[Test]
	public async Task Detect_SubclassOfAKnownType_IsThatType()
	{
		// Act
		var detected = TechnologyCatalog.Detect(new CachingRedisResource("cache"));

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("Redis");
	}

	[Test]
	[Arguments("Azure.SqlServer", "azure:sql-server")]
	[Arguments("Azure.StorageAccount", "azure:storage-accounts")]
	[Arguments("Azure.CosmosDb", "azure:azure-cosmos-db")]
	public async Task Detect_SnapshotResourceType_IsRecognised(string resourceType, string icon)
	{
		// Arrange
		ContainerResource resource = new("thing");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = resourceType, Properties = [] })
		);

		// Act
		var detected = TechnologyCatalog.Detect(resource);

		// Assert
		await Assert.That(detected?.Icon).IsEqualTo(icon);
	}

	// Container images

	[Test]
	[Arguments("mcr.microsoft.com/mssql/server:2022-latest", "SQL Server", "tech:microsoft-sql-server")]
	[Arguments("docker.io/library/postgres:17.2", "PostgreSQL", "tech:postgresql")]
	[Arguments("bitnami/redis", "Redis", "tech:redis")]
	[Arguments("localhost:5000/nginx:1.27", "NGINX", "tech:nginx")]
	[Arguments("docker.io/confluentinc/confluent-local:7.8.0", "Apache Kafka", "tech:kafka")]
	[Arguments(
		"mcr.microsoft.com/azure-messaging/servicebus-emulator:latest",
		"Azure Service Bus (emulator)",
		"azure:azure-service-bus"
	)]
	[Arguments("pgvector/pgvector:pg17@sha256:0123", "PostgreSQL (pgvector)", "tech:postgresql")]
	public async Task Detect_ContainerImage_IsRecognised(string image, string name, string icon)
	{
		// Arrange
		var container = WithImage(new ContainerResource("thing"), image);

		// Act
		var detected = TechnologyCatalog.Detect(container);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology(name, icon));
	}

	[Test]
	public async Task Detect_TechnologyWithoutAnIcon_HasNoneRatherThanALookAlike()
	{
		// Fuzzy matching "seq" lands on Sequelize; knowing it is Seq means knowing there is no icon.
		// Act
		var detected = TechnologyCatalog.Detect(WithImage(new ContainerResource("seq"), "datalust/seq:2024.3"));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Seq", null));
	}

	[Test]
	public async Task Detect_UnknownImage_IsLeftToTheFuzzyMatcher()
	{
		// Act
		var detected = TechnologyCatalog.Detect(WithImage(new ContainerResource("thing"), "acme/widget-service:1"));

		// Assert
		await Assert.That(detected).IsNull();
	}

	[Test]
	[Arguments("mcr.microsoft.com/azure-storage/azurite:3", "azure-storage/azurite")]
	[Arguments("docker.io/library/postgres:17", "postgres")]
	[Arguments("library/redis", "redis")]
	[Arguments("localhost:5000/team/api:1.2@sha256:abc", "team/api")]
	[Arguments("Quay.io/Keycloak/Keycloak:26.0", "keycloak/keycloak")]
	public async Task ImagePath_StripsRegistryNamespaceTagAndDigest(string image, string path)
	{
		// Act & Assert
		await Assert.That(TechnologyCatalog.ImagePath(image)).IsEqualTo(path);
	}

	// Frameworks

	[Test]
	public async Task Detect_AstroSiteOnVite_IsAstro()
	{
		// Arrange — Astro builds with Vite, so both are dependencies; Astro is what the site is.
		using var project = CreatePackageJson("astro", "vite", "@astrojs/react", "react");

		// Act
		var detected = TechnologyCatalog.Detect(new ViteAppResource("site", project.Path));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Astro", "tech:astro"));
	}

	[Test]
	[Arguments(new[] { "next", "react", "react-dom" }, "Next.js")]
	[Arguments(new[] { "@sveltejs/kit", "svelte", "vite" }, "SvelteKit")]
	[Arguments(new[] { "express" }, "Express")]
	[Arguments(new[] { "react", "vite" }, "React")]
	[Arguments(new[] { "vite" }, "Vite")]
	public async Task Detect_PackageJson_NamesTheFrameworkOverWhatItBuildsOn(string[] dependencies, string name)
	{
		// Arrange
		using var project = CreatePackageJson(dependencies);

		// Act
		var detected = TechnologyCatalog.Detect(new ViteAppResource("site", project.Path));

		// Assert
		await Assert.That(detected?.Name).IsEqualTo(name);
	}

	[Test]
	public async Task Detect_ViteAppWithoutAPackageJson_IsVite()
	{
		// Act
		var detected = TechnologyCatalog.Detect(new ViteAppResource("site", Path.GetTempPath()));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Vite", "tech:vite"));
	}

	[Test]
	public async Task Detect_Installer_IsItsPackageManagerNotTheAppItInstallsFor()
	{
		// An installer runs the package manager, so that is what it is, whatever the app is built with.
		// Arrange
		using var project = CreatePackageJson("astro");

		// Act
		var detected = TechnologyCatalog.Detect(new JavaScriptInstallerResource("site-installer", project.Path));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("npm", "tech:npm"));
	}

	[Test]
	[Arguments("pnpm", "pnpm", "tech:pnpm")]
	[Arguments("yarn.cmd", "Yarn", "tech:yarn")]
	[Arguments("bun", "Bun", "tech:bun")]
	public async Task Detect_Installer_IsNamedByTheCommandItRuns(string command, string name, string icon)
	{
		// Arrange
		ExecutableResource installer = new JavaScriptInstallerCommandResource(
			"site-installer",
			command,
			Path.GetTempPath()
		);

		// Act
		var detected = TechnologyCatalog.Detect(installer);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology(name, icon));
	}

	// .NET projects

	[Test]
	public async Task Detect_WebSdkProject_IsAspNetCore()
	{
		// Arrange
		using var project = CreateDotnetProject(sdk: "Microsoft.NET.Sdk.Web");

		// Act
		var detected = TechnologyCatalog.Detect(
			CreateProjectResource("api", Path.Combine(project.Path, "Service.csproj"))
		);

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("ASP.NET Core", "tech:dotnet"));
	}

	[Test]
	public async Task Detect_AspNetCoreFrameworkReferenceInRestoreOutput_IsAspNetCore()
	{
		// An SDK such as Purview.BuildSdk adds the framework reference outside the project file, so the
		// restore output is the only place it shows.
		// Arrange
		using var project = CreateDotnetProject(
			assets: CreateAssets(frameworkReferences: "\"Microsoft.AspNetCore.App\": { \"privateAssets\": \"none\" }")
		);

		// Act
		var detected = TechnologyCatalog.Detect(
			CreateProjectResource("api", Path.Combine(project.Path, "Service.csproj"))
		);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("ASP.NET Core");
	}

	[Test]
	[Arguments("<PackageReference Include=\"Yarp.ReverseProxy\" />", "YARP")]
	[Arguments("<PackageReference Include=\"Grpc.AspNetCore\" />", "ASP.NET Core gRPC")]
	[Arguments("<PackageReference Include=\"Microsoft.Azure.Functions.Worker\" />", "Azure Functions")]
	public async Task Detect_WebProjectWithAFlavourPackage_IsThatFlavour(string items, string name)
	{
		// Arrange
		using var project = CreateDotnetProject(sdk: "Microsoft.NET.Sdk.Web", items: items);

		// Act
		var detected = TechnologyCatalog.Detect(
			CreateProjectResource("api", Path.Combine(project.Path, "Service.csproj"))
		);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo(name);
	}

	[Test]
	public async Task Detect_WorkerSdkProject_IsAWorkerService()
	{
		// Arrange
		using var project = CreateDotnetProject(sdk: "Microsoft.NET.Sdk.Worker/10.0.0");

		// Act
		var detected = TechnologyCatalog.Detect(
			CreateProjectResource("worker", Path.Combine(project.Path, "Service.csproj"))
		);

		// Assert
		await Assert.That(detected?.Name).IsEqualTo(".NET Worker Service");
	}

	[Test]
	public async Task Detect_PlainConsoleProject_IsLeftForTheDotnetFallback()
	{
		// Arrange
		using var project = CreateDotnetProject(assets: CreateAssets());

		// Act
		var detected = TechnologyCatalog.Detect(
			CreateProjectResource("tool", Path.Combine(project.Path, "Service.csproj"))
		);

		// Assert
		await Assert.That(detected).IsNull();
	}

	[Test]
	public async Task Detect_FileBasedAppDirectives_NameTheFlavour()
	{
		// Arrange
		using ProjectDirectory project = new(
			"proxy.cs",
			"#!/usr/bin/env dotnet\n#:sdk Microsoft.NET.Sdk.Web\n#:package Yarp.ReverseProxy@2.3.0\n\nvar app = 1;\n"
		);

		// Act
		var detected = TechnologyCatalog.Detect(CreateProjectResource("proxy", Path.Combine(project.Path, "proxy.cs")));

		// Assert
		await Assert.That(detected?.Name).IsEqualTo("YARP");
	}

	[Test]
	public async Task Build_PlainConsoleProject_StillShowsDotnet()
	{
		// Arrange
		using var project = CreateDotnetProject();

		// Act
		var element = ModelBuilder
			.Build([CreateProjectResource("tool", Path.Combine(project.Path, "Service.csproj"))])
			.Elements.Single();

		// Assert
		await Assert.That(element.Technology).IsEqualTo(".NET");
		await Assert.That(element.Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Detect_AppRunByBun_IsBunWhenNoFrameworkIsKnown()
	{
		// Arrange
		using var project = CreatePackageJson("left-pad");

		// Act
		var detected = TechnologyCatalog.Detect(new ExecutableResource("worker", "bun", project.Path));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("Bun", "tech:bun"));
	}

	[Test]
	public async Task Detect_PythonRequirements_NamesTheFramework()
	{
		// Arrange
		using ProjectDirectory project = new("requirements.txt", "uvicorn==0.30\nfastapi[standard]>=0.115\n");

		// Act
		var detected = TechnologyCatalog.Detect(new PythonAppResource("api", project.Path));

		// Assert
		await Assert.That(detected).IsEqualTo(new KnownTechnology("FastAPI", "tech:fastapi-icon"));
	}

	[Test]
	[Arguments("flask-cors==4.0\n", null)]
	[Arguments("Flask>=3.0\n", "Flask")]
	[Arguments("[project]\ndependencies = [\"django>=5.1\"]\n", "Django")]
	public async Task PythonFramework_MatchesAWholeDependencyName(string text, string? name)
	{
		// Act & Assert
		await Assert.That(TechnologyCatalog.PythonFramework(text)?.Name).IsEqualTo(name);
	}

	[Test]
	public async Task JavaScriptFramework_MalformedPackageJson_IsUnknown()
	{
		// Act & Assert
		await Assert.That(TechnologyCatalog.JavaScriptFramework("{ not json")).IsNull();
	}

	// Through the model builder

	[Test]
	public async Task Build_AzureStorage_ShowsTheServiceAndItsIcon()
	{
		// Act
		var element = ModelBuilder.Build([new AzureStorageResource("storage")]).Elements.Single();

		// Assert
		await Assert.That(element.Technology).IsEqualTo("Azure Storage");
		await Assert.That(element.Icon).IsEqualTo("azure:storage-accounts");
	}

	[Test]
	public async Task Build_AzureResourceRemovedByRunAsContainer_IsFoundThroughItsReferences()
	{
		// Aspire 17's RunAsContainer() takes the Azure resource out of the model, leaving a plain
		// PostgreSQL container under its name. A reference made before the swap still points at the
		// Azure resource, which is what says the container is standing in for it.
		// Arrange
		AzurePostgresFlexibleServerResource azure = new("azure-postgres");
		var container = WithImage(new ContainerResource("azure-postgres"), "library/postgres:17");
		ContainerResource api = new("api");
		api.Annotations.Add(new ResourceRelationshipAnnotation(azure, "Reference"));

		// Act
		var element = ModelBuilder.Build([container, api]).Elements.Single(e => e.Name == "azure-postgres");

		// Assert
		await Assert.That(element.Technology).IsEqualTo("Azure Database for PostgreSQL (container)");
		await Assert.That(element.Icon).IsEqualTo("azure:azure-database-postgre-sql-server");
	}

	[Test]
	public async Task Build_ExplicitTechnology_StillWins()
	{
		// Arrange
		AzureStorageResource storage = new("storage");
		storage.Annotations.Add(new LikeC4NodeDetailsAnnotation("storage").WithTechnology("Blob archive"));

		// Act
		var element = ModelBuilder.Build([storage]).Elements.Single();

		// Assert
		await Assert.That(element.Technology).IsEqualTo("Blob archive");
	}

	[Test]
	public async Task Build_ExplicitIcon_StillWins()
	{
		// Arrange
		AzureStorageResource storage = new("storage");
		storage.Annotations.Add(new LikeC4NodeDetailsAnnotation("storage").WithIcon("azure:storage-explorer"));

		// Act
		var element = ModelBuilder.Build([storage]).Elements.Single();

		// Assert
		await Assert.That(element.Icon).IsEqualTo("azure:storage-explorer");
	}
}
