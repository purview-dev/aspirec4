namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Supplies the static assets for the generated viewers.
/// </summary>
/// <remarks>
/// The assets are embedded rather than written as C# string literals so they stay readable, lintable
/// and editable as real files. They are emitted beside the diagram, which keeps a viewer working with
/// no server and no additional Aspire resource.
/// </remarks>
public static class InteractiveViewer
{
	const string Prefix = "Aspire.Hosting.AspireC4.EntityFrameworkCore.Assets.";

	/// <summary>Assets for the interactive (React Flow + ELK) viewer.</summary>
	public static IReadOnlyList<string> AssetNames { get; } = ["index.html", "app.js"];

	/// <summary>Assets for the Mermaid preview.</summary>
	public static IReadOnlyList<string> MermaidAssetNames { get; } = ["index.html", "mermaid-app.js"];

	/// <summary>Reads an interactive-viewer asset, substituting the model file name and title.</summary>
	public static string Asset(string name, string modelFileName, string? title) =>
		Substitute(Read("viewer." + name), modelFileName, title);

	/// <summary>Reads a Mermaid-preview asset, substituting the diagram file name and title.</summary>
	public static string MermaidAsset(string name, string diagramFileName, string? title) =>
		Substitute(Read("mermaid." + name), diagramFileName, title);

	static string Substitute(string content, string fileName, string? title) =>
		content
			.Replace("__MODEL__", fileName, StringComparison.Ordinal)
			.Replace("__TITLE__", string.IsNullOrWhiteSpace(title) ? "ER Diagram" : title, StringComparison.Ordinal);

	/// <summary>
	/// The viewer's JavaScript dependencies, keyed by the file name written to <c>vendor/</c>.
	/// </summary>
	/// <remarks>
	/// Shipped with the package and gzipped, so a generated diagram works with no setup step. Loading
	/// them from a CDN instead would break offline use, and leaving them to a manual step meant the
	/// viewer rendered a blank page until someone ran it.
	/// </remarks>
	/// <remarks>
	/// Only what the chosen renderer needs is written: the Mermaid bundle is roughly three times the
	/// size of the interactive one, and neither renderer uses the other's.
	/// </remarks>
	static string[] VendorAssetsFor(ErdRenderer renderer) =>
		renderer == ErdRenderer.Mermaid ? ["mermaid.js"] : ["bundle.js", "reactflow.css"];

	/// <summary>
	/// Writes the viewer's dependencies into <c>&lt;directory&gt;/vendor</c>, skipping any already
	/// present so a locally rebuilt bundle is not overwritten.
	/// </summary>
	/// <returns>The number of files written.</returns>
	public static async Task<int> WriteVendorAssetsAsync(
		string directory,
		ErdRenderer renderer,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(directory);

		var vendor = Path.Combine(directory, "vendor");

		Directory.CreateDirectory(vendor);

		var written = 0;

		foreach (var name in VendorAssetsFor(renderer))
		{
			var path = Path.Combine(vendor, name);

			if (File.Exists(path))
			{
				continue;
			}

			using var compressed =
				typeof(InteractiveViewer).Assembly.GetManifestResourceStream($"{Prefix}viewer.vendor.{name}.gz")
				?? throw new InvalidOperationException($"Embedded viewer dependency '{name}.gz' was not found.");

			using System.IO.Compression.GZipStream decompressed = new(
				compressed,
				System.IO.Compression.CompressionMode.Decompress
			);
			using FileStream file = new(path, FileMode.Create, FileAccess.Write);

			await decompressed.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

			written++;
		}

		return written;
	}

	static string Read(string name)
	{
		using var stream =
			typeof(InteractiveViewer).Assembly.GetManifestResourceStream(Prefix + name)
			?? throw new InvalidOperationException(
				$"Embedded viewer asset '{name}' was not found. Expected resource '{Prefix}{name}'."
			);

		using StreamReader reader = new(stream);

		return reader.ReadToEnd();
	}
}
