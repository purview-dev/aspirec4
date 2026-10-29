# AspireC4.Hosting.EntityFrameworkCore

[![NuGet version](https://img.shields.io/nuget/v/AspireC4.Hosting.EntityFrameworkCore.svg)](https://www.nuget.org/packages/AspireC4.Hosting.EntityFrameworkCore)

Adds a live **ER diagram of your Entity Framework Core `DbContext` types** to an
[Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) AppHost, alongside the
[AspireC4](https://www.nuget.org/packages/AspireC4.Hosting) architecture diagram and linked from it.

You install it in the **AppHost** and call one method. Your service projects need no changes.

## Prerequisites

- An Aspire 13.5+ AppHost already using [`AspireC4.Hosting`](https://www.nuget.org/packages/AspireC4.Hosting)
  (this package depends on it, so installing this one brings it in).
- The projects that own your `DbContext` types are registered in the AppHost with
  `AddProject<Projects.X>(...)`.
- Docker (or Podman) for the default viewer sidecar. See [Without a container runtime](#without-a-container-runtime)
  if you have neither.

Container image exports verify bind sharing through `Purview.Containers.Environment`; preload
`alpine:3.19` offline or set `CONTAINER_ENVIRONMENT_PROBE_IMAGE`. For WSL or shared remote
daemons, install `Purview.Containers.Aspire` and call `builder.AddContainerEnvironment()`
to adapt the viewer sidecar's mounts as well. See the
[container environment guide](https://github.com/purview-dev/containers/blob/main/src/src/Environment/Sdk/README.md)
for mapping configuration, marker write permissions and remote-host limitations.

## Enabling DbContext modelling

### 1. Add the package to the AppHost

```bash
dotnet add package AspireC4.Hosting.EntityFrameworkCore
```

### 2. Register the projects that own your `DbContext` types

The diagram is built from **Aspire project resources**. Every project you add with `AddProject` is
scanned for `DbContext` types, including contexts that live in class libraries the project references.
A project that is only a `<ProjectReference>`, or that runs as a container or executable, is not scanned.

```xml
<!-- AppHost.csproj: the usual Aspire project reference -->
<ItemGroup>
  <ProjectReference Include="..\Orders.Api\Orders.Api.csproj" />
</ItemGroup>
```

### 3. Call `WithEntityFrameworkDiagram()`

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4()
       .WithEntityFrameworkDiagram();

builder.AddProject<Projects.Orders_Api>("orders-api");

builder.Build().Run();
```

Run the AppHost. The dashboard shows an **`aspirec4-er`** resource with a **View ER Diagram** link, and
each project that contributed a context gets an **ER Diagram** link. The project's element in the
architecture diagram gets one too. Output is written to `./.aspirec4/erd/` beside the AppHost.

The model is read from each project's **build output** (`bin/`), so a project must have been built
before the AppHost starts. Building the AppHost builds its referenced projects, so normally you do not
need to do anything.

### 4. (Optional) Increase fidelity

Without any change to your services, the model is recovered from assembly metadata. That covers
conventions and data annotations but **not** fluent `OnModelCreating` configuration, because a method
body is not in the metadata. Two opt-in packages close that gap. Each is enabled just by adding the
package reference:

| Add this package | To this project | And you get |
| --- | --- | --- |
| *(nothing)* | — | `Approximate`: `DbSet<T>`, navigations, data annotations, EF naming conventions |
| [`AspireC4.EntityFrameworkCore`](https://www.nuget.org/packages/AspireC4.EntityFrameworkCore) | The project that declares the `DbContext` | `Full`: adds fluent configuration (`ToTable`, `HasKey`, `HasIndex`, `HasColumnName`, …) |
| [`AspireC4.EntityFrameworkCore.ModelDump`](https://www.nuget.org/packages/AspireC4.EntityFrameworkCore.ModelDump) | The project that references the EF Core **provider** and has an `IDesignTimeDbContextFactory<T>` | `Full`: the real EF `IModel`, with value converters and composite foreign keys |

```bash
# In the project that declares the DbContext
dotnet add package AspireC4.EntityFrameworkCore

# In the project that owns the EF Core provider and a design-time factory
dotnet add package AspireC4.EntityFrameworkCore.ModelDump
```

When a context is available from more than one source, the best one is used: **dump**, then
**descriptor**, then **metadata**. Each context in the diagram records its fidelity, so an approximate
model is never mistaken for a complete one.

## Choosing which contexts appear

By default every project resource is scanned. To narrow that down:

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options
           .WithProjectOptInOnly()                         // only projects marked below
           .WithExcludedContexts("IdentityDbContext"));    // short or fully-qualified name

builder.AddProject<Projects.Orders_Api>("orders-api")
       .WithEntityFrameworkModel(options => options
           .WithIncludedContexts("OrdersDbContext"));      // per-project filter

builder.AddProject<Projects.Billing_Api>("billing-api")
       .WithEntityFrameworkModel(label: "Billing");        // label the project's group
```

Names match case-insensitively, and an exclusion always wins over an inclusion. A context that reaches
the diagram through several projects is shown once.

## Renderers

| `Renderer` | Output | Use it for |
| --- | --- | --- |
| `Interactive` *(default)* | `.json` + a browser viewer | Exploring the schema: search, column-level edges, crow's-foot cardinality, PNG/SVG export |
| `Mermaid` | `.mmd` + preview | Pasting into GitHub, Azure DevOps or a wiki |
| `Dbml` | `.dbml` | dbdiagram.io and other DBML tooling |
| `D2` | `.d2` + a live `d2 --watch` sidecar | A live d2 view |

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options.Renderer = ErdRenderer.Mermaid);
```

The interactive and Mermaid viewers are static files served by a small `nginx` sidecar, so the
dashboard has a real URL to link to. Their JavaScript ships inside this package and works offline.

## Configuration

Set options in the `configure` callback or in the `AspireC4:EntityFramework` configuration section.

| Property | Default | Description |
| --- | --- | --- |
| `Renderer` | `Interactive` | `Interactive`, `Mermaid`, `Dbml` or `D2` |
| `Title` | `null` | Title shown in the diagram |
| `DiscoverAllProjects` | `true` | Scan every project resource. `WithProjectOptInOnly()` restricts this to projects marked with `WithEntityFrameworkModel()` |
| `IncludedContexts` / `ExcludedContexts` | empty | `WithIncludedContexts(...)` / `WithExcludedContexts(...)` |
| `OutputDirectory` | `"./.aspirec4/erd/"` | Where generated files are written, relative to the AppHost |
| `FileName` | `"schema.gen"` | Generated file name without extension |
| `DomainTypeDisplay` | `DomainTypeFirst` | How value objects and enums appear beside the store type: `AssetId (guid)`, `ActorType (int, enum)` |
| `ShowNullability` | `true` | Mark nullable columns with `?` |
| `GroupBySchema` / `GroupByContext` | `true` | Group tables by database schema, and by `DbContext` when there are several |
| `UseSubgraphs` | `true` | Mermaid only: one `subgraph` per `DbContext` (Mermaid 11.17+) |
| `LayoutEngine` | `Elk` | D2 only: `Dagre`, `Elk` or `Tala` |
| `UseViewerServer` | `true` | Serve the viewer from a container. Set `false` where there is no container runtime |
| `HideFromDashboard` | `false` | Hide the diagram's sidecar from the dashboard |
| `ExportImages` | `true` | Write `schema.svg` / `schema.png` to `./.aspirec4/images/` when the model changes |
| `ImageFormats` | `{Svg, Png}` | `WithImageExport(...)`, or `WithoutImageExport()` to turn export off |
| `UseViewerFidelityExport` | `false` | Render images through the interactive viewer in a headless browser. Pulls a ~3.7 GB Playwright image |
| `D2Command` | `null` | A local `d2` binary for image export, instead of the container |
| `ContainerRuntimeCommand` | `"docker"` | Set to `podman` where Aspire uses Podman |

Set `OutputDirectory` in the callback rather than in configuration. The sidecar's bind mount is fixed
when the AppHost builder runs, so a value from configuration changes where files are written but not
what the sidecar serves.

## Without a container runtime

```csharp
// Generate the files without starting a viewer container, and render images with a local d2
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options =>
       {
           options.UseViewerServer = false;   // the dashboard offers an "open locally" command instead
           options.D2Command = "d2";          // or call options.WithoutImageExport()
       });

// Or, with the D2 renderer, replace the d2 container with a local binary
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options.Renderer = ErdRenderer.D2)
       .WithLocalD2CLI();                     // must come after WithEntityFrameworkDiagram
```

d2 is a Go binary. Install it with scoop, Homebrew, `install.sh` or `go install`.

## Troubleshooting

- **No diagram content.** Check the project is added with `AddProject<Projects.X>` and has been built.
  The `aspirec4-er` resource's log names each project where no model was found.
- **Tables are missing their fluent configuration.** The context was read from metadata
  (`Approximate`). Add `AspireC4.EntityFrameworkCore` to the project that declares it.
- **A composite or fluent-only relationship is not drawn.** Only the model dump sees these. Add
  `AspireC4.EntityFrameworkCore.ModelDump` to the project with the provider and a design-time factory.
- **Not captured from any static source:** owned types, inheritance mapping, delete behaviour,
  sequences, check constraints. Relationships across `DbContext` boundaries are never drawn.

## More information

- [Full documentation](https://purview.dev/docs/aspirec4/)
- [Repository and samples](https://github.com/purview-dev/aspirec4)
