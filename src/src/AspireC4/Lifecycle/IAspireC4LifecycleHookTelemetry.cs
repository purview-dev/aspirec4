using Purview.Telemetry;

namespace Aspire.Hosting.AspireC4.Lifecycle;

[Logger]
interface IAspireC4LifecycleHookTelemetry
{
	[Debug]
	void GeneratingLikeC4Model(int resourceCount, string[] resourceNames);

	[Debug]
	void LikeC4ModelWritten(string outputPath);

	[Debug]
	void PublishMode();

	[Debug]
	void ResourceStateChanged(string resourceName, string newState);

	[Debug]
	void DashboardUrlDiscovered(string dashboardBaseUrl);

	[Debug]
	void RegeneratingDiagramDueToStateChange();

	[Warning]
	void StateWatcherFailed(string error);

	[Debug]
	void LikeC4FormatApplied();

	[Debug]
	void AdditionalDSLFileSynced(string fileName);

	[Warning]
	void FailedToRunFormatter(Exception ex);

	[Info]
	void ResolvedLatestContainerVersion(string version);

	[Warning]
	void FailedToResolveLatestContainerVersion();

	[Debug]
	void ApplyingDiagramOptionsSnapshot();

	[Debug]
	void DiagramOptionsSnapshotApplied(string outputDirectoryName, bool formatGeneratedFile, bool disableHmr);

	[Debug]
	void ExportingImages(string format, string imageDirectory);

	[Info]
	void ImagesExported(string format, string imageDirectory);

	[Warning]
	void FailedToExportImages(string format, string reason);
}
