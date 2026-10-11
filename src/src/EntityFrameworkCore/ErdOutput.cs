using System.Text.Json;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Dbml;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Mermaid;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>A single file produced for a diagram.</summary>
/// <param name="FileName">File name including extension.</param>
/// <param name="Content">File contents.</param>
public sealed record ErdFile(string FileName, string Content);

/// <summary>
/// Turns an <see cref="EfModel"/> into the files a renderer needs.
/// </summary>
public static class ErdOutput
{
	static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

	/// <summary>Produces every file for the configured renderer, primary document first.</summary>
	public static IReadOnlyList<ErdFile> Create(EfModel model, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		var name = options.FileName;

		return options.Renderer switch
		{
			// The Mermaid preview is for eyeballing the output before pasting it somewhere that renders
			// Mermaid natively; the interactive renderer remains the primary experience.
			ErdRenderer.Mermaid =>
			[
				new ErdFile($"{name}.mmd", MermaidGenerator.Generate(model, options)),
				.. InteractiveViewer.MermaidAssetNames.Select(asset => new ErdFile(
					asset,
					InteractiveViewer.MermaidAsset(asset, $"{name}.mmd", options.Title)
				)),
			],
			ErdRenderer.Dbml => [new ErdFile($"{name}.dbml", DbmlGenerator.Generate(model, options))],
			ErdRenderer.D2 =>
			[
				new ErdFile($"{name}.d2", D2DSLGenerator.Generate(D2ModelBuilder.Build(model, options), options)),
			],
			// The viewer is written alongside its data so the diagram can be opened straight from the
			// output directory, with no server and no new Aspire resource.
			_ =>
			[
				new ErdFile($"{name}.json", Serialise(model)),
				.. InteractiveViewer.AssetNames.Select(asset => new ErdFile(
					asset,
					InteractiveViewer.Asset(asset, $"{name}.json", options.Title)
				)),
			],
		};
	}

	/// <summary>Serialises the model for the interactive renderer.</summary>
	/// <remarks>
	/// Indented so the file diffs cleanly; the model's own ordering is already deterministic, so
	/// regenerating an unchanged model produces byte-identical output.
	/// </remarks>
	public static string Serialise(EfModel model) => JsonSerializer.Serialize(model, SerializerOptions);
}
