using Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers how the ER diagram's image export invokes d2, for both the container and the local binary.
/// </summary>
/// <remarks>
/// Only the invocation is asserted, not the rendering: running d2 is covered by the integration suite,
/// and what can go wrong here is a mount, a path or a flag rather than the diagram itself.
/// </remarks>
public sealed class ErdImageExporterTests
{
	// Paths are derived from the platform's own root so the mount strings compare on Windows and Unix.
	static readonly string Root = Path.GetPathRoot(Path.GetFullPath("."))!
		.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

	static string P(params string[] segments) => Path.Combine([Root + Path.DirectorySeparatorChar, .. segments]);

	static AspireC4EfDiagramOptions CreateOptions(
		ErdRenderer renderer = ErdRenderer.Interactive,
		D2LayoutEngine layoutEngine = D2LayoutEngine.Elk,
		string? d2Command = null,
		string? containerImageTag = null,
		string fileName = "schema.gen"
	) =>
		new()
		{
			Renderer = renderer,
			LayoutEngine = layoutEngine,
			D2Command = d2Command,
			ContainerImageTag = containerImageTag,
			FileName = fileName,
		};

	// Tests

	[Test]
	[Arguments(ErdImageFormat.Svg, "svg")]
	[Arguments(ErdImageFormat.Png, "png")]
	public async Task Extension_ForAFormat_IsTheExtensionD2InfersTheFormatFrom(ErdImageFormat format, string expected)
	{
		// d2 picks its output format from the extension of the output path, so these are load-bearing.
		// Act
		var result = ErdImageExporter.Extension(format);

		// Assert
		await Assert.That(result).IsEqualTo(expected);
	}

	[Test]
	public async Task FormatsToExport_ByDefault_IsSvgThenPng()
	{
		// Arrange
		var options = CreateOptions();

		// Act
		var result = ErdImageExporter.FormatsToExport(options);

		// Assert
		await Assert.That(result).IsEquivalentTo([ErdImageFormat.Svg, ErdImageFormat.Png]);
	}

	[Test]
	public async Task FormatsToExport_FormatsAddedOutOfOrder_AreOrdered()
	{
		// A HashSet enumerates in insertion order, and configuration binding appends, so without the
		// sort the log output and the file order would depend on how the options were populated.
		// Arrange
		var options = CreateOptions();
		options.ImageFormats = [ErdImageFormat.Png, ErdImageFormat.Svg];

		// Act
		var result = ErdImageExporter.FormatsToExport(options);

		// Assert
		await Assert.That(result).IsEquivalentTo([ErdImageFormat.Svg, ErdImageFormat.Png]);
	}

	[Test]
	public async Task FormatsToExport_NoFormats_IsEmpty()
	{
		// Arrange
		var options = CreateOptions();
		options.ImageFormats = [];

		// Act
		var result = ErdImageExporter.FormatsToExport(options);

		// Assert
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task BuildInvocation_WithNoLocalCommand_RunsTheD2Container()
	{
		// Arrange
		var options = CreateOptions();
		var directory = P("apphost", ".aspirec4", "erd");

		// Act
		var (command, arguments) = ErdImageExporter.BuildInvocation(options, directory, ErdImageFormat.Svg);

		// Assert
		await Assert.That(command).IsEqualTo("docker");
		await Assert
			.That(arguments)
			.IsEquivalentTo([
				"run",
				"--rm",
				"-v",
				$"{directory}:/data",
				$"docker.io/d2lang/d2:{D2ServerResource.DefaultTag}",
				"--layout",
				"elk",
				"/data/schema.gen.d2",
				"/data/schema.gen.svg",
			]);
	}

	[Test]
	public async Task BuildInvocation_InContainerMode_MountsTheOutputDirectoryWritable()
	{
		// Unlike the sidecar's read-only mount: the whole point is to write the image back beside the
		// source, which a ":ro" suffix here would silently prevent.
		// Arrange
		var options = CreateOptions();
		var directory = P("apphost", ".aspirec4", "erd");

		// Act
		var (_, arguments) = ErdImageExporter.BuildInvocation(options, directory, ErdImageFormat.Png);

		// Assert
		await Assert.That(arguments).Contains($"{directory}:/data");
		await Assert.That(arguments).DoesNotContain($"{directory}:/data:ro");
	}

	[Test]
	public async Task BuildInvocation_PinnedContainerImageTag_IsUsed()
	{
		// Arrange
		var options = CreateOptions(containerImageTag: "v0.8.1");
		var directory = P("apphost", ".aspirec4", "erd");

		// Act
		var (_, arguments) = ErdImageExporter.BuildInvocation(options, directory, ErdImageFormat.Svg);

		// Assert
		await Assert.That(arguments).Contains("docker.io/d2lang/d2:v0.8.1");
	}

	[Test]
	public async Task BuildInvocation_CustomContainerRuntime_IsTheCommand()
	{
		// Arrange
		var options = CreateOptions();
		options.ContainerRuntimeCommand = "podman";

		// Act
		var (command, _) = ErdImageExporter.BuildInvocation(
			options,
			P("apphost", ".aspirec4", "erd"),
			ErdImageFormat.Svg
		);

		// Assert
		await Assert.That(command).IsEqualTo("podman");
	}

	[Test]
	public async Task BuildInvocation_WithLocalCommand_RunsTheBinaryWithHostPaths()
	{
		// Arrange
		var options = CreateOptions(d2Command: "d2");
		var directory = P("apphost", ".aspirec4", "erd");

		// Act
		var (command, arguments) = ErdImageExporter.BuildInvocation(options, directory, ErdImageFormat.Png);

		// Assert
		await Assert.That(command).IsEqualTo("d2");
		await Assert
			.That(arguments)
			.IsEquivalentTo([
				"--layout",
				"elk",
				Path.Combine(directory, "schema.gen.d2"),
				Path.Combine(directory, "schema.gen.png"),
			]);
	}

	[Test]
	[Arguments(D2LayoutEngine.Dagre, "dagre")]
	[Arguments(D2LayoutEngine.Elk, "elk")]
	[Arguments(D2LayoutEngine.Tala, "tala")]
	public async Task BuildInvocation_LayoutEngine_MatchesTheServedDiagram(D2LayoutEngine engine, string expected)
	{
		// An image laid out by a different engine from the one being served is a different diagram.
		// Arrange
		var options = CreateOptions(layoutEngine: engine, d2Command: "d2");

		// Act
		var (_, arguments) = ErdImageExporter.BuildInvocation(
			options,
			P("apphost", ".aspirec4", "erd"),
			ErdImageFormat.Svg
		);

		// Assert
		await Assert.That(arguments).Contains(expected);
	}

	[Test]
	public async Task BuildInvocation_CustomFileName_NamesBothTheSourceAndTheImage()
	{
		// Arrange
		var options = CreateOptions(d2Command: "d2", fileName: "my-schema");
		var directory = P("apphost", ".aspirec4", "erd");

		// Act
		var (_, arguments) = ErdImageExporter.BuildInvocation(options, directory, ErdImageFormat.Svg);

		// Assert
		await Assert.That(arguments).Contains(Path.Combine(directory, "my-schema.d2"));
		await Assert.That(arguments).Contains(Path.Combine(directory, "my-schema.svg"));
	}
}
