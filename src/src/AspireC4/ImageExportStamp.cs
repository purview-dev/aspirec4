using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Records what the images in a directory were rendered from, so an export that would reproduce them
/// byte for byte can be skipped before anything is run.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory "has the diagram changed" check cannot answer this on its own: it starts empty every
/// time the AppHost does, so the first write of each run always looks like a change and would re-export
/// images that are already correct on disk. For the viewer-fidelity path that means pulling and
/// starting a multi-gigabyte browser container to produce files identical to the ones already sitting
/// there.
/// </para>
/// <para>
/// Modelled on the viewer's own <c>.aspirec4-vendor</c> stamp. Named per diagram because both diagrams
/// now export into the same directory, so a single file would have them overwriting each other's.
/// </para>
/// </remarks>
static partial class ImageExportStamp
{
	/// <summary>The stamp file for a diagram, named so it sorts away from the images themselves.</summary>
	internal static string FileName(string diagram) => $".aspirec4-{diagram}-images";

	/// <summary>
	/// Hashes everything that decides what the images look like.
	/// </summary>
	/// <param name="content">The rendered source — the model or DSL the images are produced from.</param>
	/// <param name="parts">
	/// Every other input that changes the output: the formats, the file name, the renderer, and whether
	/// the viewer or d2 is doing the rendering. A change to any of them has to re-export.
	/// </param>
	internal static string Compute(string content, params string[] parts)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(parts);

		var payload = string.Join("\u0000", [Normalise(content), .. parts]);

		return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
	}

	/// <summary>
	/// Removes the parts of a generated model that differ between runs without changing the picture.
	/// </summary>
	/// <remarks>
	/// The architecture model embeds the endpoint URLs Aspire allocated, and those ports are different
	/// on every run. Hashing them verbatim would invalidate the stamp every single time, which for this
	/// diagram means starting a LikeC4 server and a headless browser to redraw something that has not
	/// changed. The port is replaced rather than the whole URL so that a host or a scheme actually
	/// changing is still noticed.
	/// <para>
	/// This does not make the architecture diagram fully cached across restarts, and is not meant to:
	/// the run-state tags also settle during startup, and those genuinely change the picture, so
	/// normalising them away would hide a real difference. The exported images come out byte-identical
	/// either way; what is left is a background render, not changed output.
	/// </para>
	/// </remarks>
	internal static string Normalise(string content) => EphemeralPort().Replace(content, ":<port>");

	[GeneratedRegex(@"(?<=://[^/\s""']+):\d{2,5}\b")]
	private static partial Regex EphemeralPort();

	/// <summary>
	/// Whether <paramref name="directory"/> already holds exactly these images.
	/// </summary>
	/// <remarks>
	/// Both halves matter. A matching stamp alone would skip the export after someone deleted the
	/// images; the files existing alone would keep a stale image after the model changed.
	/// </remarks>
	/// <param name="directory">The images directory.</param>
	/// <param name="diagram">Which diagram's stamp to read.</param>
	/// <param name="stamp">The stamp the current inputs produce.</param>
	/// <param name="expected">The files the export would write.</param>
	/// <param name="cancellationToken">Cancelled on shutdown.</param>
	internal static async Task<bool> IsCurrentAsync(
		string directory,
		string diagram,
		string stamp,
		IEnumerable<string> expected,
		CancellationToken cancellationToken
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentNullException.ThrowIfNull(expected);

		if (!expected.All(File.Exists))
		{
			return false;
		}

		var path = Path.Combine(directory, FileName(diagram));

		if (!File.Exists(path))
		{
			return false;
		}

		var existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

		return string.Equals(existing.Trim(), stamp, StringComparison.Ordinal);
	}

	/// <summary>Records the stamp, once the images it describes have actually been written.</summary>
	/// <remarks>
	/// Written last for the same reason the vendor stamp is: a run interrupted part-way through leaves
	/// no stamp, so the next one re-exports rather than trusting a half-written directory.
	/// </remarks>
	internal static Task WriteAsync(
		string directory,
		string diagram,
		string stamp,
		CancellationToken cancellationToken
	) => File.WriteAllTextAsync(Path.Combine(directory, FileName(diagram)), stamp, cancellationToken);
}
