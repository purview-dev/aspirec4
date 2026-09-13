using System.Text.Json;

namespace Aspire.Hosting.AspireC4.LikeC4.Generators;

/// <summary>
/// Unit tests for <see cref="LikeC4ConfigGenerator"/>.
/// </summary>
public sealed class LikeC4ConfigGeneratorTests
{
	[Test]
	public async Task Generate_MinimalConfig_ContainsSchemaNameAndTitle()
	{
		// Arrange
		const string name = "my-project";
		const string title = "My Architecture";

		// Act
		var json = LikeC4ConfigGenerator.Generate(
			name,
			title,
			[],
			new Dictionary<string, string>(),
			new Dictionary<string, string>()
		);

		// Assert
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		await Assert.That(root.GetProperty("$schema").GetString()).IsEqualTo("https://likec4.dev/schemas/config.json");
		await Assert.That(root.GetProperty("name").GetString()).IsEqualTo(name);
		await Assert.That(root.GetProperty("title").GetString()).IsEqualTo(title);
	}

	[Test]
	public async Task Generate_NoIncludePaths_DoesNotEmitIncludeBlock()
	{
		// Arrange

		// Act
		var json = LikeC4ConfigGenerator.Generate(
			"proj",
			"Title",
			[],
			new Dictionary<string, string>(),
			new Dictionary<string, string>()
		);

		// Assert
		using var doc = JsonDocument.Parse(json);
		await Assert.That(doc.RootElement.TryGetProperty("include", out _)).IsFalse();
	}

	[Test]
	public async Task Generate_WithIncludePaths_EmitsIncludeBlock()
	{
		// Arrange
		string[] includePaths = ["../extra", "../other"];

		// Act
		var json = LikeC4ConfigGenerator.Generate(
			"proj",
			"Title",
			includePaths,
			new Dictionary<string, string>(),
			new Dictionary<string, string>()
		);

		// Assert
		using var doc = JsonDocument.Parse(json);
		var include = doc.RootElement.GetProperty("include");
		var paths = include.GetProperty("paths");
		await Assert.That(paths.GetArrayLength()).IsEqualTo(2);
		await Assert.That(paths[0].GetString()).IsEqualTo("../extra");
		await Assert.That(paths[1].GetString()).IsEqualTo("../other");
	}

	[Test]
	public async Task Generate_NoImageAliases_DoesNotEmitImageAliasesBlock()
	{
		// Arrange

		// Act
		var json = LikeC4ConfigGenerator.Generate(
			"proj",
			"Title",
			[],
			new Dictionary<string, string>(),
			new Dictionary<string, string>()
		);

		// Assert
		using var doc = JsonDocument.Parse(json);
		await Assert.That(doc.RootElement.TryGetProperty("imageAliases", out _)).IsFalse();
	}

	[Test]
	public async Task Generate_WithImageAliases_EmitsImageAliasesBlock()
	{
		// Arrange
		Dictionary<string, string> aliases = new() { ["@icons"] = "../assets/icons" };

		// Act
		var json = LikeC4ConfigGenerator.Generate("proj", "Title", [], aliases, new Dictionary<string, string>());

		// Assert
		using var doc = JsonDocument.Parse(json);
		var imageAliases = doc.RootElement.GetProperty("imageAliases");
		await Assert.That(imageAliases.GetProperty("@icons").GetString()).IsEqualTo("../assets/icons");
	}

	[Test]
	public async Task Generate_FullConfig_IsValidJson()
	{
		// Arrange
		Dictionary<string, string> aliases = new() { ["@icons"] = "../icons", ["@logos"] = "../logos" };
		string[] includePaths = ["../ext/abc", "../ext/def"];

		// Act
		var json = LikeC4ConfigGenerator.Generate(
			"aspirec4",
			"Architecture",
			includePaths,
			aliases,
			new Dictionary<string, string>()
		);

		// Assert
		using var doc = JsonDocument.Parse(json);
		await Assert.That(doc.RootElement.GetProperty("name").GetString()).IsEqualTo("aspirec4");
	}
}
