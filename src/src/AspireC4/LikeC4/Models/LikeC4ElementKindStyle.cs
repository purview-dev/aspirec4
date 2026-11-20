namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>Style tokens for a <see cref="LikeC4ElementKindSpec"/>.</summary>
/// <param name="Shape">Shape token, e.g. <c>"storage"</c>, <c>"queue"</c>, <c>"cylinder"</c>.</param>
/// <param name="Color">Color token, e.g. <c>"blue"</c>, <c>"green"</c>, <c>"muted"</c>.</param>
/// <param name="Icon">Icon token, e.g. <c>"tech:postgresql"</c>, <c>"azure:storage-accounts"</c>.</param>
/// <param name="Border">Border token, e.g. <c>"dashed"</c>, <c>"dotted"</c>, <c>"bold"</c>.</param>
/// <param name="Opacity">Opacity percentage (0–100).</param>
public sealed record LikeC4ElementKindStyle(string? Shape, string? Color, string? Icon, string? Border, int? Opacity);
