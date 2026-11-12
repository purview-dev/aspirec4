using System.Text.Json.Serialization;

namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

[JsonSerializable(typeof(LikeC4IconManifest))]
sealed partial class IconMatcherJsonContext : JsonSerializerContext { }

// Settable rather than init-only: for an init-only property System.Text.Json emits a `safe extern`
// [UnsafeAccessor] under the updated memory-safety rules, and `safe` is still preview in C# 15.0.
sealed class LikeC4IconManifest
{
	[JsonPropertyName("generatedAt")]
	public string GeneratedAt { get; set; } = "";

	[JsonPropertyName("icons")]
	public Dictionary<string, string[]> Icons { get; set; } = [];
}
