# Entity Framework Core ER Diagrams

`AspireC4.Hosting.EntityFrameworkCore` adds a **second, optional diagram** to AspireC4: a live ER diagram
of your Entity Framework Core `DbContext` types, hosted beside the architecture diagram and linked from
it.

It is **additive** — `AddAspireC4()` is always the base, and `WithEntityFrameworkDiagram()` adds the ER
diagram to the builder it returns. You can run the architecture diagram on its own and never install this
package; there is no way to run the ER diagram without the architecture diagram.

![AspireC4 interactive ER diagram of the sample AppHost: five DbContext groups of tables with primary and foreign key badges and crow's-foot relationship edges](https://raw.githubusercontent.com/purview-dev/aspirec4/main/docs/images/screenshots/entity-framework-er-diagram.png)

## Packages and target frameworks

| Package | Target framework | Add to | Why |
| --- | --- | --- | --- |
| `AspireC4.Hosting` | `net10.0` | the AppHost | the architecture diagram this one hangs off. Required first |
| `AspireC4.Hosting.EntityFrameworkCore` | `net10.0` | the AppHost | the ER diagram itself |
| `AspireC4.EntityFrameworkCore` | `netstandard2.0` (analyzer-only) | a project **you own** that declares a `DbContext` | captures fluent `OnModelCreating` configuration |
| `AspireC4.EntityFrameworkCore.ModelDump` | `net10.0` (build-time tool) | the project that owns the EF Core **provider** and a design-time factory | the authoritative model, composite foreign keys included |

Both libraries target `net10.0`, so the AppHost must too. The analyzer package is `netstandard2.0` and
ships only under `analyzers/dotnet/cs/`: it adds no runtime assembly, so it works whatever framework the
owning project targets. The dump tool runs as `dotnet <tool>.dll` from an MSBuild `Exec`, so the build
machine needs a .NET 10 runtime.

## Enabling it

### 1. Add the package to the AppHost

```bash
dotnet add package AspireC4.Hosting.EntityFrameworkCore
```

### 2. Register the projects that own your `DbContext` types

The diagram is built from **Aspire project resources**. Every project added with
`AddProject<Projects.X>(...)` is scanned for `DbContext` types, including contexts that live in class
libraries the project references. A project that is only a `<ProjectReference>`, or that runs as a
container or executable, is not scanned.

### 3. Call `WithEntityFrameworkDiagram()`

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4()
       .WithEntityFrameworkDiagram();

builder.AddProject<Projects.Orders_Api>("orders-api");

builder.Build().Run();
```

Run the AppHost. The dashboard shows both diagrams under a shared `aspirec4` parent row, each project that
contributed a context gets an **ER Diagram** link, and the project's element in the architecture diagram
gets one too. Output is written to `./.aspirec4/erd/` beside the AppHost.

The model is read from each project's **build output** (`bin/`), so a project must have been built before
the AppHost starts. Building the AppHost builds its referenced projects, so normally you do not need to do
anything.

## How the model is read

Each context is recovered from the best source available, and the diagram records which one was used:

| Source | When | Fidelity |
| --- | --- | --- |
| **Dump** | the project, or one it references, references `AspireC4.EntityFrameworkCore.ModelDump` | `Full` — the real EF model, value converters and composite foreign keys included |
| **Descriptor** | the project references the analyzer-only `AspireC4.EntityFrameworkCore` package | `Full` — includes fluent `OnModelCreating` configuration |
| **Metadata** | otherwise | `Approximate` — conventions and data annotations only |

The metadata reader opens the built assembly with a reflection-only load context, so no code runs. It
cannot see `OnModelCreating` — a method body is absent from metadata — so a project without a package
still appears, but fluent renames, default schemas and `Ignore` calls do not. A shared or third-party
assembly you cannot instrument contributes at this fidelity.

Every context carries its fidelity in the diagram, so an approximate model is never mistaken for a
complete one. When the same context reaches the diagram through several projects, the higher-fidelity
description wins.

### The build-time dump (opt-in)

Reference `AspireC4.EntityFrameworkCore.ModelDump` from the project that owns the EF Core provider; that
reference **is** the opt-in. After every build the tool builds each context's real `IModel` through its
`IDesignTimeDbContextFactory<T>` and writes the diagram contract to `obj/aspirec4/ef/`. That directory is
never copied to `bin/` or `publish/` and is gitignored, so the dump has no deployment footprint. The
AppHost reads it from that project and every project it references, and falls back when it is missing or
older than the built assembly.

Set `<AspireC4EfModelDumpConnectionString>` when the design-time factory needs one to build its options —
the model is never opened, so any syntactically valid value works. A dump failure never fails the build.

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

Names match case-insensitively and an exclusion always wins over an inclusion. This is how a context
shared by several projects — an `IdentityDbContext` in a common library nobody owns — is kept out without
editing that library.

## Renderers

| `Renderer` | Output | Use it for |
| --- | --- | --- |
| `Interactive` *(default)* | `.json` + browser viewer | exploring the schema: search, column-level edges, crow's-foot cardinality, PNG/SVG export |
| `Mermaid` | `.mmd` + preview | pasting into GitHub, Azure DevOps or a wiki |
| `Dbml` | `.dbml` | dbdiagram.io and other DBML tooling |
| `D2` | `.d2` + a live d2 sidecar | a live d2 view |

Every text format is a lossy projection of the model; only the interactive renderer consumes the
descriptor directly, which is why it is the default.

The interactive and Mermaid viewers are static files served by a small `nginx` sidecar, so the dashboard
has a real URL to link to. Set `UseViewerServer = false` where there is no container runtime: the diagram
is still generated and the dashboard offers a command that opens it locally.

## Configuration

Set options in the `configure` callback or in the `AspireC4:EntityFramework` configuration section.

| Property | Default | Description |
| --- | --- | --- |
| `Renderer` | `Interactive` | `Interactive`, `Mermaid`, `Dbml` or `D2` |
| `Title` | `null` | title shown in the diagram |
| `DiscoverAllProjects` | `true` | scan every project resource. `WithProjectOptInOnly()` restricts it |
| `IncludedContexts` / `ExcludedContexts` | empty | `WithIncludedContexts(...)` / `WithExcludedContexts(...)` |
| `OutputDirectory` | `"./.aspirec4/erd/"` | where generated files are written, relative to the AppHost |
| `FileName` | `"schema.gen"` | generated file name without extension |
| `DomainTypeDisplay` | `DomainTypeFirst` | how a value object or enum shows beside the store type: `AssetId (guid)`, `ActorType (int, enum)` |
| `ShowNullability` | `true` | mark nullable columns with `?` |
| `GroupBySchema` / `GroupByContext` | `true` | group tables by database schema, and by `DbContext` when there are several |
| `UseSubgraphs` | `true` | Mermaid only: one `subgraph` per `DbContext` (Mermaid 11.17+) |
| `LayoutEngine` | `Elk` | D2 only: `Dagre`, `Elk` or `Tala` |
| `UseViewerServer` | `true` | serve the viewer from a container |
| `HideFromDashboard` | `false` | hide the diagram's sidecar from the dashboard |
| `ExportImages` | `true` | write `schema.svg` / `schema.png` to `./.aspirec4/images/` when the model changes |
| `ImageFormats` | `{Svg, Png}` | `WithImageExport(...)`, or `WithoutImageExport()` to turn export off |
| `UseViewerFidelityExport` | `false` | render images through the interactive viewer in a headless browser. Pulls a ~3.7 GB Playwright image |
| `D2Command` | `null` | a local `d2` binary for image export, instead of the container |
| `ContainerRuntimeCommand` | `"docker"` | set to `podman` where Aspire uses Podman |

> [!IMPORTANT]
> Set `OutputDirectory` in the callback rather than in configuration. The sidecar's bind mount is fixed
> when the AppHost builder runs, so a value from configuration changes where files are written but not
> what the sidecar serves.

### Without a container runtime

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

d2 is a Go binary; install it with scoop, Homebrew, `install.sh` or `go install`.

## Exporting images

Both diagrams export to **one folder**, `./.aspirec4/images/`, so there is a single place to look. The ER
diagram writes `schema.svg` and `schema.png` (renamed by `FileName`) whenever the model changes, and never
on `aspire publish`.

The ER export is fully cached: it is derived from the EF model alone, so restarting the AppHost over an
unchanged schema renders nothing. A stamp file beside the images records what they were rendered from.

`UseViewerFidelityExport` makes the file identical to the viewer's own **Save as SVG / PNG** support by
loading the generated viewer in a headless browser and calling the same function the toolbar uses. It is
off by default because it pulls `mcr.microsoft.com/playwright` (around 3.7 GB) and takes seconds rather
than milliseconds. It applies only to the `Interactive` renderer.

Prefer SVG for a large schema — a raster of the whole thing is unreadable at any sensible size. The
images folder is also the place to take a screenshot from: the one on this page is a viewer capture of the
sample AppHost's schema, kept in `docs/images/screenshots/`.

## Dashboard

With only the architecture diagram registered, `aspirec4` is a single flat row. Registering the ER diagram
nests both under a shared parent:

```text
aspirec4                 # parent row, added only when there is more than one diagram
├── aspirec4-likec4      # the architecture diagram, renamed as it is nested
└── aspirec4-er          # the ER diagram, with its own sidecar nested beneath it
```

The ER diagram row carries an **Images** property with the resolved export path and an **Open Diagram
Images** command that opens the folder. Grouping and the `-likec4` rename arrived in 13.6;
`WithLegacyLayout()` keeps the flat `aspirec4` / `aspirec4-er` pair — see [Migration](Migration.md).

## Troubleshooting

- **No diagram content.** Check the project is added with `AddProject<Projects.X>` and has been built.
  The `aspirec4-er` resource's log names each project where no model was found.
- **Tables are missing their fluent configuration.** The context was read from metadata
  (`Approximate`). Add `AspireC4.EntityFrameworkCore` to the project that declares it.
- **A composite or fluent-only relationship is not drawn.** Only the model dump sees these. Add
  `AspireC4.EntityFrameworkCore.ModelDump` to the project with the provider and a design-time factory.
- **Not captured from any static source:** owned types, inheritance mapping, delete behaviour, sequences,
  check constraints. Relationships across `DbContext` boundaries are never drawn.
- **The runner, not the product.** Running `AspireC4.IntegrationTests` from an IDE test runner fails
  Aspire's project-resource start-up, which surfaces as an empty ER model. Use `just test-integration`.

## Next pages

- [Getting Started](Getting-Started.md)
- [Generated Output](Generated-Output.md)
- [Dashboard Integration](Dashboard-Integration.md)
- [Configuration](Configuration.md)

