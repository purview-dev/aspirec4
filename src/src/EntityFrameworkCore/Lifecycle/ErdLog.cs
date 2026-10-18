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

	[LoggerMessage(
		Level = LogLevel.Information,
		Message = "{Approximate} of {Total} context(s) were recovered from assembly metadata rather than "
			+ "a source-generated descriptor, so configuration applied only in OnModelCreating is not "
			+ "shown. Reference the AspireC4.EntityFrameworkCore package from the owning project for full "
			+ "fidelity."
	)]
	public static partial void ApproximateContexts(ILogger logger, int approximate, int total);

	[LoggerMessage(Level = LogLevel.Information, Message = "ER diagram unchanged; nothing written.")]
	public static partial void Unchanged(ILogger logger);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "No Entity Framework Core models were found. Ensure at least one project resource that "
			+ "owns a DbContext has been built at least once."
	)]
	public static partial void NoModelsFound(ILogger logger);

	[LoggerMessage(Level = LogLevel.Information, Message = "Wrote {Count} viewer dependency file(s).")]
	public static partial void ViewerDependenciesWritten(ILogger logger, int count);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "No Entity Framework Core models were found for project {ProjectPath} "
			+ "(output directory: {OutputDirectory}, {AssembliesScanned} assembly(ies) read, "
			+ "{DescriptorsFound} descriptor(s) found). Ensure the project has been built at least once; "
			+ "add the AspireC4.EntityFrameworkCore package to a project you own for full fidelity."
	)]
	public static partial void ProjectProducedNoModel(
		ILogger logger,
		string projectPath,
		string outputDirectory,
		int assembliesScanned,
		int descriptorsFound
	);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "No Entity Framework Core models were found on this pass; keeping the diagram already "
			+ "written to {OutputDirectory} rather than replacing it with an empty one."
	)]
	public static partial void NoModelsFoundKeepingExisting(ILogger logger, string outputDirectory);
}
