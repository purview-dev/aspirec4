namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers how the opt-in viewer-fidelity export invokes the browser container.
/// </summary>
/// <remarks>
/// The rendering itself is covered by the integration suite; what is asserted here is the invocation,
/// because a wrong mount or a missing script argument fails in a way that looks like an empty diagram.
/// </remarks>
public sealed class ErdViewerImageExporterTests
{
	static readonly string Root = Path.GetPathRoot(Path.GetFullPath("."))!
		.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

	static string P(params string[] segments) => Path.Combine([Root + Path.DirectorySeparatorChar, .. segments]);

	static AspireC4EfDiagramOptions CreateOptions(string? viewerExportImage = null, string fileName = "schema.gen") =>
		new()
		{
			Renderer = ErdRenderer.Interactive,
			UseViewerFidelityExport = true,
			ViewerExportImage = viewerExportImage,
			FileName = fileName,
		};

	static (string Command, IReadOnlyList<string> Arguments) Invoke(
		AspireC4EfDiagramOptions options,
		params ErdImageFormat[] formats
	) =>
		ErdViewerImageExporter.BuildInvocation(
			options,
			P("apphost", ".aspirec4", "erd"),
			P("apphost", ".aspirec4", "images"),
			P("tmp", "viewer-export.mjs"),
			formats.Length == 0 ? [ErdImageFormat.Svg] : formats
		);

	// Tests

	[Test]
	public async Task ImageReference_ByDefault_IsThePinnedPlaywrightImage()
	{
		// Pinned so the browser rendering an export is the browser the viewer was verified in.
		// Act
		var result = ErdViewerImageExporter.ImageReference(CreateOptions());

		// Assert
		await Assert
			.That(result)
			.IsEqualTo($"{ErdViewerImageExporter.DefaultImage}:{ErdViewerImageExporter.DefaultTag}");
	}

	[Test]
	public async Task ImageReference_WithAnOverride_UsesItVerbatim()
	{
		// Including its tag: an override naming a mirror would be broken by appending the default one.
		// Act
		var result = ErdViewerImageExporter.ImageReference(
			CreateOptions(viewerExportImage: "internal.registry/playwright:v1.56.0")
		);

		// Assert
		await Assert.That(result).IsEqualTo("internal.registry/playwright:v1.56.0");
	}

	[Test]
	public async Task BuildInvocation_MountsTheViewerReadOnlyAndImagesWritable()
	{
		// The viewer is only read; the images directory is the only thing the container may write to.
		// Act
		var (command, arguments) = Invoke(CreateOptions());

		// Assert
		await Assert.That(command).IsEqualTo("docker");
		await Assert
			.That(arguments)
			.Contains($"type=bind,source={P("apphost", ".aspirec4", "erd")},target=/data,readonly");
		await Assert.That(arguments).Contains($"type=bind,source={P("apphost", ".aspirec4", "images")},target=/out");
	}

	[Test]
	public async Task BuildInvocation_MountsTheScriptAsASingleReadOnlyFile()
	{
		// A file mount rather than a directory one, so the container sees the script and nothing else.
		// Act
		var (_, arguments) = Invoke(CreateOptions());

		// Assert
		await Assert
			.That(arguments)
			.Contains($"type=bind,source={P("tmp", "viewer-export.mjs")},target=/srv/viewer-export.mjs,readonly");
		await Assert.That(arguments).Contains("node");
		await Assert.That(arguments).Contains("/srv/viewer-export.mjs");
	}

	[Test]
	public async Task BuildInvocation_PassesEveryFormatToOneInvocation()
	{
		// One browser session renders both files: starting it and laying the diagram out again is what
		// the export actually costs, and the viewer can produce both from a single render.
		// Act
		var (_, arguments) = Invoke(CreateOptions(), ErdImageFormat.Svg, ErdImageFormat.Png);

		// Assert
		await Assert.That(arguments).Contains("svg,png");
		await Assert.That(arguments.Count(a => a == "node")).IsEqualTo(1);
	}

	[Test]
	public async Task BuildInvocation_PassesTheImageBaseNameWithoutTheGeneratedMarker()
	{
		// The script names the file, so it needs the same base name the d2 path would have produced.
		// Act
		var (_, arguments) = Invoke(CreateOptions());

		// Assert
		await Assert.That(arguments).Contains("schema");
		await Assert.That(arguments).DoesNotContain("schema.gen");
	}

	[Test]
	public async Task BuildInvocation_CustomContainerRuntime_IsTheCommand()
	{
		// Arrange
		var options = CreateOptions();
		options.ContainerRuntimeCommand = "podman";

		// Act
		var (command, _) = Invoke(options);

		// Assert
		await Assert.That(command).IsEqualTo("podman");
	}
}
