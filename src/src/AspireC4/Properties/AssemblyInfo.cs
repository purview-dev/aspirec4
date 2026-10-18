using System.Runtime.CompilerServices;

// The EF Core integration ships as its own package but is part of the same product: it creates the
// shared dashboard parent through AspireC4DiagramGrouping, which renames this package's resource and
// so must stay internal to consumers.
[assembly: InternalsVisibleTo("Aspire.Hosting.AspireC4.EntityFrameworkCore")]
