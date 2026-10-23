namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Lifecycle;

sealed partial class AspireC4EfLifecycleHook : IDisposable
{
	/// <summary>
	/// Releases the export gate. The hook is a DI singleton, so this runs when the AppHost's own
	/// container is disposed at shutdown.
	/// </summary>
	public void Dispose() => _exportGate.Dispose();
}
