namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// The image formats the ER diagram is exported to on startup.
/// </summary>
/// <remarks>
/// Both are produced by d2, whatever <see cref="ErdRenderer"/> is in use — see
/// <see cref="AspireC4EfDiagramOptions.ExportImages"/> for why.
/// </remarks>
public enum ErdImageFormat
{
	/// <summary>
	/// Scalable vector graphic. Stays legible at any zoom and is the better choice for a large schema,
	/// where a raster of the whole diagram is unreadable at any sensible pixel size.
	/// </summary>
	Svg,

	/// <summary>
	/// Portable network graphic, for the wikis, issue trackers and chat clients that will not render an
	/// SVG attachment.
	/// </summary>
	Png,
}
