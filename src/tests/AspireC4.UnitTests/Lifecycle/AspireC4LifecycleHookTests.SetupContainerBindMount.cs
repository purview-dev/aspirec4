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
