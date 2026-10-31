# AspireC4.Hosting

[![NuGet version](https://img.shields.io/nuget/v/AspireC4.Hosting.svg)](https://www.nuget.org/packages/AspireC4.Hosting)

An [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) integration that generates a live
[LikeC4](https://likec4.dev) architecture diagram from your AppHost's resource graph, and hosts it in
the Aspire dashboard.

## Prerequisites

- **.NET 10 or later.** This package targets `net10.0`, so the AppHost that references it must too.
- **Aspire 13.5.3 or later.** The AppHost must reference both the AppHost SDK and
  `Aspire.Hosting.AppHost`.
- **Docker** (or Podman) for the default LikeC4 sidecar container.
- Optional: a local Node.js CLI runtime (`npx`, `pnpm`, `yarn`, `bun` or `deno`) if you call
  `.WithLocalCLI()` instead.

Container image exports verify bind sharing through `Purview.Containers.Environment`, using
`alpine:3.19` by default. Offline environments must preload the probe image or set
`CONTAINER_ENVIRONMENT_PROBE_IMAGE`. For a daemon inside WSL or a shared remote filesystem,
also install `Purview.Containers.Aspire` and call `builder.AddContainerEnvironment()`
to adapt the live sidecar's mounts. See the
[container environment guide](https://github.com/purview-dev/containers/blob/main/src/src/Environment/Sdk/README.md)
for verified path mappings, write permissions and remote-host limitations.

## Installation

Add the package to the **AppHost** project:

```bash
dotnet add package AspireC4.Hosting
```

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="AspireC4.Hosting" />
  </ItemGroup>
</Project>
```

## Quick start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4();

builder.AddProject<Projects.Api>("api");

builder.Build().Run();
```

When the AppHost starts, this writes `./.aspirec4/likec4/model.gen.c4` and starts the LikeC4 server.
The diagram refreshes as the Aspire app changes. Open it from the `aspirec4` resource in the dashboard.

## Screenshots

![AspireC4 architecture diagram of the sample AppHost: a Node app referencing Redis Commander, local Redis and Postgres in a local dev group, PgWeb, Azure Redis and Azure Postgres, with labelled relationships](https://raw.githubusercontent.com/purview-dev/aspirec4/main/docs/images/screenshots/architecture-diagram.png)

The image above is captured from the sample AppHost in the
[repository](https://github.com/purview-dev/aspirec4) and stored in `docs/images/screenshots/`. Your own
diagram exports a PNG for every view to `./.aspirec4/images/` on startup (`ExportImages`), which is the
easiest way to capture one.

## Configuration

Configure the diagram in the `configure` callback, or through the `AspireC4` configuration section.

```csharp
builder.AddAspireC4(configure: options => options
    .WithTitle("Orders platform")
    .WithViewTitle("Architecture")
    .WithHideFromDashboard());
```

| Property | Default | Description |
| --- | --- | --- |
| `Title` | `null` | Title shown in the LikeC4 app |
| `ViewTitle` | `"Architecture"` | Title of the generated view |
| `ViewDescription` | `null` | Optional view description |
| `OutputDirectory` | `"./.aspirec4/likec4/"` | Where the generated `.c4` file is written |
| `ProjectDirectory` | `null` | An existing LikeC4 project to generate into and serve |
| `FileName` | `"model.gen"` | Generated file name without extension |
| `DisableHMR` | `false` | Disable Hot Module Replacement |
| `HMRPort` | `null` | Fixed HMR port. Allocated dynamically by default |
| `ContainerImageTag` | `null` (`latest`) | Pin the `ghcr.io/likec4/likec4` image tag |
| `AutoIconsEnabled` | `true` | Infer LikeC4 icons from resource type and name |
| `HideFromDashboard` | `false` | Hide the LikeC4 sidecar from the dashboard |
| `IncludeAspireDashboardLinks` | `true` | Link diagram elements back to the Aspire dashboard |
| `ExportImages` | `true` | Export every view as an image to `./.aspirec4/images/` when the model changes |
| `ImageFormats` | `{Png}` | `Png` and/or `Jpg` |
| `UseLegacyLayout` | `false` | Restore the pre-13.6 output paths and dashboard names |

### Run LikeC4 locally instead of in a container

```csharp
builder.AddAspireC4().WithLocalCLI();
```

### Use your own LikeC4 project

Serve a LikeC4 project you already maintain. The generated model is added to it as one more file, and
your `likec4.config.json`, `.c4` sources and views are left alone.

```csharp
builder.AddAspireC4(configure: options => options
    .WithLikeC4ProjectFolder("../../architecture"));

builder.AddProject<Projects.Api>("api")
       .WithLikeC4Element("cloud.backend.api");   // attach live state to an element you declared
```

## Describing resources

```csharp
var db = builder.AddPostgres("pg").AddDatabase("orders");

builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details => details
        .WithLabel("Public API")
        .WithTechnology(".NET")
        .WithDescription("The public HTTP API surface.")
        .WithIcon("tech:dotnet")
        .WithTag("backend"))
    .WithLikeC4Reference(db, opts => opts.WithLabel("Persists orders"))   // also calls WithReference
    .WithLikeC4Group("Platform");

builder.AddContainer("debug-tools", "busybox")
    .ExcludeFromLikeC4();
```

## Compile-time validation

The package includes a source generator that checks the values passed to `.WithTag()`, `.WithKind()`,
`.WithLikeC4Group()` and `.WithMetadata()` against a registry class in your AppHost. You do not need
to reference anything else.

```csharp
using Aspire.Hosting.AspireC4;

[LikeC4Registry(Strict = LikeC4Severity.Warning)]
internal static class ArchitectureRegistry
{
    public static class Tags
    {
        public const string Backend = "backend";
    }

    public static class Groups
    {
        public const string Platform = "Platform";
    }
}
```

Set the project-wide severity with `<AspireC4Strict>warning</AspireC4Strict>`, and add your `.c4`
files as `<AdditionalFiles>` to validate against their `specification` blocks too. The diagnostics are
`ASPIREC4001` to `ASPIREC4007`.

## TypeScript AppHosts

The same API is exported to TypeScript AppHosts in camel case (`addAspireC4`, `withLikeC4Details`, …).
Add `"AspireC4.Hosting"` to the `packages` in `aspire.config.json` and run `aspire restore`.

## ER diagrams from Entity Framework Core

Add [`AspireC4.Hosting.EntityFrameworkCore`](https://www.nuget.org/packages/AspireC4.Hosting.EntityFrameworkCore)
to the AppHost and call `.WithEntityFrameworkDiagram()` to add a live ER diagram of your `DbContext`
types, linked from the architecture diagram.

It is **additive, and optional**: `AddAspireC4()` stays exactly as it is, and
`WithEntityFrameworkDiagram()` is an extension on the builder it returns, so the two diagrams run side by
side — or you keep the architecture diagram on its own and never install the package. The ER diagram has
its own renderers (`Interactive` by default, plus `Mermaid`, `Dbml` and `D2`) and options; see that
package's README.

## More information

- [Full documentation](https://purview.dev/docs/aspirec4/)
- [Repository, samples and the complete option reference](https://github.com/purview-dev/aspirec4)
