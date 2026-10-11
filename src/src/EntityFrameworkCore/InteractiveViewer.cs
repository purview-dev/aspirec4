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
