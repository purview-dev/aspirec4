using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4.LikeC4;

public sealed class ResourceSourceMetadataTests
{
	// Helpers

	static string CreateAppHostDirectory()
	{
		var root = Path.Combine(Path.GetTempPath(), "aspirec4-source-" + Guid.NewGuid().ToString("N")[..8]);
		var appHost = Path.Combine(root, "AppHost");
		Directory.CreateDirectory(appHost);
		return appHost;
	}

	static string CreateFile(string directory, string relativePath, string content)
	{
		var path = Path.GetFullPath(Path.Combine(directory, relativePath));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
		return path;
	}

	static ProjectResource CreateProjectResource(string name, string? projectPath = null)
	{
		ProjectResource resource = new(name);
		if (projectPath is not null)
		{
			resource.Annotations.Add(new TestProjectMetadata(projectPath));
		}

		return resource;
	}

	static ExecutableResource CreateExecutableResource(string name, string workingDirectory) =>
		new(name, "node", workingDirectory);

	static ContainerResource CreateContainerResource(
		string name,
		string image = "mssql/server",
		string? registry = "mcr.microsoft.com",
		string? tag = "2025-latest",
		string? sha256 = null
	)
	{
		ContainerResource resource = new(name);
		ContainerImageAnnotation annotation = new()
		{
			Image = image,
			Registry = registry,
			Tag = tag,
		};

		// Aspire clears the tag whenever SHA256 is assigned, even to null, so assign it only when pinning.
		if (sha256 is not null)
		{
			annotation.SHA256 = sha256;
		}

		resource.Annotations.Add(annotation);
		return resource;
	}

	static string? ValueOf(IReadOnlyList<Models.LikeC4Metadata> metadata, string key) =>
		metadata.FirstOrDefault(m => m.Key == key)?.Value;

