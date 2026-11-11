using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// The outcome of looking for a build-time EF Core model dump.
/// </summary>
/// <remarks>
/// A closed hierarchy rather than a <see langword="bool"/> out parameter: the set of outcomes is
/// fixed here, so a switch over it is exhaustive and adding an outcome fails the build in every
/// consumer until it is handled. The reason matters because the dump is the only discovery source
/// that sees conventions and fluent configuration — a caller that only knows "not found" cannot
/// tell a project that needs rebuilding from one that never referenced the ModelDump package.
/// </remarks>
closed record class EfModelDumpRead;

/// <summary>A usable dump was found and merged into a model.</summary>
/// <param name="Model">The model the dump described.</param>
sealed record EfModelDumpLoaded(EfModel Model) : EfModelDumpRead;

/// <summary>No usable dump was found.</summary>
/// <param name="Status">Why the dump could not be used.</param>
sealed record EfModelDumpUnavailable(EfModelDumpStatus Status) : EfModelDumpRead;
