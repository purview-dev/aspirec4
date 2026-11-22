namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	public void Dispose()
	{
		_regenerationScheduler.Dispose();

		_exportGate.Dispose();
	}
}
