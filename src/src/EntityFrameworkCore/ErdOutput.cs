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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	public static IReadOnlyList<ErdFile> Create(EfModel model, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		var name = options.FileName;

		// Resolved once, so every rule and style callback runs once per element per regeneration.
		var presentation = ErdPresentation.Build(model, options);

		return options.Renderer switch
		{
			// The Mermaid preview is for eyeballing the output before pasting it somewhere that renders
			// Mermaid natively; the interactive renderer remains the primary experience.
			ErdRenderer.Mermaid =>
			[
				new ErdFile($"{name}.mmd", MermaidGenerator.Generate(model, options, presentation)),
				.. InteractiveViewer.MermaidAssetNames.Select(asset => new ErdFile(
					asset,
					InteractiveViewer.MermaidAsset(asset, $"{name}.mmd", options.Title)
				)),
			],
			ErdRenderer.Dbml => [new ErdFile($"{name}.dbml", DbmlGenerator.Generate(model, options, presentation))],
			ErdRenderer.D2 =>
			[
				new ErdFile(
					$"{name}.d2",
					D2DSLGenerator.Generate(D2ModelBuilder.Build(model, options, presentation), options)
				),
			],
			// The viewer is written alongside its data so the diagram can be opened straight from the
			// output directory, with no server and no new Aspire resource.
			_ =>
			[
				new ErdFile($"{name}.json", Serialise(model, options, presentation)),
				.. InteractiveViewer.AssetNames.Select(asset => new ErdFile(
					asset,
					InteractiveViewer.Asset(asset, $"{name}.json", options)
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

	/// <summary>
	/// Serialises the model for the interactive renderer, with its presentation — viewer defaults,
	/// layout, styles and descriptions — beside the contexts.
	/// </summary>
	/// <remarks>
	/// A sibling of <c>contexts</c> rather than a wrapper around it, so anything that reads the file as a
	/// model — <c>EfModelLoader.Parse</c>, the viewer check, a consumer's own tooling — still does, and
	/// simply ignores the presentation.
	/// </remarks>
	internal static string Serialise(
		EfModel model,
		AspireC4EfDiagramOptions options,
		ErdPresentation? presentation = null
	)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		var json = JsonSerializer.SerializeToNode(model, SerializerOptions)!.AsObject();

		json["presentation"] = (presentation ?? ErdPresentation.Build(model, options)).ToJson(options);

		return json.ToJsonString(SerializerOptions);
	}
}
