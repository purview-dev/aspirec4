using System.Runtime.InteropServices;

// A deliberately inert service. It exists so the sample EF Core models are owned by a real Aspire
// project resource, which is what AspireC4 discovers and reads descriptors from. It opens no
// connections and runs no migrations — the ER diagram is built from compile-time metadata, never
// from a live database.
//
// Kept dependency-free on purpose: a hosting package would add nothing, and this has to stay cheap
// because the integration tests start it on every run.
using CancellationTokenSource cts = new();

using var sigTerm = PosixSignalRegistration.Create(
	PosixSignal.SIGTERM,
	context =>
	{
		context.Cancel = true;
		cts.Cancel();
	}
);

using var sigInt = PosixSignalRegistration.Create(
	PosixSignal.SIGINT,
	context =>
	{
		context.Cancel = true;
		cts.Cancel();
	}
);

Console.WriteLine("Sample data service started. Owns the AspireC4 sample EF Core models.");

try
{
	await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
}
catch (OperationCanceledException)
{
	// Shutdown requested.
}
