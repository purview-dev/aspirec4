namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;

/// <summary>Maps <see cref="D2LayoutEngine"/> onto the names the d2 CLI accepts for <c>--layout</c>.</summary>
/// <remarks>
/// Shared by the sidecar resources and the image exporter so a diagram's exported image is laid out by
/// the same engine as the one being served.
/// </remarks>
static class D2LayoutEngineNames
{
#pragma warning disable IDE0072 // Add missing cases
	public static string ToEngineName(this D2LayoutEngine engine) =>
		engine switch
		{
			D2LayoutEngine.Dagre => "dagre",
			D2LayoutEngine.Tala => "tala",
			_ => "elk",
		};
#pragma warning restore IDE0072 // Add missing cases
}
