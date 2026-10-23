# AspireC4.Hosting

[![NuGet version](https://img.shields.io/nuget/v/AspireC4.Hosting.svg)](https://www.nuget.org/packages/AspireC4.Hosting)
[![Release](https://github.com/purview-dev/aspirec4/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/aspirec4/actions/workflows/release.yml)

**AspireC4.Hosting** is an [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) extension library that generates live [LikeC4](https://likec4.dev) diagrams from the Aspire resource graph.

## Prerequisites

- **.NET 8 or later**.
- **Aspire 13.5.3 or later**. Aspire AppHost projects must reference both the AppHost SDK and
  `Aspire.Hosting.AppHost`.
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

## Quick start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4();

builder.Build().Run();
```

This writes `./.aspirec4/likec4/model.gen.c4`, starts the LikeC4 server, and refreshes the diagram as the Aspire app changes.

> [!NOTE]
> **Upgrading from 13.5 or earlier?** Generated output moved to `./.aspirec4/` and, once a second
> diagram is registered, the architecture diagram's dashboard resource is renamed. For an upgrade that
> changes nothing else, call `options.WithLegacyLayout()` — it restores both the old paths
> (`./likec4/gen/`, `./erd/gen/`) and the flat `aspirec4` / `aspirec4-er` names. See
> [Upgrading from before 13.6](#upgrading-from-before-136).

## Configuration

Configure the diagram through `AspireC4DiagramOptions`:

| Property | Default | Description |
| --- | --- | --- |
| `Title` | `null` | Title shown in the LikeC4 app |
| `ViewTitle` | `"Architecture"` | Title shown in the generated view |
| `ViewDescription` | `null` | Optional view description |
| `OutputDirectory` | `"./.aspirec4/likec4/"` | Directory where the generated `.c4` file is written |
| `ProjectDirectory` | `null` | An existing LikeC4 project to generate into and serve — see [Using your own LikeC4 project](#using-your-own-likec4-project) |
| `UseLegacyLayout` | `false` | Restores the pre-13.6 paths and dashboard names — see [Upgrading from before 13.6](#upgrading-from-before-136) |
| `FileName` | `"model.gen"` | Generated file name without extension |
| `DisableHMR` | `false` | Disable Hot Module Replacement |
| `HMRPort` | `null` (dynamically allocated) | HMR port used by the LikeC4 server and browser when it supports configurable HMR ports; set a fixed port when needed |
| `ContainerImageTag` | `null` (`latest`) | Pin the `ghcr.io/likec4/likec4` image tag |
| `AutoIconsEnabled` | `true` | Infer LikeC4 icons from resource type and name |
| `HideFromDashboard` | `false` | Hide the LikeC4 sidecar from the Aspire dashboard |
| `DashboardLinkDisplayName` | `"Architecture Diagram"` | Name used for the diagram link when hidden from the dashboard |
| `IncludeAspireDashboardLinks` | `true` | Add Aspire dashboard links to diagram elements |
| `ExportImages` | `true` | Write every view out as an image whenever the model changes — see [Export the views as images](#export-the-views-as-images) |
| `ImageFormats` | `{Png}` | `Png` and/or `Jpg`. **No SVG** — `likec4 export` does not offer one |
| `ImageExportDirectory` | `null` (`./.aspirec4/images/`) | Where images are written; relative paths resolve against the AppHost directory. Shared with the ER diagram |
| `ImageExportTimeoutSeconds` | `300` | How long to wait for one `likec4 export` invocation |
| `ContainerRuntimeCommand` | `"docker"` | Runtime for the one-shot export container; `podman` where that is what Aspire uses |

## Common options

### Local CLI

```csharp
builder.AddAspireC4().WithLocalCLI();
```

### Hide from the dashboard

```csharp
builder.AddAspireC4(options => options.WithHideFromDashboard());
```

### Disable HMR

```csharp
builder.AddAspireC4(options => options.WithHMRDisabled());
```

### Exclude the sidecar from the diagram

The LikeC4 sidecar is excluded automatically. Set `WithIncludeAspireC4InternalResource(true)` in the options callback if you want to inspect it.

### Export the views as images

On by default. Every view is written out as a PNG whenever the generated model changes, so a diagram can
be attached to a pull request or pasted into a wiki without running the server.

**Both diagrams export into one folder**, `./.aspirec4/images/`, so there is a single place to look:

```text
apphost/.aspirec4/images/
  architecture-index.png        <- architecture diagram, one file per view
  architecture-services.png
  schema.svg                    <- ER diagram
  schema.png
```

The architecture diagram's files carry an `architecture-` prefix, because `likec4 export` names each
file after its view id and a bare `index.png` says nothing in a shared folder. The ER diagram's row in
the dashboard carries an **Images** property with the resolved path, and an **Open Diagram Images**
command that opens the folder — the dashboard cannot navigate to a `file://` path itself, so the
AppHost opens it for you.

`ImageExportDirectory` is resolved from the **AppHost directory**, not from `OutputDirectory`, which is
what lets both diagrams land in the same place. Setting it on only one of the two splits them apart.

```csharp
builder.AddAspireC4(options => options.WithoutImageExport());            // opt out
builder.AddAspireC4(options => options
    .WithImageExport(LikeC4ImageFormat.Png, LikeC4ImageFormat.Jpg)
    .WithImageExportDirectory("docs/diagrams")   // relative to the AppHost directory
);
```

**There is no SVG option for the architecture diagram.** `likec4 export` renders by screenshotting each
view in a headless browser and offers only `png` and `jpg`. The ER diagram *does* export SVG, because it
renders through d2 instead.

That headless browser has two consequences. Export takes tens of seconds and needs a Chromium: container
mode (the default) gets one from the `ghcr.io/likec4/likec4` image, which already carries it, while
`WithLocalCLI()` mode needs a Playwright Chromium installed on the host. And it runs in the background,
so startup is never delayed. A failure is reported and leaves the generated `.c4` file untouched.

#### When export actually runs

Nothing is rendered — and **no container is started** — unless the output would differ:

- **Unchanged model, same session.** The resource state churn that regenerates an identical model
  exports nothing.
- **Restart.** A stamp file beside the images records what they were rendered from, so restarting the
  AppHost over an unchanged model runs nothing. Without it, every restart would re-render, because the
  in-memory change detection starts empty each time.
- **Changed formats, or deleted images.** Both re-export: the stamp covers the requested formats, and
  is only trusted when the files it describes are still there.
- **Publish.** `aspire publish` generates the model and manifest and exports nothing at all. An export
  is fire-and-forget, so on publish it would be killed part-way through — and publish is expected to be
  hermetic, which reaching for a container runtime a build agent may not have would break.

#### Committing the images

The images are the one piece of generated output worth tracking: a diagram that shows up in a pull
request is documentation, and because export only runs when the diagram changes they do not churn.
Ignore the generated directory but carve the images out of it:

```gitignore
# Note the trailing /* rather than /: git will not descend into an excluded directory, so the
# children have to be excluded individually for the re-include to be reachable.
**/.aspirec4/*
!**/.aspirec4/images/
# The stamp recording what the images were rendered from is a local cache, not documentation.
**/.aspirec4/images/.aspirec4-*
```

The two diagrams differ in how well they cache, though both are stable in source control:

- **ER diagram** — fully cached. Derived from your EF model alone, so restarting the AppHost over an
  unchanged schema renders nothing at all. This is the one that matters most, since it is also the one
  that can pull a browser image under `WithViewerFidelityExport()`.
- **Architecture diagram** — re-rendered on each run, but to identical bytes. Its model is a picture of
  a *running* system: the endpoint ports and the run-state tags settle during startup, so the model the
  first export sees is not quite the one the last export sees. The stamp already ignores the ephemeral
  ports; the remaining difference is the run states, and those genuinely change the picture, so they
  are not normalised away. Measured across restarts the exported PNGs are byte-for-byte the same, so
  git sees no change — the cost is a background render, not a dirty tree.

Set `ContainerRuntimeCommand` to `podman` where that is the runtime Aspire itself is using.

## Upgrading from before 13.6

13.6 changed two things, both breaking:

- Generated output moved under one root: `./.aspirec4/likec4/` (was `./likec4/gen/`) and
  `./.aspirec4/erd/` (was `./erd/gen/`). Replace your `.gitignore` entries with `**/.aspirec4/`.
- When more than one diagram is registered, they nest under a shared dashboard parent and the
  architecture diagram's resource is renamed from `aspirec4` to `aspirec4-likec4`. With only
  `AddAspireC4()`, nothing changes.

### Legacy mode (keeping the pre-13.6 layout)

Both changes revert together with one switch, for a zero-change upgrade:

```csharp
builder.AddAspireC4(configure: options => options.WithLegacyLayout());
```

or, without touching code:

```json
{
  "AspireC4": {
    "UseLegacyLayout": true
  }
}
```

With legacy mode on, the architecture diagram writes to `./likec4/gen/`, the ER diagram to
`./erd/gen/`, and the diagrams stay flat in the dashboard as `aspirec4` and `aspirec4-er` with no
parent row. An `OutputDirectory` you set explicitly still wins over the legacy default, and the switch
does not affect `WithLikeC4ProjectFolder(...)` — generated output always stays beneath a hand-authored
project.

It is a compatibility shim that will be removed in a future major version — prefer adopting the new
layout. The full details, including what to update when you drop it, are in the
[migration guide](docs/wiki/Migration.md).

## Using your own LikeC4 project

Point AspireC4 at a LikeC4 project you already maintain and it serves that directory instead of its own:
your `likec4.config.json`, `.c4` sources and views are all in play, and the generated model joins them as
one more file. AspireC4 writes to `.aspirec4/` beneath the project and never generates a config over yours.

```csharp
builder.AddAspireC4(configure: options => options
    .WithLikeC4ProjectFolder("../../architecture"));

builder.AddProject<Projects.Api>("api")
       // Attach Aspire's live state to the element the project already declares,
       // rather than adding a duplicate beside it.
       .WithLikeC4Element("cloud.backend.api");
```

The generated model emits an `extend` block for a mapped resource:

```likec4
model {
  extend cloud.backend.api {
    #aspire-run-state-running
    link http://localhost:5123 'Endpoint: http'
    metadata { aspire-name 'api' }
  }
}
```

Only tags, links, metadata, relationships and nested elements are applied — LikeC4's `extend` body accepts
nothing else, so your project stays the single source of each element's title, technology, description and
icon. See [Generated Output](docs/wiki/Generated-Output.md) for the details.

## Entity Framework Core ER diagrams (d2)

`AspireC4.Hosting.EntityFrameworkCore` adds a second, complementary diagram: a live
[d2](https://d2lang.com) `sql_table` ER diagram generated from your Entity Framework Core `DbContext`
types, hosted alongside the architecture diagram and linked from it.

The AppHost package is all that is required. `WithEntityFrameworkDiagram()` finds every associated
`DbContext` automatically — reading a build-time dump or a source-generated descriptor where a project
carries one, and falling back to the assembly's metadata otherwise:

```bash
# AppHost project
dotnet add package AspireC4.Hosting.EntityFrameworkCore
```

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options
           .LayoutEngine = D2LayoutEngine.Elk);

builder.AddProject<Projects.Api>("api");

builder.Build().Run();
```

Two packages upgrade a project's fidelity, and both are opt-in by reference alone — see
[How the model is read](#how-the-model-is-read):

```bash
# The project that owns the DbContext and that you control: captures fluent configuration
dotnet add package AspireC4.EntityFrameworkCore

# The project that owns the EF Core provider and a design-time DbContext factory:
# writes the real model, composite keys included
dotnet add package AspireC4.EntityFrameworkCore.ModelDump
```

This writes `./.aspirec4/erd/schema.gen.d2`, starts a `d2lang/d2` sidecar running `d2 --watch`, and
adds an **ER Diagram** link to each contributing project in the dashboard and on its element in the
architecture diagram.

### Dashboard layout

Both diagrams write beneath a single `./.aspirec4/` root in the AppHost directory — one directory to
ignore in source control regardless of how many are enabled.

> [!NOTE]
> The ER diagram's `./.aspirec4/erd/` default follows the architecture diagram's legacy switch:
> `WithLegacyLayout()` restores `./erd/gen/` too, because the two diagrams must agree. An
> `OutputDirectory` set on `WithEntityFrameworkDiagram` still wins. See
> [Legacy mode](#legacy-mode-keeping-the-pre-136-layout).

With only the architecture diagram registered, the dashboard shows the one `aspirec4` row it always
has. Registering a second diagram nests them under a shared parent instead:

```text
aspirec4                 # parent row, added only when there is more than one diagram
├── aspirec4-likec4      # the architecture diagram
└── aspirec4-er          # the ER diagram
```

### Configuration

| Property | Default | Description |
| --- | --- | --- |
| `Renderer` | `Interactive` | `Interactive`, `Mermaid`, `Dbml` or `D2` — see [Renderers](#renderers) |
| `Title` | `null` | Title shown in the generated diagram |
| `DiscoverAllProjects` | `true` | Scan every project resource for `DbContext` types. `WithProjectOptInOnly()` restricts it to projects marked with `WithEntityFrameworkModel()` |
| `IncludedContexts` | empty | When non-empty, only these contexts appear. `WithIncludedContexts(...)` |
| `ExcludedContexts` | empty | Contexts to omit, matched by short or fully-qualified name. `WithExcludedContexts(...)` |
| `OutputDirectory` | `"./.aspirec4/erd/"` | Directory the generated files are written to. Follows the architecture diagram's legacy switch when left at its default |
| `FileName` | `"schema.gen"` | Generated file name without extension |
| `UseSubgraphs` | `true` | Mermaid only — group entities into one `subgraph` per `DbContext` (needs Mermaid 11.17+) |
| `LayoutEngine` | `Elk` | D2 only. `Dagre`, `Elk` or `Tala`. **dagre cannot target `sql_table` rows** |
| `ShowNullability` | `true` | Render nullable columns with a trailing `?` |
| `GroupBySchema` | `true` | Group tables into d2 containers by database schema |
| `GroupByContext` | `true` | Group by `DbContext` when more than one is discovered |
| `DomainTypeDisplay` | `DomainTypeFirst` | How a column shows its domain type — a value object or an enum — beside the database type: `DomainTypeFirst` (`AssetId (guid)`, `ActorType (int, enum)`), `DatabaseFirst`, `DomainTypeOnly`, `DatabaseOnly` |
| `ContainerImageTag` | `null` (`v0.9.0`) | Pin the [`d2lang/d2`](https://hub.docker.com/r/d2lang/d2/) image tag |
| `UseViewerServer` | `true` | Serve the viewer so the dashboard can link to it; set `false` where there is no container runtime |
| `ViewerServerImageTag` | `null` (`1.29-alpine`) | Pin the `nginx` image tag used to serve the viewer |
| `HideFromDashboard` | `false` | Hide the d2 sidecar from the Aspire dashboard |
| `DashboardLinkDisplayName` | `"ER Diagram"` | Name used for the diagram link |
| `ExportImages` | `true` | Write the diagram out as an image whenever it changes — see [Exporting the ER diagram as an image](#exporting-the-er-diagram-as-an-image) |
| `ImageFormats` | `{Svg, Png}` | Formats to export. `WithImageExport(...)` |
| `ImageExportDirectory` | `null` (`./.aspirec4/images/`) | Where images are written; relative paths resolve against the AppHost directory. Shared with the architecture diagram |
| `D2Command` | `null` | A local `d2` binary to export with; `null` runs the container. Set by `WithLocalD2CLI()` |
| `ContainerRuntimeCommand` | `"docker"` | Runtime for the one-shot export container; `podman` where that is what Aspire uses |
| `ExportTimeoutSeconds` | `120` | How long to wait for one d2 invocation before abandoning it |
| `UseViewerFidelityExport` | `false` | Render images through the viewer in a headless browser so they match its own export. Pulls a ~3.7 GB browser image; `Interactive` only |
| `ViewerExportImage` | `null` | Override the browser image, including its tag |
| `ViewerExportTimeoutSeconds` | `600` | How long to wait for a viewer-fidelity export, which may be pulling the image |

### Renderers

Every text format is a **lossy projection** of the model. Only the interactive renderer consumes the
descriptor directly, which is why it is the default.

| Renderer | Output | Fidelity | Use it for |
| --- | --- | --- | --- |
| `Interactive` *(default)* | `.json` + viewer | **Full** — column-level edges, grouping, key badges, crow's-foot cardinality | The primary experience |
| `Dbml` | `.dbml` | High — column-level refs and precision types survive | Interchange: dbdiagram.io, SQL generation |
| `Mermaid` | `.mmd` + preview | Low — edges join entities, not columns; one flat grouping level | Publishing: GitHub, Azure DevOps, wikis |
| `D2` | `.d2` + live sidecar | High — column-level edges, nested containers | Live local viewing |

Known limitations, by renderer:

- **Mermaid** emits a local preview alongside the `.mmd` for eyeballing the output before pasting it
  somewhere that renders Mermaid natively. The preview is deliberately minimal — render, pan, zoom,
  theme and export — because Mermaid's job here is publishing, not being the primary experience. It cannot attach an edge
  to a column, so the participating columns are carried in the edge label instead. Its attribute-type grammar permits parentheses but **not commas**, so `decimal(18,2)`
  is emitted as `decimal(18_2)` — the precision is kept rather than dropped. Grouping is one flat
  `subgraph` level, so the schema tier is lost. GitHub was confirmed to render the subgraph output in
  October 2026; set `UseSubgraphs = false` for a renderer pinned to a Mermaid older than 11.17.
- **DBML** renders nowhere on its own — it is an interchange format for tools such as dbdiagram.io.
- **D2** needs the container (or a local `d2` binary) and renders nowhere else.
- **Interactive** needs its JavaScript dependencies vendored once per output directory — see below.

#### Cardinality mapping

EF's own `IsUnique`/`IsRequired` are runtime concepts, so both are derived statically: a foreign key is
*unique* when its columns are the primary key or carry a unique index, and *required* when they are
non-nullable. The principal side is always `||`, because a foreign key references exactly one row.

The interactive renderer draws the same mapping as crow's-foot notation, so both renderers say the
same thing about a relationship.

| Unique | Required | Mermaid | Meaning |
| --- | --- | --- | --- |
| yes | yes | `\|\|--\|\|` | one-to-one, mandatory |
| yes | no | `\|\|--o\|` | one-to-zero-or-one |
| no | yes | `\|\|--\|{` | one-to-one-or-many |
| no | no | `\|\|--o{` | one-to-zero-or-many |

Lines are always identifying (`--`). Delete behaviour and ownership are runtime EF concepts that static
analysis cannot recover, and guessing non-identifying would be misleading.

### Using the viewer

The interactive viewer is a tool for reading a schema, not just a picture of one.

**Search** (`/` or `Ctrl`/`Cmd`+`K` to focus) matches table *and* column names. Matching tables are
highlighted, everything else — including the relationships into it — recedes, and the row that caused
a match is marked on the card, so a hit on a column is visible without hunting for it. The counter
shows your position in the results; `Enter` travels to the next match and `Shift`+`Enter` to the
previous, bringing each into view. `Esc` clears.

**Relationships** are labelled with the relationship's name in both directions — `Blog ⇄ Posts` — so a
direction the model does not declare shows as `?` rather than being hidden. Where neither navigation
exists it falls back to the foreign key's constraint name, then its columns. Edges are drawn from the
model, so nothing can be connected by hand: the handles are anchors, not a drawing surface.

**Layout.** Drag a table and the box around its `DbContext` follows, growing or shrinking to wrap its
tables; the arrangement is remembered per model, so a reload keeps it and a regenerated diagram (whose
model has changed) falls back to a fresh layout.

**View** (the toolbar's `View` control) chooses how much of each table is drawn: `Columns` (the
default — every column with its type and key badges) or `Names` (the table name alone, for reading a
large schema at a glance).

**Settings** (the toolbar's *Settings* button) holds the theme (`Auto`, the default, following the
operating system; `Light`; `Dark`), the edge animation speed (or off), the background pattern, and a
motion override. A pinned theme is applied before the page paints, so there is no flash of the wrong
theme. The Mermaid preview shares the theme — pinning it in one applies it to both.

**Export** writes `PNG` or `SVG` of the *whole* diagram at its natural size, not a capture of the
window, so nothing scrolled out of view is lost. PNG is rendered at 2× for printing and zooming. Both
honour the current theme, so a diagram exported in dark mode is dark.

The Mermaid preview carries the same theme control and the same PNG/SVG export; beyond that it stays
deliberately minimal — render, pan, zoom — because Mermaid's job here is publishing. Note that
animated edges are browser-only and do not appear in any exported image.

### Recipes

```sh
just erd-vendor --embed               # rebuild the viewer's bundled JS after a dependency bump
just erd-check                        # drive the interactive viewer in a real browser
just erd-mermaid-check                # drive the Mermaid preview in a real browser
just erd-svg .aspirec4/erd/schema.gen.mmd   # render Mermaid to SVG via mermaid-cli 12 in a container
```

### How the viewer is served

The interactive and Mermaid renderers write static files rather than running a server, so a small
`nginx` sidecar serves the output directory. That is what gives the resource a real URL in the
dashboard — a `file://` link cannot be navigated to from the dashboard's own origin — and it makes
every generated export downloadable from the same place.

Set `UseViewerServer = false` where no container runtime is available. The diagram is still
generated, and the dashboard offers a command that opens it locally instead.

The viewer's JavaScript ships with the package, gzipped, and is written out beside the diagram on
first generation — there is no setup step. Only what the chosen renderer needs is written.

The viewer loads its dependencies from `./vendor/` rather than a CDN, so a generated diagram keeps
working offline. They are genuinely bundled with `bun build`; fetching ESM modules from a CDN at
vendoring time does **not** work, because the responses are re-export stubs pointing at absolute CDN
paths.

Existing files are never overwritten, so a locally rebuilt bundle survives regeneration. Run
`bunx playwright install chromium` once before `just erd-check`.

Animated edges are browser-only and will not appear in the SVG output.

### How the model is read

Every project resource is scanned for `DbContext` types. Each context is recovered from the best source
available, and the diagram records which one was used:

| Source | When | Fidelity |
| --- | --- | --- |
| **Dump** | The project — or one it references — references `AspireC4.EntityFrameworkCore.ModelDump` | `Full` — the real EF model, composite foreign keys included |
| **Descriptor** | The project references the analyzer-only `AspireC4.EntityFrameworkCore` package | `Full` — includes fluent `OnModelCreating` configuration |
| **Metadata** | Otherwise | `Approximate` — conventions and data annotations only |

The metadata path opens the built assembly with a reflection-only load context, so no code in it runs —
the same guarantee as the descriptor path, which reads the attribute straight out of assembly metadata.
This is what makes `WithEntityFrameworkDiagram()` work with no per-project configuration, and it is the
only way a shared or third-party assembly — one you cannot add a package to — contributes at all.

Because `OnModelCreating` is a method body, it is absent from metadata: a project without the analyzer
still appears, but fluent renames, default schemas and `Ignore` calls do not. `MetadataContextsFound` in
the loader diagnostics, and the `Approximate` fidelity on each context, record which happened, so a
partial model is never mistaken for a complete one.

#### Filtering

`WithExcludedContexts(...)` and `WithIncludedContexts(...)` accept short names (`IdentityDbContext`) or
fully-qualified type names (`Shared.Data.IdentityDbContext`), case-insensitively; an exclusion always
wins. This is how a context shared by several projects — an `IdentityDbContext` in a common library
nobody owns — is kept out without editing that library. The same filters are available per project
through the `WithEntityFrameworkModel(options => ...)` overload. Because the same context can reach the
diagram from more than one project, contexts are de-duplicated by fully-qualified type name; when both
an approximate and a full description exist, the full one wins.

#### Authoritative dump (opt-in)

The two recovered sources above never touch the service. Where you own the project and want the exact
model — composite foreign keys included — add the dump package to the project that owns the EF Core
**provider** and a design-time `DbContext` factory:

```bash
dotnet add package AspireC4.EntityFrameworkCore.ModelDump
```

That reference *is* the opt-in. The build then runs the dump tool, which builds each context's real
`IModel` and writes the diagram contract to `obj/aspirec4/ef/`. That directory is never copied to
`bin/` or `publish/` and is gitignored, so the dump has no deployment footprint. The AppHost reads it —
from that project and from every project it references — in preference to the descriptor and the
metadata reader, and falls back when it is missing or older than the built assembly.

Set `<AspireC4EfModelDumpConnectionString>` when the design-time factory needs one to build its
options — the model is never opened, so any syntactically valid value works.

Because the model is recovered **statically**, it is a close approximation rather than a guaranteed
match for the migrated schema.

**Recovered from metadata** (every project):

- `DbSet<T>` properties, and entity types reachable through navigation properties.
- `[Key]`, `[Required]`, `[MaxLength]`, `[StringLength]`, `[Table]`, `[Column]`, `[ForeignKey]`,
  `[NotMapped]`, `[Index(IsUnique = true)]`.
- EF naming conventions for primary keys (`Id` / `<Type>Id`) and foreign keys (`<Nav>Id` /
  `<PrincipalType>Id`).
- A column's **domain type** — a value object or an enum — is shown beside its real database type,
  controlled by `DomainTypeDisplay`: `AssetId (guid)` for a value object, `ActorType (int, enum)` for an
  enum. A **scalar** value object (a struct wrapping a single `Value`, such as a strongly-typed
  identifier) becomes a column, and its foreign keys carry the relationships between entities. A
  **complex** value object — one marked `[ValueObject(EFMapping = ComplexType)]` — is flattened into its
  owner's table. An **enum** is shown as itself and marked `enum`. The primitive a scalar value object
  converts to is known only to its value converter, so the model keeps both.

**Also recovered from a descriptor** (analyzer-referenced projects):

- Fluent configuration in `OnModelCreating`: `HasDefaultSchema`, `ToTable`, `HasKey`, `Ignore`,
  `HasIndex(...).IsUnique()`, and `Property(...)` with `HasColumnName`, `HasColumnType`,
  `HasMaxLength`, `IsRequired`, `HasComment`.
- Column comments, from `[Comment]` or `HasComment`.

**Derived in both cases:**

- Relationship cardinality and join tables, both **derived** rather than read from EF: a foreign key
  is unique when its columns are the key or carry a unique index, required when they are non-nullable,
  and a table is a join table when every key column is also a foreign key and there is more than one.
- Relationships themselves are derived from navigation properties and the `<Nav>Id` convention. A
  relationship configured only fluently — with no navigation, or a composite foreign key whose columns
  do not match that convention — is not drawn.

**Not captured.** These need either runtime model-building or analysis beyond what is sensible
statically, and are silently absent rather than guessed at: owned types, TPH/TPT/TPC inheritance
mapping, delete behaviour, sequences, check constraints, and value conversions. Relationships that
cross a `DbContext` boundary are deliberately not drawn — EF models a relationship inside one context,
so an edge across that boundary would be invented rather than observed.

Configuration applied indirectly — most notably `ApplyConfigurationsFromAssembly` — cannot be seen by
static analysis and is reported as `ASPIREC4010`. Store types are generic (`int`, `varchar`, `guid`, …)
rather than provider-specific, since the provider is not knowable at compile time; an explicit
`[Column(TypeName = "...")]` or `HasColumnType("...")` always wins.

### Local CLI instead of a container

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram()
       .WithLocalD2CLI();          // after WithEntityFrameworkDiagram
```

d2 ships as a Go binary. Unlike the LikeC4 sidecar, there is **no npm CLI fallback** — the
`@d2lang/d2` npm package is a WASM library with no executable. Install d2 (scoop, Homebrew,
`install.sh`, or `go install`) so it is on `PATH`, or pass an explicit path:
`WithLocalD2CLI("/opt/d2/bin/d2")`.

### Exporting the ER diagram as an image

On by default. `schema.svg` and `schema.png` are written to `./.aspirec4/images/` — the same folder the
architecture diagram exports to — whenever the diagram changes. The name drops the `.gen` marker, which
earns its place on a generated source file you hand-edit beside but not on an image.

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options.WithoutImageExport());

builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options.WithImageExport(ErdImageFormat.Svg));
```

**Export always renders through d2, whatever `Renderer` is set to.** The interactive renderer is a
browser application with no headless path, so an image of it could only be produced by driving a
browser; d2 is the only format that both renders images itself and keeps column-level edges, so routing
every renderer through it gives one toolchain and one appearance. Two things follow:

- Export needs either the `d2lang/d2` container or a local `d2` binary. `WithLocalD2CLI()` sets
  `D2Command` for you; set it directly to render locally while leaving the sidecar or viewer alone.
  Where neither is available, call `WithoutImageExport()`.
- `schema.gen.d2` is written alongside the diagram even for the other renderers, purely to be rendered.
  It is byte-identical to what the `D2` renderer writes.

Export runs in the background, so startup is never delayed, and only when it would change something — a
stamp beside the images means a restart over an unchanged model runs nothing at all, not even a
container. It is skipped entirely during `aspire publish`. See
[When export actually runs](#when-export-actually-runs). Failures are logged to the diagram resource
and leave the diagram itself untouched.

Prefer SVG for a large schema — a raster of the whole thing is unreadable at any sensible size. The
viewer also has **Save as SVG / PNG** buttons for exporting what is currently on screen.

#### Matching the viewer exactly

The d2 render is a *different diagram*, not a differently-styled one: d2 draws `sql_table` shapes with
its own theme and layout, while the viewer draws React Flow cards with context colours, key badges,
complex-type groups and crow's-foot markers. If the exported file must be the one the viewer's own
**Save as SVG / PNG** control produces, opt into the viewer render:

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options.WithViewerFidelityExport());
```

That loads the generated viewer in a headless browser and calls the very function the toolbar button
calls, so the file is identical. It is **off by default** because the viewer only runs in a browser:
turning it on pulls `mcr.microsoft.com/playwright` (around 3.7 GB) and makes an export take seconds
rather than milliseconds. Point `ViewerExportImage` at a mirror or an image you have already cached if
the default is inconvenient.

It applies only to the `Interactive` renderer — the one that has a viewer. Any other renderer exports
through d2 regardless, and says so in the log rather than failing.

### Overriding the output directory

Prefer the `configure` callback over configuration for `OutputDirectory`. The sidecar's bind mount is
fixed when the AppHost builder runs, so a value supplied through configuration changes where the file
is written but **not** what the container serves. The callback is applied in time for both.

## Aspire TypeScript AppHost support

AspireC4 supports both C# and TypeScript Aspire AppHosts. In a TypeScript AppHost, the Aspire integration exports the
same diagram configuration, resource metadata, grouping, and relationship features through camel-cased asynchronous
APIs. The C# registry source generator described later is not applied to TypeScript; TypeScript applications configure
tags, kinds, groups, and metadata through the generated fluent API.

The Aspire CLI generates the TypeScript API surface under `.aspire/modules/`. Import `createBuilder` from the generated
Aspire module, then add AspireC4 to the builder:

```typescript
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

await builder
    .addAspireC4({
        configure: async (options) => {
            options
                .withTitle("My distributed application")
                .withViewTitle("Architecture")
                .withViewDescription("Generated from the Aspire resource graph");
        },
    })
    .configureServer(async (resource) => {
        resource.withLikeC4Details({
            configure: async (options) => {
                options.withLabel("Architecture diagram");
            },
        });
    });

const api = await builder.addNodeApp("api", "../api", "index.ts");

await api.withLikeC4Details({
    configure: async (options) => {
        options
            .withLabel("API")
            .withTechnology("Node.js")
            .withTag("backend");
    },
});

const app = await builder.build();
await app.run();
```

Declare the integration and its Aspire dependencies in `aspire.config.json`:

```json
{
  "appHost": {
    "path": "apphost.mts",
    "language": "typescript/nodejs"
  },
  "sdk": {
    "version": "13.5.3"
  },
  "packages": {
    "Aspire.Hosting.JavaScript": "13.5.3",
    "AspireC4.Hosting": "13.5.3"
  }
}
```

Use the AspireC4 package version appropriate for the application. The repository sample points `AspireC4.Hosting` at
`../../src/src/AspireC4/AspireC4.csproj` so it exercises the local source instead of a published package.

### Run the TypeScript sample

The sample at [`samples/typescript-app-host`](samples/typescript-app-host) demonstrates:

- AspireC4 configuration from `apphost.mts`.
- Azure Redis and PostgreSQL resources running as local containers.
- Redis Commander and PgWeb dashboard resources.
- A TypeScript Node.js service with Redis and PostgreSQL references.
- LikeC4 labels, descriptions, links, icons, metadata, tags, groups, and relationships.
- Additional LikeC4 DSL and image folders from this repository's `assets` directory.

Prerequisites are Docker, the Aspire CLI, and Bun. From the
repository root:

```bash
cd samples/typescript-app-host
bun install
aspire restore
aspire start
```

`aspire restore` restores the integrations declared in `aspire.config.json` and regenerates `.aspire/modules/`. Once
`aspire start` completes, open the Aspire dashboard URL printed by the CLI and select the LikeC4 resource or its
architecture-diagram link. The sample also exposes the `node-app` `/health`, `/ping/redis`, and `/ping/postgres`
endpoints through Aspire-assigned URLs.

For an interactive foreground session, the sample's Bun script is equivalent to `aspire run`:

```bash
bun run dev
```

Stop a background session with:

```bash
aspire stop
```

### Generated TypeScript modules

Do not edit files under `.aspire/modules/`; Aspire owns and regenerates them. If the folder is missing or stale after a
pull, clean, or branch switch, run:

```bash
aspire restore
```

When adding another Aspire integration, use `aspire add <package>` so Aspire updates `aspire.config.json` and regenerates
the TypeScript API. Inspect `.aspire/modules/aspire.mts` to see the APIs currently available to `apphost.mts`.

## Compile-time registry validation

AspireC4 includes an incremental source generator built on `Purview.SourceGeneratorFramework`. It can validate constant
values passed to:

- `.WithTag()`
- `.WithKind()`
- `.WithLikeC4Group()`
- `.WithMetadata()`

The generator injects the registry attributes and enums automatically. Do not declare or reference a separate source
generator package.

### Registry class

Add one `[LikeC4Registry]` class to the AppHost assembly. Its accessibility and nesting do not matter. Values are
declared as `const string` fields in conventionally named nested classes:

```csharp
using Aspire.Hosting.AspireC4;

[LikeC4Registry]
internal static class ArchitectureRegistry
{
    public static class Tags
    {
        public const string External = "external";
        public const string LocalDevelopment = "local-dev";
    }

    public static class ElementKinds
    {
        public const string Service = "service";
    }

    public static class RelationshipKinds
    {
        public const string Async = "async";
    }

    public static class Groups
    {
        public const string Platform = "Platform";
    }

    public static class MetadataKeys
    {
        public const string AzureSku = "Azure_SKU";
    }
}
```

Supported nested-class names are:

| Registry type | Accepted class names |
| --- | --- |
| Tag | `Tag`, `Tags` |
| Element kind | `ElementKind`, `ElementKinds`, `Element`, `Elements` |
| Relationship kind | `RelationshipKind`, `RelationshipKinds`, `Relationship`, `Relationships` |
| Group | `Group`, `Groups` |
| Metadata key | `MetadataKey`, `MetadataKeys` |

Use the constants at call sites to make refactoring safe:

```csharp
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details =>
        details
            .WithTag(ArchitectureRegistry.Tags.External)
            .WithKind(ArchitectureRegistry.ElementKinds.Service)
            .WithMetadata(ArchitectureRegistry.MetadataKeys.AzureSku, "Standard_LRS")
    )
    .WithLikeC4Group(ArchitectureRegistry.Groups.Platform);
```

### Individual registry fields

For a flat registry, annotate each constant with `[KnownType]`:

```csharp
[LikeC4Registry]
internal static class ArchitectureRegistry
{
    [KnownType(LikeC4RegistryType.Tag)]
    public const string External = "external";

    [KnownType(LikeC4RegistryType.Group, Strict = LikeC4Severity.Warning)]
    public const string Platform = "Platform";
}
```

Do not declare the same registry type using both a named nested class and `[KnownType]` fields. Doing so produces
`ASPIREC4005`.

### Validation severity

Without an explicit strict setting, a registry class enables suggestion-level validation. Severity can be configured at
three levels, from broadest to most specific:

1. The `AspireC4Strict` MSBuild property.
2. `[LikeC4Registry(Strict = ...)]` for the registry.
3. `[Severity(...)]` on a named nested class, or `KnownType.Strict` on an individual field.

```csharp
[LikeC4Registry(Strict = LikeC4Severity.Warning)]
internal static class ArchitectureRegistry
{
    [Severity(LikeC4Severity.Error)]
    public static class Tags
    {
        public const string External = "external";
    }

    [KnownType(LikeC4RegistryType.Group, Strict = LikeC4Severity.Off)]
    public const string UnvalidatedGroup = "Temporary";
}
```

`LikeC4Severity` supports `Inherit`, `Off`, `Suggestion`, `Warning`, and `Error`.

The project-wide setting can be placed in the AppHost project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <AspireC4Strict>warning</AspireC4Strict>
</PropertyGroup>
```

Accepted `AspireC4Strict` values are:

| Value | Behavior |
| --- | --- |
| `off` or an unset/unknown value | Disables DSL-file strict validation |
| `suggestion` | Reports undeclared DSL values as suggestions |
| `warning` | Reports undeclared DSL values as warnings |
| `error`, `true`, `yes`, or `all` | Reports undeclared DSL values as errors |
| `allincludingmetadata` | Error-level validation including metadata keys |

Metadata-key comparison is case-insensitive and normalizes punctuation and whitespace to underscores. For example,
`Azure SKU`, `azure sku`, and `Azure_SKU` identify the same key.

To disable all AspireC4 source-generator diagnostics while retaining the injected registry types:

```xml
<PropertyGroup>
  <DisableAspireC4SourceGenerator>true</DisableAspireC4SourceGenerator>
</PropertyGroup>
```

### Validate LikeC4 files

Add `.c4` or `.likec4` specification files as compiler additional files, then set `AspireC4Strict`:

```xml
<ItemGroup>
  <AdditionalFiles Include="likec4/**/*.c4" />
  <AdditionalFiles Include="likec4/**/*.likec4" />
</ItemGroup>

<PropertyGroup>
  <AspireC4Strict>warning</AspireC4Strict>
</PropertyGroup>
```

Tags, element kinds, and relationship kinds in `specification` blocks are merged with registry-class definitions.

### Diagnostics

| ID | Meaning |
| --- | --- |
| `ASPIREC4001` | A tag passed to `.WithTag()` is undeclared |
| `ASPIREC4002` | An element or relationship kind passed to `.WithKind()` is undeclared |
| `ASPIREC4003` | More than one class in the assembly has `[LikeC4Registry]` |
| `ASPIREC4004` | A group passed to `.WithLikeC4Group()` is undeclared |
| `ASPIREC4005` | A registry type uses both a nested class and `[KnownType]` fields |
| `ASPIREC4006` | A metadata key passed to `.WithMetadata()` is undeclared |
| `ASPIREC4007` | A nested class in the registry uses a name that is not a recognized registry type |

## Breaking changes in the Source Generator Framework migration

The source generator now uses the current `Purview.SourceGeneratorFramework` incremental APIs. Existing applications
should review the following changes when upgrading:

- **Aspire AppHost dependency is explicit.** Aspire 13.5 AppHosts must reference `Aspire.Hosting.AppHost`; relying on the
  AppHost SDK alone produces `ASPIRE002`.
- **Only one registry class is supported per assembly.** Merge multiple `[LikeC4Registry]` classes into one class.
- **Registry declaration styles cannot be mixed per type.** For example, choose either a `Tags` nested class or
  `[KnownType(LikeC4RegistryType.Tag)]` fields. Mixing both now produces `ASPIREC4005`.
- **Strict settings are severity-based.** Replace older boolean-only assumptions with `suggestion`, `warning`, `error`,
  `all`, or `allincludingmetadata`. `true` remains accepted as an alias for error-level validation.
- **Metadata validation is opt-in at the global level.** Use `allincludingmetadata`, or apply an explicit metadata
  severity through `[Severity]`/`[KnownType]`. Plain `all` does not validate metadata keys.
- **Generated source files are split by type.** The generator now emits `LikeC4RegistryAttribute.g.cs`,
  `KnownTypeAttribute.g.cs`, `SeverityAttribute.g.cs`, `LikeC4RegistryType.g.cs`, and `LikeC4Severity.g.cs` instead of a
  combined `LikeC4RegistryAttributes.g.cs`. This affects generator snapshot tests and tooling that inspected hint names;
  normal application source code is unaffected.
- **Do not define generated registry types manually.** Remove compatibility copies of `LikeC4RegistryAttribute`,
  `KnownTypeAttribute`, `SeverityAttribute`, `LikeC4RegistryType`, or `LikeC4Severity` to avoid duplicate-type errors.
- **Generator packaging is automatic.** Consumers should reference only `AspireC4.Hosting`; remove direct references to
  `AspireC4.SourceGenerators` or `Purview.SourceGeneratorFramework` that were added solely to make the AspireC4 generator
  run.
- **TypeScript APIs are generated by Aspire.** TypeScript AppHosts import from `.aspire/modules/aspire.mjs`; generated
  files must not be copied between projects or edited manually. Run `aspire restore` after upgrading AspireC4 so the
  exported API matches the installed integration version.

## Documentation

- [Homepage](https://purview.dev/projects/aspirec4/)
- [Documentation](https://purview.dev/docs/aspirec4/)


