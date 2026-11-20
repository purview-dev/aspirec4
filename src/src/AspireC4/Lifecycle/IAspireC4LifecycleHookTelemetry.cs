using Purview.Telemetry;

namespace Aspire.Hosting.AspireC4.Lifecycle;

/// <summary>
/// The architecture diagram's log messages, written to the AppHost's logger under this interface's full
/// name.
/// </summary>
/// <remarks>
/// Event ids are pinned in the 1000–1099 range (the ER diagram's <c>ErdLog</c> uses 2000–2099) and
/// documented in <c>docs/wiki/configuration/Logging.md</c>, so a filter written against one keeps working
/// when a method is renamed. Never reuse a retired id; add new messages at the end of the range.
/// </remarks>
[Logger]
interface IAspireC4LifecycleHookTelemetry
{
	[Debug(1001)]
	void GeneratingLikeC4Model(int resourceCount, string[] resourceNames);

	[Debug(1002)]
	void LikeC4ModelWritten(string outputPath);

	[Debug(1003)]
	void PublishMode();

	[Debug(1004)]
	void ResourceStateChanged(string resourceName, string newState);

	[Debug(1005)]
	void DashboardUrlDiscovered(string dashboardBaseUrl);

	[Debug(1006)]
	void RegeneratingDiagramDueToStateChange();

	[Warning(1007)]
	void StateWatcherFailed(string watcher, Exception ex);

	[Debug(1008)]
	void LikeC4FormatApplied();

	[Debug(1009)]
	void AdditionalDSLFileSynced(string fileName);

	[Info(1010)]
	void GitIgnoreFileKept(string path);

	[Warning(1011)]
	void FailedToRunFormatter(Exception ex);

	[Info(1012)]
	void ResolvedLikeC4Version(string version);

	[Warning(1013)]
	void FailedToResolveLikeC4Version(string command, string reason);

	[Debug(1014)]
	void ApplyingDiagramOptionsSnapshot();

	[Debug(1015)]
	void DiagramOptionsSnapshotApplied(string outputDirectoryName, bool formatGeneratedFile, bool disableHmr);

	[Debug(1016)]
	void ExportingImages(string format, string imageDirectory);

	[Info(1017)]
	void ImagesExported(string format, string imageDirectory);

	[Warning(1018)]
	void FailedToExportImages(string format, string reason);

	[Debug(1019)]
	void ImagesUnchanged(string imageDirectory);

	[Warning(1020)]
	void DiagramRegenerationFailed(Exception ex);

	[Warning(1021)]
	void ImageExportFailed(Exception ex);

	[Warning(1022)]
	void AdditionalDSLFileMissing(string path);

	[Warning(1023)]
	void FormatterExited(int exitCode, string error);
}
