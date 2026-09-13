namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>
/// Known LikeC4 state tags for Aspire resource run states.
/// </summary>
static class LikeC4StateTags
{
	/// <summary>
	/// The resource is starting.
	/// </summary>
	public const string Starting = "aspire-run-state-starting";

	/// <summary>
	/// The resource is waiting.
	/// </summary>
	public const string Waiting = "aspire-run-state-waiting";

	/// <summary>
	/// The resource is running.
	/// </summary>
	public const string Running = "aspire-run-state-running";

	/// <summary>
	/// The resource is stopping.
	/// </summary>
	public const string Stopping = "aspire-run-state-stopping";

	/// <summary>
	/// The resource has exited.
	/// </summary>
	public const string Exited = "aspire-run-state-exited";

	/// <summary>
	/// The resource has finished.
	/// </summary>
	public const string Finished = "aspire-run-state-finished";

	/// <summary>
	/// The resource is unhealthy at runtime.
	/// </summary>
	public const string RuntimeUnhealthy = "aspire-run-state-runtimeunhealthy";

	/// <summary>
	/// The resource failed to start.
	/// </summary>
	public const string FailedToStart = "aspire-run-state-failedtostart";
}
