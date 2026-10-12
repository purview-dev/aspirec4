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

## Upgrading from before 13.6

13.6 changed two things, both breaking:

- Generated output moved under one root: `./.aspirec4/likec4/` (was `./likec4/gen/`) and
  `./.aspirec4/erd/` (was `./erd/gen/`). Replace your `.gitignore` entries with `**/.aspirec4/`.
- When more than one diagram is registered, they nest under a shared dashboard parent and the
  architecture diagram's resource is renamed from `aspirec4` to `aspirec4-likec4`. With only
  `AddAspireC4()`, nothing changes.

Both revert together with one switch, for a zero-change upgrade:

```csharp
builder.AddAspireC4(configure: options => options.WithLegacyLayout());
```

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

Two packages are involved, because the model is read from source at compile time:

```bash
# AppHost project
dotnet add package AspireC4.Hosting.EntityFrameworkCore

# Each service project that owns a DbContext
dotnet add package AspireC4.EntityFrameworkCore
```

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options
           .LayoutEngine = D2LayoutEngine.Elk);

builder.AddProject<Projects.Api>("api")
       .WithEntityFrameworkModel();   // contribute this project's DbContexts

builder.Build().Run();
```

This writes `./.aspirec4/erd/schema.gen.d2`, starts a `d2lang/d2` sidecar running `d2 --watch`, and
adds an **ER Diagram** link to each contributing project in the dashboard and on its element in the
architecture diagram.

### Dashboard layout

Both diagrams write beneath a single `./.aspirec4/` root in the AppHost directory — one directory to
ignore in source control regardless of how many are enabled.

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
| `OutputDirectory` | `"./.aspirec4/erd/"` | Directory the generated files are written to |
| `FileName` | `"schema.gen"` | Generated file name without extension |
| `UseSubgraphs` | `true` | Mermaid only — group entities into one `subgraph` per `DbContext` (needs Mermaid 11.17+) |
| `LayoutEngine` | `Elk` | D2 only. `Dagre`, `Elk` or `Tala`. **dagre cannot target `sql_table` rows** |
| `ShowNullability` | `true` | Render nullable columns with a trailing `?` |
| `GroupBySchema` | `true` | Group tables into d2 containers by database schema |
| `GroupByContext` | `true` | Group by `DbContext` when more than one is discovered |
| `ContainerImageTag` | `null` (`v0.9.0`) | Pin the [`d2lang/d2`](https://hub.docker.com/r/d2lang/d2/) image tag |
| `UseViewerServer` | `true` | Serve the viewer so the dashboard can link to it; set `false` where there is no container runtime |
| `ViewerServerImageTag` | `null` (`1.29-alpine`) | Pin the `nginx` image tag used to serve the viewer |
| `HideFromDashboard` | `false` | Hide the d2 sidecar from the Aspire dashboard |
| `DashboardLinkDisplayName` | `"ER Diagram"` | Name used for the diagram link |

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

**Theme** is `Auto`, `Light` or `Dark`. `Auto` follows the operating system and is the default; a
pinned choice is remembered across reloads and is applied before the page paints, so there is no flash
of the wrong theme. The Mermaid preview shares the setting — pinning a theme in one applies it to both.

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

The `AspireC4.EntityFrameworkCore` package is an **analyzer-only** package. At compile time it reads
your `DbContext` types and emits the discovered model into the assembly as an attribute; the AppHost
reads that attribute straight out of the built assembly's metadata, without loading or executing it.

Because the model is recovered **statically**, it is a close approximation rather than a guaranteed
match for the migrated schema. Understood today:

- `DbSet<T>` properties, and entity types reachable through navigation properties.
- `[Key]`, `[Required]`, `[MaxLength]`, `[StringLength]`, `[Table]`, `[Column]`, `[ForeignKey]`,
  `[NotMapped]`, `[Index(IsUnique = true)]`.
- EF naming conventions for primary keys (`Id` / `<Type>Id`) and foreign keys (`<Nav>Id` /
  `<PrincipalType>Id`).
- Fluent configuration in `OnModelCreating`: `HasDefaultSchema`, `ToTable`, `HasKey`, `Ignore`,
  `HasIndex(...).IsUnique()`, and `Property(...)` with `HasColumnName`, `HasColumnType`,
  `HasMaxLength`, `IsRequired`, `HasComment`.
- Column comments, from `[Comment]` or `HasComment`.
- Relationship cardinality and join tables, both **derived** rather than read from EF: a foreign key
  is unique when its columns are the key or carry a unique index, required when they are non-nullable,
  and a table is a join table when every key column is also a foreign key and there is more than one.

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
