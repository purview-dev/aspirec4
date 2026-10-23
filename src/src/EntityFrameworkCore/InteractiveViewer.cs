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

	/// <summary>Reads an interactive-viewer asset, substituting the model file name, title and display.</summary>
	public static string Asset(string name, string modelFileName, string? title, DomainTypeDisplay domainTypeDisplay) =>
		Substitute(Read("viewer." + name), modelFileName, title, domainTypeDisplay);

	/// <summary>Reads a Mermaid-preview asset, substituting the diagram file name and title.</summary>
	public static string MermaidAsset(string name, string diagramFileName, string? title) =>
		Substitute(Read("mermaid." + name), diagramFileName, title, DomainTypeDisplay.DomainTypeFirst);

	static string Substitute(string content, string fileName, string? title, DomainTypeDisplay display) =>
		content
			.Replace("__MODEL__", fileName, StringComparison.Ordinal)
			.Replace("__TITLE__", string.IsNullOrWhiteSpace(title) ? "ER Diagram" : title, StringComparison.Ordinal)
			.Replace("__DOMAIN_TYPE_DISPLAY__", display.ToString(), StringComparison.Ordinal);

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
	/// Names the file recording which build of the embedded dependencies is in <c>vendor/</c>.
	/// </summary>
	internal const string VendorStampFileName = ".aspirec4-vendor";

	/// <summary>
	/// Writes the viewer's dependencies into <c>&lt;directory&gt;/vendor</c>, skipping the write when
	/// the directory already holds this build of them.
	/// </summary>
	/// <remarks>
	/// The decision is made from a stamp holding a hash of the embedded assets rather than from the
	/// files merely existing: the generated <c>app.js</c> imports named exports from
	/// <c>vendor/bundle.js</c>, so a bundle left over from an earlier package version fails the import
	/// and the viewer renders nothing. Hashing the embedded resources, rather than stamping the package
	/// version, also refreshes the directory while the assets are being rebuilt in-repo.
	/// </remarks>
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

		var assets = VendorAssetsFor(renderer);
		var stampPath = Path.Combine(vendor, VendorStampFileName);
		var stamp = VendorStamp(assets);

		if (
			assets.All(name => File.Exists(Path.Combine(vendor, name)))
			&& await MatchesAsync(stampPath, stamp, cancellationToken).ConfigureAwait(false)
		)
		{
			return 0;
		}

		var written = 0;

		foreach (var name in assets)
		{
			var path = Path.Combine(vendor, name);

			using var compressed = OpenVendorAsset(name);
			using System.IO.Compression.GZipStream decompressed = new(
				compressed,
				System.IO.Compression.CompressionMode.Decompress
			);
			using FileStream file = new(path, FileMode.Create, FileAccess.Write);

			await decompressed.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

			written++;
		}

		// Written last, so a run interrupted part-way through leaves a stamp that does not match and the
		// next run rewrites rather than trusting a half-populated directory.
		await File.WriteAllTextAsync(stampPath, stamp, cancellationToken).ConfigureAwait(false);

		return written;
	}

	static async Task<bool> MatchesAsync(string stampPath, string stamp, CancellationToken cancellationToken)
	{
		if (!File.Exists(stampPath))
		{
			return false;
		}

		var existing = await File.ReadAllTextAsync(stampPath, cancellationToken).ConfigureAwait(false);

		return string.Equals(existing.Trim(), stamp, StringComparison.Ordinal);
	}

	/// <summary>Hashes the embedded, still-compressed assets the given renderer depends on.</summary>
	static string VendorStamp(string[] assets)
	{
		using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
			System.Security.Cryptography.HashAlgorithmName.SHA256
		);

		foreach (var name in assets)
		{
			using var compressed = OpenVendorAsset(name);

			hash.AppendData(System.Text.Encoding.UTF8.GetBytes(name));

			var buffer = new byte[81920];
			int read;

			while ((read = compressed.Read(buffer, 0, buffer.Length)) > 0)
			{
				hash.AppendData(buffer, 0, read);
			}
		}

		return Convert.ToHexStringLower(hash.GetHashAndReset());
	}

	static Stream OpenVendorAsset(string name) =>
		typeof(InteractiveViewer).Assembly.GetManifestResourceStream($"{Prefix}viewer.vendor.{name}.gz")
		?? throw new InvalidOperationException($"Embedded viewer dependency '{name}.gz' was not found.");

	/// <summary>
	/// The script the viewer-fidelity image export runs inside the browser container.
	/// </summary>
	/// <remarks>
	/// Kept here with the rest of the viewer's assets because it drives that viewer's own export
	/// control, so the two have to agree on the markup — the <c>[data-export]</c> buttons and the
	/// <c>.entity</c> node class in particular.
	/// </remarks>
	internal static string ViewerExportScript() => Read("export.viewer-export.mjs");

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
