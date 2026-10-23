using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Aspire.Hosting.AspireC4.EntityFrameworkCore.UnitTests")]
// The integration suite drives both products' image exporters from one place, so it needs this
// assembly's internals too. The SDK generates the conventional names per package, which for this
// package are prefixed "…EntityFrameworkCore.*Tests" and so do not cover the shared suite.
[assembly: InternalsVisibleTo("Aspire.Hosting.AspireC4.IntegrationTests")]
