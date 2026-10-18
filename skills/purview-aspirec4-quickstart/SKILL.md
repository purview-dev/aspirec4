---
name: purview-aspirec4-quickstart
description: >-
  Use when adding AspireC4 to an Aspire AppHost to generate a live LikeC4 architecture diagram -
  installing AspireC4.Hosting, calling AddAspireC4, what happens on startup, running the LikeC4
  server as a Docker sidecar or through a local Node CLI, and finding the diagram in the Aspire
  dashboard.
metadata:
  product: AspireC4
  repository: purview-dev/aspirec4
---

# AspireC4 quickstart

AspireC4 generates a **live** LikeC4 architecture diagram from the Aspire resource graph, and serves
it beside your application.

## Prerequisites

- .NET 8 or later.
- **Aspire 13.5.3 or later.** An AppHost project must reference both the AppHost SDK and
  `Aspire.Hosting.AppHost`.
- **Docker**, to run the LikeC4 sidecar container — or a local Node CLI runtime if you use
  `.WithLocalCLI()`.

```bash
dotnet add package AspireC4.Hosting
dotnet add package Aspire.Hosting.AppHost
```

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="AspireC4.Hosting" />
  </ItemGroup>
</Project>
```

> The generator, registry attributes and enums are injected automatically by `AspireC4.Hosting`.
> **Do not reference a separate `AspireC4.SourceGenerators` package** — there isn't one to add.

## One line

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4();

builder.Build().Run();
```

On startup AspireC4:

1. writes the generated model to `./.aspirec4/likec4/model.gen.c4`, relative to the AppHost working
   directory;
2. starts `ghcr.io/likec4/likec4` as a sidecar resource named `aspirec4`;
3. **refreshes the diagram whenever the application changes** — a resource moving to `Running` or
   `Exited`, for instance.

Open the Aspire dashboard and select the `aspirec4` resource, or its **View LikeC4 Diagram** link.

## Upgrading from 13.5

13.6 is breaking for anything that reads AspireC4's generated files or addresses its dashboard
resources by name:

- the architecture model moved from `./likec4/gen/` to `./.aspirec4/likec4/`;
- the ER diagram moved from `./erd/gen/` to `./.aspirec4/erd/`;
- with a second diagram registered, the rows nest under a parent and the architecture diagram is
  renamed `aspirec4` → `aspirec4-likec4`.

To keep the previous layout, turn on **legacy mode**:

```csharp
builder.AddAspireC4(configure: options => options.WithLegacyLayout());
```

or, without code, set `"AspireC4": { "UseLegacyLayout": true }` in `appsettings.json` (or
`AspireC4__UseLegacyLayout=true`). That restores both paths and the flat `aspirec4` / `aspirec4-er`
names. It is a compatibility shim and will be removed in a future major version — see
`purview-aspirec4-configuration` for the caveats and the migration steps.

## A little configuration

```csharp
builder.AddAspireC4(options =>
    options
        .WithTitle("My distributed application")
        .WithViewTitle("Architecture")
        .WithViewDescription("Generated from the Aspire resource graph")
);
```

Options also bind from configuration under the `AspireC4` section, applied **on top of** the
builder-time snapshot — so `"AspireC4": { "ViewTitle": "Architecture" }` in `appsettings.json`, or a
matching environment variable, wins over the callback.

Two common adjustments:

```csharp
builder.AddAspireC4(options => options.WithHideFromDashboard());        // surface it as a link instead
builder.AddAspireC4(options => options.WithContainerImageTag("1.57"));  // pin the LikeC4 version
```

## Without Docker

```csharp
builder.AddAspireC4().WithLocalCLI();                      // auto-detect
builder.AddAspireC4().WithLocalCLI(LocalCLIRuntime.Bun);   // or pick one
```

The runtime must be on `PATH`. `Auto` tries, in order: **npx → pnpm → yarn → bun → deno**.

| Runtime | Command |
| --- | --- |
| `Npx` | `npx likec4 serve <dir> --port <port>` |
| `Pnpm` | `pnpm dlx --ignore-workspace likec4 serve …` |
| `Yarn` | `yarn dlx --package likec4 --package react --package react-dom likec4 serve …` |
| `Bun` | `bunx --bun likec4 serve …` |
| `Deno` | `deno run --allow-all --node-modules-dir=none npm:likec4 serve …` |

`Auto` **throws** a `DistributedApplicationException` when none is found — install one, or drop
`WithLocalCLI()` and use Docker.

Behaviour worth knowing:

- The output directory is passed as an **absolute** path, and the server runs with the system temp
  directory as its working directory, so a package manager does not walk up and treat the AppHost's
  parent `package.json` as a workspace root.
- `ContainerImageTag` is **ignored** — it only applies to the Docker container.
- Yarn's `dlx` does not install optional peer dependencies, so `react` and `react-dom` are passed
  explicitly.
- HMR is enabled with `--hmr-port` unless `DisableHMR` is set.

### `ConfigureServer`

```csharp
builder.AddAspireC4()
    .ConfigureServer(server => server.WithLikeC4Details(options => options.WithLabel("Architecture diagram")));
```

**Call it *after* `.WithLocalCLI()`.** Calling it first configures the Docker container that is then
replaced.

## TypeScript AppHosts

Supported, with their own fluent API. The source-generator validation described in
`purview-aspirec4-validation` is C#-only; a TypeScript AppHost uses the generated API instead.

## Related skills

`purview-aspirec4-configuration` for the full options reference,
`purview-aspirec4-resources` for per-resource labels, relationships and grouping,
`purview-aspirec4-validation` for the registry and the `ASPIREC4xxx` diagnostics, and
`purview-aspirec4-entityframework` for the Entity Framework Core ER diagram.
