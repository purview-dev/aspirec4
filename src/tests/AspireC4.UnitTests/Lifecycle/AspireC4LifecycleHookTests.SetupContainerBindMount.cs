using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4.Lifecycle;

public sealed partial class AspireC4LifecycleHookTests
{
	[Test]
	public async Task SetupContainerBindMount_NoExtrasConfigured_BindsMountAtOutputDir()
	{
		// Arrange
		var outputDir = P("project", "apphost", "likec4", "gen");
		AspireC4DiagramOptions diagramOptions = new() { OutputDirectory = outputDir };
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert
		var mount = serverResource.Annotations.OfType<ContainerMountAnnotation>().Single();
		await Assert.That(mount.Source).IsEqualTo(outputDir);
	}

	[Test]
	public async Task SetupContainerBindMount_WithAdditionalDSLFolder_ExpandsBoundMountToCommonAncestor()
	{
		// Arrange
		// extensions/ is a sibling of gen/ — the bind mount must expand to the parent likec4/.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");
		AspireC4DiagramOptions diagramOptions = new()
		{
			OutputDirectory = outputDir,
			AdditionalDSLFolders = [dslFolder],
		};
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert
		var mount = serverResource.Annotations.OfType<ContainerMountAnnotation>().Single();
		var expectedAncestor = P("project", "apphost", "likec4");
		await Assert.That(mount.Source).IsEqualTo(expectedAncestor);
	}

	[Test]
	public async Task SetupContainerBindMount_WithImageAliasFolder_ExpandsBoundMountToCommonAncestor()
	{
		// Arrange
		// images/ is a sibling of gen/ — the bind mount must expand to the parent likec4/.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var imagesFolder = P("project", "apphost", "likec4", "images");
		AspireC4DiagramOptions diagramOptions = new()
		{
			OutputDirectory = outputDir,
			ImageAliases = new() { ["@"] = imagesFolder },
		};
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert
		var mount = serverResource.Annotations.OfType<ContainerMountAnnotation>().Single();
		var expectedAncestor = P("project", "apphost", "likec4");
		await Assert.That(mount.Source).IsEqualTo(expectedAncestor);
	}

	[Test]
	public async Task SetupContainerBindMount_WithDSLAndImageFolders_ExpandsBoundMountToLeastCommonAncestor()
	{
		// Arrange
		// Both extensions/ and images/ are siblings of gen/ — mount expands to likec4/.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");
		var imagesFolder = P("project", "apphost", "likec4", "images");
		AspireC4DiagramOptions diagramOptions = new()
		{
			OutputDirectory = outputDir,
			AdditionalDSLFolders = [dslFolder],
			ImageAliases = new() { ["@"] = imagesFolder },
		};
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert
		var mount = serverResource.Annotations.OfType<ContainerMountAnnotation>().Single();
		var expectedAncestor = P("project", "apphost", "likec4");
		await Assert.That(mount.Source).IsEqualTo(expectedAncestor);
	}

	[Test]
	public async Task SetupContainerBindMount_SetsContainerServePath_RelativeToCommonAncestor()
	{
		// Arrange
		// extensions/ forces the mount to expand to likec4/; ContainerServePath should be
		// /data/gen (relative path from likec4/ to gen/ inside the container workspace).
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");
		AspireC4DiagramOptions diagramOptions = new()
		{
			OutputDirectory = outputDir,
			AdditionalDSLFolders = [dslFolder],
		};
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert — the serve path is /data/gen (gen/ relative to the bind-mount root likec4/)
		await Assert.That(workspaceOptions.ContainerServePath).IsEqualTo("/data/gen");
	}

	[Test]
	public async Task SetupContainerBindMount_SetsContainerWorkDirToServePath()
	{
		// Arrange
		// extensions/ forces the mount to expand to likec4/; the serve path is /data/gen and the
		// container must run `likec4 start` with that directory as its working directory so that
		// config-relative paths (imageAliases, include.paths) resolve the same way they do on the
		// host (where the LikeC4 tooling runs with cwd = output directory).
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");
		AspireC4DiagramOptions diagramOptions = new()
		{
			OutputDirectory = outputDir,
			AdditionalDSLFolders = [dslFolder],
		};
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert
		var annotation = serverResource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>().Single();
		List<object> args = [];
		await annotation.Callback(new ContainerRuntimeArgsCallbackContext(args));

		await Assert.That(args).Count().IsEqualTo(2);
		await Assert.That(args[0]).IsEqualTo("--workdir");
		await Assert.That(args[1]).IsEqualTo("/data/gen");
		await Assert.That(args[1]).IsEqualTo(workspaceOptions.ContainerServePath);
	}

