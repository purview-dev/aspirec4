namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Tests for the viewer's embedded asset handling.
/// </summary>
/// <remarks>
/// The staleness behaviour is the point of these: the generated <c>app.js</c> imports named exports
/// from <c>vendor/bundle.js</c>, so a bundle left behind by an earlier package version fails the
/// import and the viewer renders a blank page.
/// </remarks>
public sealed class InteractiveViewerTests
{
	// Helpers

	static string CreateDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), "aspirec4-viewer-" + Guid.NewGuid().ToString("n"));

		Directory.CreateDirectory(directory);

		return directory;
	}

	static string VendorPath(string directory, string name) => Path.Combine(directory, "vendor", name);

	// Tests

	[Test]
	public async Task WriteVendorAssetsAsync_EmptyDirectory_WritesTheInteractiveDependencies(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var directory = CreateDirectory();

		// Act
		var written = await InteractiveViewer.WriteVendorAssetsAsync(
			directory,
			ErdRenderer.Interactive,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEqualTo(2);
		await Assert.That(File.Exists(VendorPath(directory, "bundle.js"))).IsTrue();
		await Assert.That(File.Exists(VendorPath(directory, "reactflow.css"))).IsTrue();
		await Assert.That(File.Exists(VendorPath(directory, InteractiveViewer.VendorStampFileName))).IsTrue();
	}

	[Test]
	public async Task WriteVendorAssetsAsync_AlreadyCurrent_WritesNothing(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();

		await InteractiveViewer.WriteVendorAssetsAsync(directory, ErdRenderer.Interactive, cancellationToken);

		// Act
		var written = await InteractiveViewer.WriteVendorAssetsAsync(
			directory,
			ErdRenderer.Interactive,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEqualTo(0);
	}

	[Test]
	public async Task WriteVendorAssetsAsync_StaleBundleFromAnEarlierVersion_RewritesIt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var directory = CreateDirectory();

		Directory.CreateDirectory(Path.Combine(directory, "vendor"));

		await File.WriteAllTextAsync(
			VendorPath(directory, "bundle.js"),
			"export const stale = true;",
			cancellationToken
		);
		await File.WriteAllTextAsync(VendorPath(directory, "reactflow.css"), ".stale {}", cancellationToken);

		// Act
		var written = await InteractiveViewer.WriteVendorAssetsAsync(
			directory,
			ErdRenderer.Interactive,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEqualTo(2);

		var bundle = await File.ReadAllTextAsync(VendorPath(directory, "bundle.js"), cancellationToken);

		await Assert.That(bundle).DoesNotContain("export const stale = true;");
		// The exports the current app.js imports, and the ones the pre-13.6 bundle was missing.
		await Assert.That(bundle).Contains("toSvg");
		await Assert.That(bundle).Contains("useReactFlow");
	}

	[Test]
	public async Task WriteVendorAssetsAsync_StampWithoutTheFiles_RewritesThem(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();

		await InteractiveViewer.WriteVendorAssetsAsync(directory, ErdRenderer.Interactive, cancellationToken);

		File.Delete(VendorPath(directory, "bundle.js"));

		// Act
		var written = await InteractiveViewer.WriteVendorAssetsAsync(
			directory,
			ErdRenderer.Interactive,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEqualTo(2);
		await Assert.That(File.Exists(VendorPath(directory, "bundle.js"))).IsTrue();
	}

	[Test]
	public async Task WriteVendorAssetsAsync_MermaidRenderer_WritesOnlyTheMermaidBundle(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var directory = CreateDirectory();

		// Act
		var written = await InteractiveViewer.WriteVendorAssetsAsync(directory, ErdRenderer.Mermaid, cancellationToken);

		// Assert
		await Assert.That(written).IsEqualTo(1);
		await Assert.That(File.Exists(VendorPath(directory, "mermaid.js"))).IsTrue();
		await Assert.That(File.Exists(VendorPath(directory, "bundle.js"))).IsFalse();
	}

	[Test]
	public async Task WriteVendorAssetsAsync_SwitchingRenderer_DoesNotTrustTheOtherRenderersStamp(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var directory = CreateDirectory();

		await InteractiveViewer.WriteVendorAssetsAsync(directory, ErdRenderer.Mermaid, cancellationToken);

		// Act
		var written = await InteractiveViewer.WriteVendorAssetsAsync(
			directory,
			ErdRenderer.Interactive,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEqualTo(2);
	}
}
