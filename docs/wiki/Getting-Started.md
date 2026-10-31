# Getting Started

This guide walks through installing AspireC4 and generating your first live architecture diagram.

## Prerequisites

- **.NET 10 or later.** The AppHost targets `net10.0`, and so do the `AspireC4.Hosting` and `AspireC4.Hosting.EntityFrameworkCore` libraries.
- **Aspire 13.5.3 or later**. Aspire AppHost projects must reference both the AppHost SDK and `Aspire.Hosting.AppHost`.
- **Docker** is used by default to run the LikeC4 sidecar container.
- Optional: a local Node.js CLI runtime (`npx`, `pnpm`, `yarn`, `bun`, or `deno`) if you call `.WithLocalCLI()`.

## Installation

Add AspireC4 to the AppHost project:

```bash
dotnet add package AspireC4.Hosting
dotnet add package Aspire.Hosting.AppHost
```

An Aspire 13.5 AppHost project should contain the equivalent of:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="AspireC4.Hosting" />
  </ItemGroup>
</Project>
```

> [!NOTE]
> The generator, registry attributes, and enums are injected automatically by the `AspireC4.Hosting` package. Do not declare or reference a separate `AspireC4.SourceGenerators` package.

## Quick start

Add the visualization to your AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4();

builder.Build().Run();
```

That is all it takes. On startup AspireC4:

1. Writes the generated model to `./.aspirec4/likec4/model.gen.c4` (relative to the AppHost working directory).
2. Starts the official `ghcr.io/likec4/likec4` container as a sidecar resource named `aspirec4`.
3. Refreshes the diagram whenever the Aspire application changes — for example when a resource transitions to `Running` or `Exited`.

Open the Aspire dashboard and select the `aspirec4` resource (or its **View LikeC4 Diagram** link) to see the live diagram.

> [!NOTE]
> **Upgrading from 13.5 or earlier?** 13.6 moved generated output under `./.aspirec4/` and changed the
> dashboard names when a second diagram is registered. `WithLegacyLayout()` keeps the old
> `./likec4/gen/` and `./erd/gen/` paths and the flat `aspirec4` / `aspirec4-er` rows. See
> [Migration](Migration.md).

## Customize the diagram

Pass a configuration callback to `AddAspireC4`:

```csharp
builder.AddAspireC4(options =>
    options
        .WithTitle("My distributed application")
        .WithViewTitle("Architecture")
        .WithViewDescription("Generated from the Aspire resource graph")
);
```

See [Configuration](Configuration.md) for the full options reference and [Customizing Resources](Customizing-Resources.md) for per-resource details.

## Add an ER diagram too (optional)

The architecture diagram is the base: `AddAspireC4()` is all you need, and you can stop there. To add a
linked ER diagram of your Entity Framework Core `DbContext` types as well, install the second AppHost
package and call one more method on the builder `AddAspireC4()` returns:

```bash
dotnet add package AspireC4.Hosting.EntityFrameworkCore
```

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4()
       .WithEntityFrameworkDiagram();

builder.AddProject<Projects.Api>("api");

builder.Build().Run();
```

The dashboard then shows both diagrams under one `aspirec4` parent row, and each contributing project is
linked to the ER diagram. Output moves to `./.aspirec4/erd/`, beside the architecture diagram's
`./.aspirec4/likec4/`.

Two more optional packages raise the fidelity of the ER model — `AspireC4.EntityFrameworkCore` in the
project declaring the `DbContext`, and `AspireC4.EntityFrameworkCore.ModelDump` in the project owning the
EF Core provider. Both are opt-in by reference alone. See
[Entity Framework Core ER Diagrams](Entity-Framework-Diagrams.md).

## Use a local CLI instead of Docker

When Docker is not available, or you prefer a local Node.js-based workflow:

```csharp
builder.AddAspireC4().WithLocalCLI();
```

The first runtime available on the system `PATH` is selected automatically (npx → pnpm → yarn → bun → deno). See [Local CLI Runtimes](Local-CLI.md).

## Run the TypeScript sample

AspireC4 supports both C# and TypeScript AppHosts. See [TypeScript AppHosts](Typescript-AppHost.md) for the TypeScript API and a runnable sample.

## Next pages

- [Configuration](Configuration.md)
- [Customizing Resources](Customizing-Resources.md)
- [Entity Framework Core ER Diagrams](Entity-Framework-Diagrams.md)
- [Generated Output](Generated-Output.md)
- [Dashboard Integration](Dashboard-Integration.md)
- [Source Generator Validation](Source-Generator.md)