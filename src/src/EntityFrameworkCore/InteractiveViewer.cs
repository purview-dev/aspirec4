namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Supplies the static assets for the interactive (React Flow + ELK) viewer.
/// </summary>
/// <remarks>
/// The assets are embedded rather than written as C# string literals so they stay readable, lintable
/// and editable as real files. They are emitted beside the model JSON, which keeps the viewer working
/// with no server and no additional Aspire resource.
/// </remarks>
public static class InteractiveViewer
{
	const string ResourcePrefix = "Aspire.Hosting.AspireC4.EntityFrameworkCore.Assets.viewer.";

	/// <summary>Names of every asset the viewer needs written to disk.</summary>
	public static IReadOnlyList<string> AssetNames { get; } = ["index.html", "app.js"];

	/// <summary>Reads an asset, substituting the model file name and title.</summary>
	/// <param name="name">One of <see cref="AssetNames"/>.</param>
	/// <param name="modelFileName">The JSON file the viewer loads, relative to the viewer.</param>
	/// <param name="title">Title shown in the page, when set.</param>
	public static string Asset(string name, string modelFileName, string? title) =>
		Read(name)
			.Replace("__MODEL__", modelFileName, StringComparison.Ordinal)
			.Replace("__TITLE__", string.IsNullOrWhiteSpace(title) ? "ER Diagram" : title, StringComparison.Ordinal);

	static string Read(string name)
	{
		using var stream =
			typeof(InteractiveViewer).Assembly.GetManifestResourceStream(ResourcePrefix + name)
			?? throw new InvalidOperationException(
				$"Embedded viewer asset '{name}' was not found. Expected resource '{ResourcePrefix}{name}'."
			);

		using StreamReader reader = new(stream);

		return reader.ReadToEnd();
	}
}
