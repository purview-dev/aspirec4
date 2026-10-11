using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

/// <summary>
/// Log messages written to the ER diagram resource's own log stream.
/// </summary>
/// <remarks>
/// Source-generated rather than written through <c>ILogger.LogInformation</c> so the arguments are not
/// boxed when logging is switched off. These deliberately go to
/// <see cref="ResourceLoggerService"/> rather than the AppHost logger: they are what the dashboard
/// shows under the resource, and without them it appears to be doing nothing.
/// </remarks>
static partial class ErdLog
{
	[LoggerMessage(Level = LogLevel.Information, Message = "Wrote {Path}")]
	public static partial void Wrote(ILogger logger, string path);

	[LoggerMessage(
		Level = LogLevel.Information,
		Message = "Generated {Renderer} ER diagram: {Contexts} context(s), {Tables} table(s)."
	)]
	public static partial void Generated(ILogger logger, ErdRenderer renderer, int contexts, int tables);

	[LoggerMessage(Level = LogLevel.Information, Message = "ER diagram unchanged; nothing written.")]
	public static partial void Unchanged(ILogger logger);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "No Entity Framework Core models were found. Opt a project in with "
			+ ".WithEntityFrameworkModel(), reference the AspireC4.EntityFrameworkCore package from it, "
			+ "and build it at least once."
	)]
	public static partial void NoModelsFound(ILogger logger);

	[LoggerMessage(Level = LogLevel.Information, Message = "Wrote {Count} viewer dependency file(s).")]
	public static partial void ViewerDependenciesWritten(ILogger logger, int count);
}
