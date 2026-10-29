# AspireC4.EntityFrameworkCore.ModelDump

[![NuGet version](https://img.shields.io/nuget/v/AspireC4.EntityFrameworkCore.ModelDump.svg)](https://www.nuget.org/packages/AspireC4.EntityFrameworkCore.ModelDump)

Writes your **real** Entity Framework Core model to `obj/` on every build, so the AspireC4 ER diagram
shows exactly what EF builds: conventions, value converters, `IEntityTypeConfiguration<T>` classes and
composite foreign keys included.

This package is **optional**. It is a companion to
[`AspireC4.Hosting.EntityFrameworkCore`](https://www.nuget.org/packages/AspireC4.Hosting.EntityFrameworkCore),
which goes in the AppHost and does the drawing. Start there if you have not set up the ER diagram yet.

## When to use it

The AppHost can already read your contexts without this package, either from assembly metadata or from
the [`AspireC4.EntityFrameworkCore`](https://www.nuget.org/packages/AspireC4.EntityFrameworkCore)
source generator. Both read the model statically, so neither sees configuration applied at runtime
(`ApplyConfigurationsFromAssembly`, value converters) or relationships configured only fluently.
This package builds the model the same way your application does, so the dump is always preferred
when it is present.

## Installation

### 1. Add the package to the project that owns the EF Core provider

That is the project that references `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`
or similar. It can be the service registered in the AppHost or a persistence library the service
references.

```bash
dotnet add package AspireC4.EntityFrameworkCore.ModelDump
```

The reference is the opt-in. The package is a development dependency, so it adds nothing to your
build output or to the projects that reference yours.

### 2. Make sure the project has a design-time factory

The dump creates each context through `IDesignTimeDbContextFactory<T>`, which is the same entry point
`dotnet ef` uses. You may already have one for migrations. If not, add one for each context:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            // The database is never opened, so any syntactically valid connection string works.
            .UseNpgsql("Host=localhost;Database=orders")
            .Options;

        return new OrdersDbContext(options);
    }
}
```

A project with no design-time factory dumps nothing, and the AppHost falls back to the other sources.

### 3. Build

The dump runs automatically after every build. The AppHost picks it up the next time it starts, from
the registered project and from every project it references.

## What it writes

The dump is written to `obj/aspirec4/ef/`, one JSON file per context. That directory is never copied
to `bin/` or `publish/`, and `obj/` is normally ignored by git. The dump leaves nothing in your
deployment or your repository.

The AppHost ignores a dump that is older than the built assembly, so a stale dump is never shown.

## MSBuild properties

| Property | Default | Description |
| --- | --- | --- |
| `AspireC4EfModelDumpConnectionString` | *(empty)* | Passed to the factory as `--connection=<value>` in `args`, for a factory that reads its connection string from there |
| `AspireC4EfModelDumpOutputDir` | `$(BaseIntermediateOutputPath)aspirec4/ef/` | Where the dump is written. Change it only if your `obj/` layout is custom |

```xml
<PropertyGroup>
  <AspireC4EfModelDumpConnectionString>Host=localhost;Database=orders</AspireC4EfModelDumpConnectionString>
</PropertyGroup>
```

A dump failure never fails your build. The target runs with `ContinueOnError`, and the AppHost uses
the next best source instead.

## More information

- [Full documentation](https://purview.dev/docs/aspirec4/)
- [Repository and samples](https://github.com/purview-dev/aspirec4)
