using Aspire.Hosting.AspireC4.ApplicationModel;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Covers how the architecture diagram's image export invokes <c>likec4 export</c>, for both the
/// one-shot container and the local CLI.
/// </summary>
/// <remarks>
/// Only the invocation is asserted, not the rendering. What can go wrong here is a mount, a container
/// path or a working directory — and a wrong working directory is the failure mode that matters, because
/// LikeC4 resolves the relative include paths and image aliases in <c>likec4.config.json</c> against it.
/// </remarks>
public sealed class LikeC4ImageExporterTests
{
	// Paths are derived from the platform's own root so the mount strings compare on Windows and Unix.
	static readonly string Root = Path.GetPathRoot(Path.GetFullPath("."))!
		.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

	static string P(params string[] segments) => Path.Combine([Root + Path.DirectorySeparatorChar, .. segments]);

	static AspireC4DiagramOptions CreateOptions(string? containerImageTag = null) =>
		new() { ContainerImageTag = containerImageTag };

	static string OutputDirectory() => P("apphost", ".aspirec4", "likec4");

	// Tests

	[Test]
	[Arguments(LikeC4ImageFormat.Png, "png")]
	[Arguments(LikeC4ImageFormat.Jpg, "jpg")]
	public async Task FormatName_ForAFormat_IsTheCLISubcommand(LikeC4ImageFormat format, string expected)
	{
		// Act
		var result = LikeC4ImageExporter.FormatName(format);

		// Assert
		await Assert.That(result).IsEqualTo(expected);
	}

	[Test]
	public async Task FormatsToExport_ByDefault_IsPngOnly()
	{
		// PNG alone by default: a JPEG of a diagram is all compression artefacts around the text.
		// Arrange
		var options = CreateOptions();

		// Act
		var result = LikeC4ImageExporter.FormatsToExport(options);

		// Assert
		await Assert.That(result).IsEquivalentTo([LikeC4ImageFormat.Png]);
	}

