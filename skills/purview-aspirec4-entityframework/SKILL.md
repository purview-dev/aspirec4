---
name: purview-aspirec4-entityframework
description: >-
  Use when generating an Entity Framework Core ER diagram from an Aspire AppHost with AspireC4 -
  installing AspireC4.Hosting.EntityFrameworkCore, calling WithEntityFrameworkDiagram, how the model is
  discovered (assembly metadata, an analyzer descriptor, or the opt-in build-time dump), filtering and
  excluding DbContexts, domain types (Purview [Scalar] and [ValueObject] value objects, owned and JSON
  types, enums and C# unions) and the DomainTypeDisplay option, relationships through value-object keys,
  the renderers, exporting the diagram as SVG and PNG on startup, and the dashboard integration. Also use
  when a value object shows as a bare primitive, or a collection or complex type appears as a table.
metadata:
  product: AspireC4
  repository: purview-dev/aspirec4
---

# Entity Framework Core ER diagrams

`AspireC4.Hosting.EntityFrameworkCore` adds a second diagram beside the architecture one: a live ER
diagram built from your Entity Framework Core `DbContext` types, linked from each contributing project in
the dashboard and on its element in the architecture diagram.

It is **additive and optional**. `AddAspireC4()` is always the base, and `WithEntityFrameworkDiagram()` is
an extension on the `IResourceBuilder<AspireC4Resource>` it returns, so the architecture diagram is always
present — run one diagram or both, but never the ER diagram alone.

## Requirements and target frameworks

| Package | Target frameworks | Shipped as | Needs |
| --- | --- | --- | --- |
| `AspireC4.Hosting.EntityFrameworkCore` | `net10.0`, `net11.0` | `lib/net10.0/` and `lib/net11.0/` libraries | an Aspire 13.6.5+ AppHost built with the .NET 11 SDK |
| `AspireC4.EntityFrameworkCore` | `netstandard2.0` | analyzer-only under `analyzers/dotnet/cs/`; adds no runtime assembly | the **.NET 11 SDK** in the project it is added to |
| `AspireC4.EntityFrameworkCore.ModelDump` | `net10.0` | build-time tool under `tools/`, invoked by MSBuild | a .NET 10 or later runtime on the build machine; it rolls forward to the latest major, so the .NET 11 SDK alone is enough |

The libraries ship the lowest common supported pair: `net10.0` is the LTS and the Aspire AppHost
framework, and `net11.0` serves a host on the current release. .NET 8 and .NET 9 are not supported.

Both optional packages fail **quietly**, and the diagram just loses fidelity, so check these first when
a context shows `Approximate` unexpectedly:

- **Analyzer on an older compiler.** It runs inside the compiler and is built against Roslyn 5.9, so a
  .NET 10 SDK reports `CS9057` and skips it. The target framework is irrelevant. No descriptor is
  emitted, and the context falls back to the metadata read.
- **Dump tool that could not run.** It runs as `dotnet <tool>.dll` with `RollForward=LatestMajor`, so
  any .NET 10+ runtime works, and a `net11.0` project is loaded by the newest installed runtime. When it
  still fails (no design-time factory, or a factory that throws), the target is `ContinueOnError`: the
  build succeeds, no dump is written, and the ER diagram log names the missing dump.

## Packages

| Package | Add to | Why |
| --- | --- | --- |
| `AspireC4.Hosting.EntityFrameworkCore` | the AppHost | the diagram itself. Required |
| `AspireC4.EntityFrameworkCore` | a project **you own** that has a `DbContext` | analyzer-only; captures fluent `OnModelCreating` configuration |
| `AspireC4.EntityFrameworkCore.ModelDump` | the project that owns the EF Core **provider** and a design-time `DbContext` factory | the authoritative model, composite foreign keys included |

```bash
dotnet add package AspireC4.Hosting.EntityFrameworkCore
```

The AppHost package is all that is required — `WithEntityFrameworkDiagram()` finds every associated
`DbContext` with no per-project configuration.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4().WithEntityFrameworkDiagram();

builder.AddProject<Projects.Api>("api");

builder.Build().Run();
```

## How the model is read

Every project resource is scanned, and each context is recovered from the best source available:

| Source | When | Fidelity |
| --- | --- | --- |
| **Dump** | the project, or one it references, references `AspireC4.EntityFrameworkCore.ModelDump` | the real EF model |
| **Descriptor** | the project references `AspireC4.EntityFrameworkCore` | includes fluent configuration |
| **Metadata** | otherwise | conventions and data annotations only |

The metadata reader opens the assembly with a reflection-only load context, so no code runs. It cannot
see `OnModelCreating` — a method body is absent from metadata — so a project without a package still
appears, but fluent renames, default schemas and `Ignore` calls do not. A shared or third-party
assembly you cannot instrument contributes at this fidelity.

### The build-time dump

Reference the dump package from the project that owns the EF Core provider; that reference **is** the
opt-in. The build then runs the tool, which builds each context's real `IModel` through its
`IDesignTimeDbContextFactory` and writes the diagram contract to `obj/aspirec4/ef/`. That directory is
never copied to `bin/` or `publish/`, so there is no deployment footprint. The AppHost reads it from
that project and every project it references, and falls back when it is missing or stale.

Set `<AspireC4EfModelDumpConnectionString>` when the design-time factory needs one to build its
options — the model is never opened, so any syntactically valid value works.

## Filtering

`WithExcludedContexts(...)` and `WithIncludedContexts(...)` take short names (`IdentityDbContext`) or
fully-qualified type names (`Shared.Data.IdentityDbContext`), case-insensitively; an exclusion always
wins. This is how a context shared by several projects is kept out without editing the library that
owns it.

```csharp
builder.AddAspireC4().WithEntityFrameworkDiagram(configure: options => options
    .WithExcludedContexts("IdentityDbContext")
);
```

The same filters are available per project through the `WithEntityFrameworkModel(options => ...)`
overload, so a context can be suppressed for one contributor while another still shows it. Contexts
are de-duplicated by fully-qualified type name across projects.

By default every project resource is scanned. `WithProjectOptInOnly()` restricts the diagram to
projects marked with `WithEntityFrameworkModel()`.

## Domain types

A column's **domain type** is the type your application declares, as opposed to the type the database
stores. Four kinds are recognised, and each is distinct:

| Kind | What it is | Example |
| --- | --- | --- |
| `ValueObject` | a scalar value object: a struct or class wrapping one value | `AssetId (guid)` |
| `ComplexValueObject` | a complex value object, flattened into its owner's table — or one `json` column | `Address` |
| `Enum` | an enum, stored as its underlying integral type (or `varchar` when converted to a string) | `ActorType (tinyint, enum)` |
| `Union` | a C# union; EF stores it through a value converter | `Reading (varchar, union)` |

How each is recognised — the same rules in all three sources:

- **Scalar value object:** Purview's `[Scalar<T>]`, `[Scalar(typeof(T))]` or `[Scalar("Amount")]` (the
  wrapped type comes from the attribute, or from the named property), `IScalarValueObject<TSelf, TValue>`,
  or a non-generic struct with a public `Value` not typed `object`. `Nullable<T>` keeps the type. The
  attributes are what the analyzer needs: Purview's generator writes `Value` into a file no other
  generator can see.
- **Complex value object, flattened:** Purview's `[ValueObject]` — its `EFMapping` **defaults to
  `ComplexType`**, so a bare attribute is enough — EF's `[ComplexType]`, or an `[Owned]` reference. Columns
  are `{Property}_{Member}`, each recording the property it came from, so the viewer folds them under it
  even when a member is itself a value object or an enum.
- **One `json` column:** `[ValueObject(EFMapping = Json)]`, a complex collection, or a collection of
  primitives, value objects or enums (`List<AssetId>` shows as `AssetId[]`). None of these is a table.
- **Union:** declared with `union`, or a struct marked `[Union]` / implementing `IUnion`. Checked before
  the value-object rules, because a union's `Value` accessor has their shape.
- `OwnsMany`, or an `[Owned]` type in a collection, is a table of its own with a foreign key to its owner.

Fluent configuration — `ComplexProperty`, `OwnsOne`, `OwnsMany`, `ToJson`, `HasConversion`,
`HasOne(...).HasForeignKey(...)` — is a method call the analyzer and the metadata reader do not
interpret; only the **model dump** sees it. Prefer the attribute when the static sources matter.

### Relationships through value objects

A foreign key typed with a value object resolves like any other, from **either end**: a reference
navigation on the dependent (`[ForeignKey]`, `{Navigation}Id`, `{Principal}Id`), or only a collection on
the principal, where EF's naming finds the key — and when the dependent declares none, EF's **shadow
foreign key** column is shown. A single-column foreign key into a composite key pairs with the key
column of its own type. Collections on both sides are a many-to-many through EF's own join table.

When a context shows `Approximate`, or a value object shows as a bare primitive, check:

1. Is the value object declared with a Purview attribute, or a `Value` member in source? A
   hand-rolled wrapper with neither is invisible to the analyzer.
2. Is the complex type configured only fluently? Mark it, or add the model dump.
The database type is always kept; `DomainTypeDisplay` chooses which is the headline:

| `DomainTypeDisplay` | Renders |
| --- | --- |
| `DomainTypeFirst` *(default)* | `AssetId (guid)`, `ActorType (int, enum)` |
| `DatabaseFirst` | `guid (AssetId)`, `int (ActorType, enum)` |
| `DomainTypeOnly` | `AssetId`, `ActorType (enum)` — the database type moves to the annotation slot (a Mermaid comment, a DBML note, the viewer's tooltip); **d2 has no such slot**, so its tables show the domain type alone |
| `DatabaseOnly` | `guid`, `int (enum)` |

```csharp
options.WithDomainTypeDisplay(DomainTypeDisplay.DatabaseFirst);
```

Enum *values* are not listed — only the type, so a reader can see that `ActorType` is an enum without
the diagram enumerating every member.

## Options

Options bind from the **`AspireC4:EntityFramework`** section, applied on top of the callback.

| Property | Default | Description |
| --- | --- | --- |
| `Renderer` | `Interactive` | `Interactive`, `Mermaid`, `Dbml` or `D2` |
| `Title` | `null` | title shown in the generated diagram |
| `DiscoverAllProjects` | `true` | scan every project resource; `WithProjectOptInOnly()` narrows it |
| `IncludedContexts` / `ExcludedContexts` | empty | context filters |
| `OutputDirectory` | `"./.aspirec4/erd/"` | where generated files are written; follows the architecture diagram's `WithLegacyLayout()` (`./erd/gen/`) when left at its default |
| `FileName` | `"schema.gen"` | file name without extension |
| `UseSubgraphs` | `true` | Mermaid only — one `subgraph` per `DbContext` (needs Mermaid 11.17+) |
| `LayoutEngine` | `Elk` | D2 only. `Dagre`, `Elk` or `Tala`; **dagre cannot target `sql_table` rows** |
| `ShowNullability` | `true` | render nullable columns with a trailing `?` |
| `GroupBySchema` | `true` | group tables into containers by database schema |
| `GroupByContext` | `true` | group by `DbContext` when more than one is discovered |
| `DomainTypeDisplay` | `DomainTypeFirst` | how a column shows its domain type (a value object, an enum or a union) beside the database type |
| `GenerateGitIgnoreFile` | `true` | write a `.gitignore` naming the generated files; `WithoutGitIgnoreFileGeneration()` turns it off |
| `ContainerImageTag` | `null` | pin the `d2lang/d2` image tag |
| `UseViewerServer` | `true` | serve the viewer so the dashboard can link to it; `false` where there is no container runtime |
| `ViewerServerImageTag` | `null` | pin the viewer `nginx` image tag |
| `HideFromDashboard` | `false` | hide the sidecar and surface it as a link |
| `DashboardLinkDisplayName` | `"ER Diagram"` | the link name when hidden |
| `ExportImages` | `true` | write the diagram out as an image when it changes |
| `ImageFormats` | `{Svg, Png}` | `Svg` and/or `Png` |
| `ImageExportDirectory` | `null` (`./.aspirec4/images/`) | where images are written; relative paths resolve against the **AppHost directory**. Shared with the architecture diagram |
| `D2Command` | `null` | a local `d2` binary to export with; `null` runs the container |
| `ContainerRuntimeCommand` | `"docker"` | the runtime for the one-shot export container; `podman` where that is what Aspire uses |
| `ExportTimeoutSeconds` | `120` | cap on one d2 invocation |
| `UseViewerFidelityExport` | `false` | render through the viewer in a headless browser so images match its own export. Pulls a ~3.7 GB image; `Interactive` only |
| `ViewerExportImage` | `null` | override the browser image, including its tag |
| `ViewerExportTimeoutSeconds` | `600` | cap on one viewer-fidelity export |

The output path follows the architecture diagram: `WithLegacyLayout()` on `AddAspireC4` restores
`./erd/gen/` as well, because there is no separate ER switch. An `OutputDirectory` set on
`WithEntityFrameworkDiagram` still wins over either default. See `purview-aspirec4-configuration` for
legacy mode.

## Renderers

| Renderer | Output | Notes |
| --- | --- | --- |
| `Interactive` *(default)* | `.json` + viewer | the only renderer that loses nothing — column-level edges, grouping, key badges |
| `Dbml` | `.dbml` | interchange with database tooling; renders nowhere on its own |
| `Mermaid` | `.mmd` + preview | the only format GitHub and most wikis render inline; edges join entities, not columns |
| `D2` | `.d2` + live sidecar | column-level edges and nested containers; needs the container or a local `d2` binary |

The d2 sidecar runs as a container by default. Without a container runtime:

```csharp
builder.AddAspireC4().WithEntityFrameworkDiagram().WithLocalD2CLI();
```

d2 ships as a Go binary, so it must be on `PATH`; there is no npm fallback. Call `WithLocalD2CLI()`
**after** `WithEntityFrameworkDiagram()`.

## Image export

On by default, so a schema can go into a pull request or a wiki without opening the viewer. Images are
written to **`./.aspirec4/images/`** — the same folder the architecture diagram exports to, so there is
one place to look:

```text
apphost/.aspirec4/images/
  schema.svg                    <- this diagram
  schema.png
  architecture-index.png        <- architecture diagram
```

That folder is also the place to take a screenshot from: it is written by the AppHost itself, so nothing
has to be captured by hand.

The name drops the `.gen` marker: `schema.gen` produces `schema.svg`, not `schema.gen.svg`. The marker
earns its place on a generated source file sitting in a directory you also hand-edit, which an image in
the shared folder never is. The dashboard row carries an **Images** property with the resolved path and
an **Open Diagram Images** command that opens the folder.

```csharp
builder.AddAspireC4().WithEntityFrameworkDiagram(configure: options => options.WithoutImageExport());
builder.AddAspireC4().WithEntityFrameworkDiagram(configure: options =>
    options.WithImageExport(ErdImageFormat.Svg)
);
```

`WithImageExport()` with no format turns export off, the same as `WithoutImageExport()`.

**Export always renders through d2, whatever `Renderer` is set to.** The interactive renderer is a
browser application with no headless path, so an image of it could only be produced by driving a
browser; d2 is the only format that both renders images itself and keeps column-level edges. Two
things follow:

- Export needs either the `d2lang/d2` container or a local `d2` binary (`D2Command`, which
  `WithLocalD2CLI()` sets for you). Where neither is available, call `WithoutImageExport()`.
- `{FileName}.d2` is written alongside the diagram even for the other renderers, purely to be
  rendered. It is identical to what the `D2` renderer writes.

Export runs in the background, so startup is never delayed, and nothing is rendered — **no container is
started** — unless the output would differ:

| Situation | Exports? |
| --- | --- |
| Identical model regenerated | no |
| AppHost restarted over an unchanged model | no — a stamp file beside the images records what they were rendered from |
| Model changed, or a format added | yes |
| Images deleted | yes — the stamp is only trusted when the files it describes still exist |
| `aspire publish` | **never** |

Publish is skipped outright: an export is fire-and-forget, so it would be killed part-way through when
publish exits, and publish is expected to be hermetic rather than reaching for a container runtime a
build agent may not have.

Failures are best-effort: they are logged to the diagram resource and the diagram itself is unaffected.

Prefer SVG for a large schema — a raster of the whole thing is unreadable at any sensible size. The
viewer also has **Save as SVG / PNG** buttons for exporting what is currently on screen.

### Matching the viewer exactly

The d2 render is a **different diagram**, not a differently-styled one: d2 draws `sql_table` shapes with
its own theme and layout, the viewer draws React Flow cards with context colours, key badges,
complex-type groups and crow's-foot markers. To get the file the viewer's own export produces:

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options.WithViewerFidelityExport());
```

This loads the generated viewer in a headless browser and calls the same function the toolbar button
calls, so the output is identical. **Off by default**: it pulls `mcr.microsoft.com/playwright` (around
3.7 GB) and an export takes seconds rather than milliseconds. `ViewerExportImage` overrides the image
for a mirror or a cached one.

Applies only to `Interactive`, the renderer that has a viewer. Any other renderer exports through d2
regardless and logs why.

## Using the viewer

The interactive viewer is for reading a schema, not just looking at one.

- **Search** — `/` or `Ctrl`/`Cmd`+`K` focuses the field; it matches table *and* column names. `Enter`
  walks the matches, `Shift`+`Enter` walks back, `Esc` clears.
- **Relationships** are labelled with the relationship's name in both directions (`Blog ⇄ Posts`); a
  direction the model does not declare shows as `?`. With no navigation at all it falls back to the
  foreign key's constraint name, then its columns. Edges come from the model, so nothing can be
  connected by hand.
- **Layout** — drag a table and the box around its `DbContext` follows it, growing or shrinking to wrap
  its tables. The arrangement is remembered per model, so a reload keeps it; a regenerated diagram
  whose model has changed falls back to a fresh layout.
- **View** — the toolbar's `View` control chooses `Columns` (the default) or `Names`, which collapses
  each table to its header for reading a large schema at a glance.
- **Settings** — the toolbar's *Settings* button holds the theme (`Auto`/`Light`/`Dark`), the edge
  animation speed (or off), the background pattern and a motion override.
- **Export** — `PNG` or `SVG` of the whole diagram at its natural size, honouring the current theme.

## Related skills

`purview-aspirec4-quickstart` for installing AspireC4 and running the architecture diagram,
`purview-aspirec4-configuration` for the LikeC4 options reference, and `purview-aspirec4-resources` for
per-resource labels, relationships and grouping.