	const string ProjectFile = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net10.0</TargetFramework>
		  </PropertyGroup>
		</Project>
		""";

	// .NET projects

	[Test]
	public async Task Read_DotnetProject_RecordsTheProjectPathRelativeToTheAppHost()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var projectPath = CreateFile(appHost, "../Api/Api.csproj", ProjectFile);
		var resource = CreateProjectResource("api", projectPath);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ProjectPathKey)).IsEqualTo("../Api/Api.csproj");
	}

	[Test]
	public async Task Read_DotnetProjectWithoutRestoreOutput_ReadsTheTargetFrameworkFromTheProjectFile()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var projectPath = CreateFile(appHost, "../Api/Api.csproj", ProjectFile);
		var resource = CreateProjectResource("api", projectPath);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.DotnetTargetFrameworkKey)).IsEqualTo("net10.0");
	}

	[Test]
	public async Task Read_DotnetProjectWithRestoreOutput_PrefersTheRestoredTargetFrameworks()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var projectPath = CreateFile(
			appHost,
			"../Api/Api.csproj",
			"""<Project Sdk="Purview.BuildSdk"><PropertyGroup><TargetFrameworks>$(PurviewCurrent)</TargetFrameworks></PropertyGroup></Project>"""
		);
		CreateFile(
			appHost,
			"../Api/obj/project.assets.json",
			"""{ "project": { "restore": { "originalTargetFrameworks": [ "net10.0", "net11.0" ] }, "frameworks": { "net10.0": {}, "net11.0": {} } } }"""
		);
		var resource = CreateProjectResource("api", projectPath);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert
			.That(ValueOf(metadata, ResourceSourceMetadata.DotnetTargetFrameworkKey))
			.IsEqualTo("net10.0, net11.0");
	}

	[Test]
	public async Task Read_DotnetProjectWithOnlyAPropertyReference_OmitsTheTargetFramework()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var projectPath = CreateFile(
			appHost,
			"../Api/Api.csproj",
			"""<Project><PropertyGroup><TargetFramework>$(DefaultTfm)</TargetFramework></PropertyGroup></Project>"""
		);
		var resource = CreateProjectResource("api", projectPath);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.DotnetTargetFrameworkKey)).IsNull();
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ProjectPathKey)).IsNotNull();
	}

	[Test]
	public async Task Read_DotnetProjectWithMalformedFiles_StillRecordsThePath()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var projectPath = CreateFile(appHost, "../Api/Api.csproj", "<Project><not-closed>");
		CreateFile(appHost, "../Api/obj/project.assets.json", "{ not json");
		var resource = CreateProjectResource("api", projectPath);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(metadata).Count().IsEqualTo(1);
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ProjectPathKey)).IsEqualTo("../Api/Api.csproj");
	}

	[Test]
	public async Task Read_FileBasedApp_ReadsTheTargetFrameworkDirective()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var sourcePath = CreateFile(
			appHost,
			"../tools/seed.cs",
			"#!/usr/bin/env dotnet\n#:property TargetFramework=net11.0\n\nConsole.WriteLine();\n"
		);
		var resource = CreateProjectResource("seed", sourcePath);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.DotnetTargetFrameworkKey)).IsEqualTo("net11.0");
	}

	[Test]
	public async Task Read_ProjectResourceWithoutProjectMetadata_ReturnsNothing()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var resource = CreateProjectResource("api");

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(metadata).IsEmpty();
	}

	// Node, Python and Go projects

	[Test]
	public async Task Read_NodeProject_RecordsTheDirectoryEngineAndPackageManager()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(
			appHost,
			"../web/package.json",
			"""{ "name": "web", "engines": { "node": ">=22" }, "packageManager": "pnpm@9.12.0+sha512.abc", }"""
		);
		var resource = CreateExecutableResource("web", Path.Combine(appHost, "..", "web"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ProjectPathKey)).IsEqualTo("../web");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.NodeVersionKey)).IsEqualTo(">=22");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.PackageManagerKey)).IsEqualTo("pnpm@9.12.0");
	}

	[Test]
	public async Task Read_BunProject_RecordsTheBunEngine()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(
			appHost,
			"../web/package.json",
			"""{ "engines": { "bun": ">=1.2" }, "packageManager": "bun@1.4.2" }"""
		);
		var resource = CreateExecutableResource("web", Path.Combine(appHost, "..", "web"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.BunVersionKey)).IsEqualTo(">=1.2");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.NodeVersionKey)).IsNull();
	}

	[Test]
	public async Task Read_NodeProjectWithNvmrc_PrefersTheNvmrcVersion()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(appHost, "../web/package.json", """{ "engines": { "node": ">=20" } }""");
		CreateFile(appHost, "../web/.nvmrc", "22.11.0\n");
		var resource = CreateExecutableResource("web", Path.Combine(appHost, "..", "web"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.NodeVersionKey)).IsEqualTo("22.11.0");
	}

	[Test]
	public async Task Read_PythonProject_ReadsThePythonVersionFile()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(appHost, "../worker/pyproject.toml", "[project]\nrequires-python = \">=3.12\"\n");
		CreateFile(appHost, "../worker/.python-version", "3.13\n");
		var resource = CreateExecutableResource("worker", Path.Combine(appHost, "..", "worker"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.PythonVersionKey)).IsEqualTo("3.13");
	}

	[Test]
	public async Task Read_PythonProjectWithoutVersionFile_ReadsRequiresPython()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(appHost, "../worker/pyproject.toml", "[project]\nname = \"worker\"\nrequires-python = \">=3.12\"\n");
		var resource = CreateExecutableResource("worker", Path.Combine(appHost, "..", "worker"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.PythonVersionKey)).IsEqualTo(">=3.12");
	}

	[Test]
	public async Task Read_GoModuleWithToolchain_PrefersTheToolchainVersion()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(appHost, "../svc/go.mod", "module example.com/svc\n\ngo 1.23\n\ntoolchain go1.24.2\n");
		var resource = CreateExecutableResource("svc", Path.Combine(appHost, "..", "svc"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.GoVersionKey)).IsEqualTo("1.24.2");
	}

	[Test]
	public async Task Read_GoModuleWithoutToolchain_ReadsTheGoDirective()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		CreateFile(appHost, "../svc/go.mod", "module example.com/svc\n\ngo 1.23\n");
		var resource = CreateExecutableResource("svc", Path.Combine(appHost, "..", "svc"));

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.GoVersionKey)).IsEqualTo("1.23");
	}

	[Test]
	public async Task Read_ExecutableWithoutAProjectMarker_ReturnsNothing()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var resource = CreateExecutableResource("tool", appHost);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(metadata).IsEmpty();
	}

	// Containers

	[Test]
	public async Task Read_Container_RecordsTheFullImageReferenceAndTag()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var resource = CreateContainerResource("sql");

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert
			.That(ValueOf(metadata, ResourceSourceMetadata.ContainerImageKey))
			.IsEqualTo("mcr.microsoft.com/mssql/server:2025-latest");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ContainerTagKey)).IsEqualTo("2025-latest");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ContainerDigestKey)).IsNull();
	}

	[Test]
	public async Task Read_ContainerWithoutRegistryOrTag_RecordsTheBareImage()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var resource = CreateContainerResource("cache", image: "redis", registry: null, tag: null);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ContainerImageKey)).IsEqualTo("redis");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ContainerTagKey)).IsNull();
	}

	[Test]
	public async Task Read_ContainerPinnedByDigest_RecordsTheDigestReference()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var resource = CreateContainerResource(
			"cache",
			image: "redis",
			registry: "docker.io",
			tag: null,
			sha256: "abc123"
		);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert
			.That(ValueOf(metadata, ResourceSourceMetadata.ContainerImageKey))
			.IsEqualTo("docker.io/redis@sha256:abc123");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ContainerDigestKey)).IsEqualTo("sha256:abc123");
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.ContainerTagKey)).IsNull();
	}

	[Test]
	public async Task Read_ContainerBuiltFromADockerfile_RecordsTheDockerfilePath()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		var dockerfile = CreateFile(appHost, "../gateway/Dockerfile", "FROM nginx\n");
		var resource = CreateContainerResource("gateway", image: "gateway-image", registry: null, tag: "latest");
		resource.Annotations.Add(
			new DockerfileBuildAnnotation(Path.GetDirectoryName(dockerfile)!, dockerfile, stage: null)
		);

		// Act
		var metadata = ResourceSourceMetadata.Read(resource, appHost);

		// Assert
		await Assert.That(ValueOf(metadata, ResourceSourceMetadata.DockerfileKey)).IsEqualTo("../gateway/Dockerfile");
	}

	// ReadAll

	[Test]
	public async Task ReadAll_HiddenOriginalAndVisibleSurrogate_KeepsTheEntryWithMetadata()
	{
		// Arrange
		var appHost = CreateAppHostDirectory();
		ExecutableResource hidden = new("cache", "unused", Path.Combine(appHost, "missing"));
		var surrogate = CreateContainerResource("cache", image: "redis", registry: null, tag: "8.0");

		// Act
		var all = ResourceSourceMetadata.ReadAll([hidden, surrogate], appHost);

		// Assert
		await Assert.That(ValueOf(all["cache"], ResourceSourceMetadata.ContainerImageKey)).IsEqualTo("redis:8.0");
	}

	sealed class TestProjectMetadata(string projectPath) : IProjectMetadata
	{
		public string ProjectPath { get; } = projectPath;
	}
}
