using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspire.Hosting.AspireC4.LikeC4.Generators;

/// <summary>
/// Generates the content of a <c>likec4.config.json</c> file that configures a LikeC4 project.
/// </summary>
/// <remarks>
/// The config file is placed in the output directory alongside the generated model file.
/// Relative paths in the config are computed from the output directory, so the config is portable
/// as long as the relative structure between output and referenced folders is preserved.
/// </remarks>
public static class LikeC4ConfigGenerator
{
	static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

	/// <summary>
	/// Generates the content of a <c>likec4.config.json</c> file.
	/// </summary>
	/// <param name="name">
	/// The LikeC4 project name (becomes the <c>name</c> field; must be a unique identifier within the workspace).
	/// </param>
	/// <param name="title">
	/// Optional display title shown in generated diagrams (becomes the <c>title</c> field). Can be <c>null</c>.
	/// </param>
	/// <param name="includePaths">
	/// Paths to include, relative to the location of the config file.
	/// Forwarded to the <c>include.paths</c> array. Can be empty.
	/// </param>
	/// <param name="imageAliases">
	/// Key/value pairs where each key is an image alias (must start with <c>@</c>) and the value
	/// is a path relative to the config file. Forwarded to the <c>imageAliases</c> object. Can be empty.
	/// </param>
	/// <param name="configFileMetadata">
	/// Key/value pairs where each key is a metadata field and the value is the corresponding metadata value.
	/// Forwarded to the <c>metadata</c> object. Can be empty.
	/// </param>
	/// <returns>A formatted JSON string suitable for writing to a <c>likec4.config.json</c> file.</returns>
	public static string Generate(
		string name,
		string? title,
		IEnumerable<string> includePaths,
		IReadOnlyDictionary<string, string> imageAliases,
		IReadOnlyDictionary<string, string> configFileMetadata
	)
	{
		ArgumentNullException.ThrowIfNull(imageAliases);
		JsonObject root = new() { ["$schema"] = "https://likec4.dev/schemas/config.json", ["name"] = name };

		if (!string.IsNullOrWhiteSpace(title))
			root["title"] = title;

		var paths = includePaths.ToList();
		if (paths.Count > 0)
		{
			JsonArray pathArray = [];
			foreach (var p in paths)
				pathArray.Add(JsonValue.Create(p));

			root["include"] = new JsonObject { ["paths"] = pathArray };
		}

		if (imageAliases.Count > 0)
		{
			JsonObject aliasesObject = [];
			foreach (var (key, relativePath) in imageAliases)
				aliasesObject[key] = relativePath;

			root["imageAliases"] = aliasesObject;
		}

		if (configFileMetadata?.Count > 0)
		{
			JsonObject metadataObject = [];
			foreach (var (key, value) in configFileMetadata)
				metadataObject[key] = value;

			root["metadata"] = metadataObject;
		}

		return root.ToJsonString(WriteOptions);
	}
}
