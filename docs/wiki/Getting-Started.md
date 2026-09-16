# Getting Started

This guide walks through installing AspireC4 and generating your first live architecture diagram.

## Prerequisites

- **.NET 8 or later**.
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

1. Writes the generated model to `./likec4/gen/model.gen.c4` (relative to the AppHost working directory).
2. Starts the official `ghcr.io/likec4/likec4` container as a sidecar resource named `aspirec4`.
3. Refreshes the diagram whenever the Aspire application changes — for example when a resource transitions to `Running` or `Exited`.

Open the Aspire dashboard and select the `aspirec4` resource (or its **View LikeC4 Diagram** link) to see the live diagram.

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
- [Generated Output](Generated-Output.md)
- [Dashboard Integration](Dashboard-Integration.md)
- [Source Generator Validation](Source-Generator.md)