	[Test]
	public async Task FormatsToExport_NoFormats_IsEmpty()
	{
		// Arrange
		var options = CreateOptions();
		options.ImageFormats = [];

		// Act
		var result = LikeC4ImageExporter.FormatsToExport(options);

		// Assert
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task ResolveImageDirectory_ByDefault_IsImagesBeneathTheOutputDirectory()
	{
		// Arrange
		var options = CreateOptions();
		var outputDirectory = OutputDirectory();

		// Act
		var result = LikeC4ImageExporter.ResolveImageDirectory(options, outputDirectory);

		// Assert
		await Assert.That(result).IsEqualTo(Path.Combine(outputDirectory, "images"));
	}

	[Test]
	public async Task ResolveImageDirectory_RelativePath_IsResolvedAgainstTheOutputDirectory()
	{
		// Arrange
		var options = CreateOptions();
		options.ImageExportDirectory = "../diagrams";
		var outputDirectory = OutputDirectory();

		// Act
		var result = LikeC4ImageExporter.ResolveImageDirectory(options, outputDirectory);

		// Assert
		await Assert.That(result).IsEqualTo(P("apphost", ".aspirec4", "diagrams"));
	}

	[Test]
	public async Task ResolveImageDirectory_AbsolutePath_IsUsedAsGiven()
	{
		// Arrange
		var options = CreateOptions();
		var absolute = P("artifacts", "diagrams");
		options.ImageExportDirectory = absolute;

		// Act
		var result = LikeC4ImageExporter.ResolveImageDirectory(options, OutputDirectory());

		// Assert
		await Assert.That(result).IsEqualTo(absolute);
	}

	[Test]
	public async Task BuildInvocation_InContainerMode_RunsTheLikeC4Image()
	{
		// Arrange
		var options = CreateOptions();
		var outputDirectory = OutputDirectory();
		var imageDirectory = Path.Combine(outputDirectory, "images");

		// Act
		var (command, arguments, _) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			imageDirectory,
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(command).IsEqualTo("docker");
		await Assert.That(arguments).Contains("run");
		await Assert.That(arguments).Contains("--rm");
		await Assert.That(arguments).Contains($"ghcr.io/likec4/likec4:{LikeC4ServerResource.DefaultTag}");
		await Assert.That(arguments).Contains("export");
		await Assert.That(arguments).Contains("png");
	}

	[Test]
	public async Task BuildInvocation_InContainerMode_MountsSourcesReadOnlyAndImagesWritable()
	{
		// The source tree is read-only for the same reason the server's mount is: an export must not
		// write into the project it is rendering. The images therefore need their own writable mount.
		// Arrange
		var options = CreateOptions();
		var outputDirectory = OutputDirectory();
		var imageDirectory = Path.Combine(outputDirectory, "images");

		// Act
		var (_, arguments, workspaceRoot) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			imageDirectory,
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(workspaceRoot).IsEqualTo(outputDirectory);
		await Assert.That(arguments).Contains($"{outputDirectory}:/data:ro");
		await Assert.That(arguments).Contains($"{imageDirectory}:/out");
		await Assert.That(arguments).Contains("-o");
		await Assert.That(arguments).Contains("/out");
	}

	[Test]
	public async Task BuildInvocation_InContainerMode_WorkingDirectoryIsTheOutputDirectoryInsideTheContainer()
	{
		// LikeC4 resolves the relative include paths and image aliases in likec4.config.json against the
		// process working directory. Point it anywhere else and those paths climb out of the bind mount,
		// which fails as an ENOENT on an icon rather than as anything that names the real cause.
		// Arrange
		var options = CreateOptions();
		var serveDirectory = P("apphost", "likec4");
		var outputDirectory = Path.Combine(serveDirectory, ".aspirec4");
		var imageDirectory = Path.Combine(outputDirectory, "images");

		// Act
		var (_, arguments, workspaceRoot) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			serveDirectory,
			imageDirectory,
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(workspaceRoot).IsEqualTo(serveDirectory);
		await Assert.That(arguments).Contains("--workdir");
		await Assert.That(arguments).Contains("/data/.aspirec4");
	}

	[Test]
	public async Task BuildInvocation_InContainerMode_HandAuthoredProject_ExportsTheServedProject()
	{
		// With a hand-authored project the served root is an ancestor of the generated output, and it is
		// the project — not the generated subfolder — whose views are exported.
		// Arrange
		var options = CreateOptions();
		var serveDirectory = P("apphost", "likec4");
		var outputDirectory = Path.Combine(serveDirectory, ".aspirec4");

		// Act
		var (_, arguments, _) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			serveDirectory,
			Path.Combine(outputDirectory, "images"),
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(arguments[^1]).IsEqualTo("/data");
	}

	[Test]
	public async Task BuildInvocation_InContainerMode_AdditionalDSLFolder_WidensTheMountToCoverIt()
	{
		// The mount has to reach every directory the config refers to, exactly as the server's does.
		// Arrange
		var options = CreateOptions();
		options.AdditionalDSLFolders.Add(P("apphost", "extensions"));
		var outputDirectory = OutputDirectory();

		// Act
		var (_, arguments, workspaceRoot) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			Path.Combine(outputDirectory, "images"),
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(workspaceRoot).IsEqualTo(P("apphost"));
		await Assert.That(arguments).Contains($"{P("apphost")}:/data:ro");
		await Assert.That(arguments).Contains("/data/.aspirec4/likec4");
	}

	[Test]
	public async Task BuildInvocation_PinnedContainerImageTag_IsUsed()
	{
		// Arrange
		var options = CreateOptions(containerImageTag: "1.57");
		var outputDirectory = OutputDirectory();

		// Act
		var (_, arguments, _) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			Path.Combine(outputDirectory, "images"),
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(arguments).Contains("ghcr.io/likec4/likec4:1.57");
	}

	[Test]
	public async Task BuildInvocation_CustomContainerRuntime_IsTheCommand()
	{
		// Arrange
		var options = CreateOptions();
		options.ContainerRuntimeCommand = "podman";
		var outputDirectory = OutputDirectory();

		// Act
		var (command, _, _) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			Path.Combine(outputDirectory, "images"),
			LikeC4ImageFormat.Png,
			localCLI: null
		);

		// Assert
		await Assert.That(command).IsEqualTo("podman");
	}

	[Test]
	public async Task BuildInvocation_InLocalCLIMode_RunsTheHostCLIFromTheOutputDirectory()
	{
		// Arrange
		var options = CreateOptions();
		var outputDirectory = OutputDirectory();
		var imageDirectory = Path.Combine(outputDirectory, "images");

		// Act
		var (command, arguments, workingDirectory) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			imageDirectory,
			LikeC4ImageFormat.Jpg,
			localCLI: ("npx", ["likec4"])
		);

		// Assert
		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(workingDirectory).IsEqualTo(outputDirectory);
		await Assert.That(arguments[0]).IsEqualTo("likec4");
		await Assert.That(arguments[1]).IsEqualTo("export");
		await Assert.That(arguments[2]).IsEqualTo("jpg");
		await Assert.That(arguments).Contains(imageDirectory);
		await Assert.That(arguments[^1]).IsEqualTo(outputDirectory);
		// No container is involved, so nothing should have been mounted.
		await Assert.That(arguments).DoesNotContain("run");
	}

	[Test]
	public async Task BuildInvocation_AlwaysFlattensAndToleratesAFailedView()
	{
		// --flat keeps an image's name tied to its view rather than to where its .c4 file happened to
		// live; -i stops one unrenderable view discarding the ones that rendered.
		// Arrange
		var options = CreateOptions();
		var outputDirectory = OutputDirectory();

		// Act
		var (_, arguments, _) = LikeC4ImageExporter.BuildInvocation(
			options,
			outputDirectory,
			outputDirectory,
			Path.Combine(outputDirectory, "images"),
			LikeC4ImageFormat.Png,
			localCLI: ("npx", ["likec4"])
		);

		// Assert
		await Assert.That(arguments).Contains("--flat");
		await Assert.That(arguments).Contains("-i");
	}
}