	[Test]
	public async Task SetupContainerBindMount_ConfigRelativePathsResolveInsideContainerWorkspace()
	{
		// Arrange
		// images/ and extensions/ are siblings of gen/ — the mount expands to likec4/ and the
		// serve path is /data/gen. This regression test mirrors LikeC4's resolution of the config's
		// relative paths (imageAliases, include.paths) from the container's working directory.
		// Because the workdir is now the serve path, "../images" resolves to /data/images — inside
		// the bind-mount root. Before the workdir override, the process ran from the image WORKDIR
		// (/data) and the paths escaped above the mount root (e.g. /assets/images/...), which is the
		// ENOENT seen when the webcomponent build tried to open the icon.
		var outputDir = P("project", "apphost", "likec4", "gen");
		var dslFolder = P("project", "apphost", "likec4", "extensions");
		var imagesFolder = P("project", "apphost", "likec4", "images");
		AspireC4DiagramOptions diagramOptions = new()
		{
			OutputDirectory = outputDir,
			AdditionalDSLFolders = [dslFolder],
			ImageAliases = new() { ["@"] = imagesFolder },
		};
		ContainerWorkspaceOptions workspaceOptions = new();
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(diagramOptions: diagramOptions, workspaceOptions: workspaceOptions);

		// Act — compute the config's relative paths exactly as WriteConfigFileAsync does, then
		// resolve them from the container working directory like LikeC4 does.
		sut.SetupContainerBindMount(null!, serverResource);
		var workdir = workspaceOptions.ContainerServePath;
		var includePaths = diagramOptions
			.AdditionalDSLFolders.Select(folder => Path.GetRelativePath(outputDir, folder).Replace('\\', '/'))
			.ToList();
		var aliasPaths = diagramOptions
			.ImageAliases.Values.Select(folder => Path.GetRelativePath(outputDir, folder).Replace('\\', '/'))
			.ToList();

		// Assert — every config-relative path, resolved from the container workdir, stays within
		// the bind-mount root (/data); none may escape above it.
		foreach (var relative in includePaths.Concat(aliasPaths))
		{
			var resolved = ResolvePosix(workdir, relative);
			await Assert.That(resolved).StartsWith(LikeC4ServerResource.WorkspacePath + "/");
		}
	}

	/// <summary>
	/// Resolves a POSIX-style relative path against a base path, mirroring Node's
	/// <c>path.resolve</c> semantics (the resolution LikeC4 applies at runtime inside the container).
	/// Traversal above the root is clamped to the root.
	/// </summary>
	static string ResolvePosix(string basePath, string relative)
	{
		var parts = basePath.Split('/').Where(segment => segment.Length > 0).ToList();
		foreach (var segment in relative.Split('/'))
		{
			switch (segment)
			{
				case "" or ".":
					break;
				case ".." when parts.Count > 0:
					parts.RemoveAt(parts.Count - 1);
					break;
				case "..":
					break;
				default:
					parts.Add(segment);
					break;
			}
		}
		return "/" + string.Join("/", parts);
	}

	static AspireC4LifecycleHook CreateLifecycleHookSut(
		AspireC4DiagramOptions? diagramOptions = null,
		ContainerWorkspaceOptions? workspaceOptions = null,
		IAspireC4LifecycleHookTelemetry? telemetry = null
	)
	{
		return new AspireC4LifecycleHook(
			Options.Create(diagramOptions ?? new AspireC4DiagramOptions()),
			new OptionsWrapper<ContainerWorkspaceOptions>(workspaceOptions ?? new ContainerWorkspaceOptions()),
			null!,
			null!,
			telemetry ?? IAspireC4LifecycleHookTelemetry.Mock(),
			null!
		);
	}
}
