using System.Runtime.CompilerServices;

// The EF Core integration ships as its own package but is part of the same product: it creates the
// shared dashboard parent through AspireC4DiagramGrouping, which renames this package's resource and
// so must stay internal to consumers.
[assembly: InternalsVisibleTo("Aspire.Hosting.AspireC4.EntityFrameworkCore")]
// The Aspire 13 copy of the integration suite. The SDK grants the conventional
// "Aspire.Hosting.AspireC4.IntegrationTests" name itself; this one carries the line in its name.
[assembly: InternalsVisibleTo("Aspire.Hosting.AspireC4.Aspire13.IntegrationTests")]
