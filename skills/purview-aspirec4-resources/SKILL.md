---
name: purview-aspirec4-resources
description: >-
  Use when customising how an Aspire resource appears in an AspireC4 LikeC4 diagram - WithLikeC4Details
  for labels, technology, descriptions, summaries, icons, kinds, tags, links and metadata;
  WithLikeC4Reference for typed relationships; WithLikeC4Group for grouping; WithLikeC4Element for
  mapping onto a hand-authored element; styling by tag or kind; and ExcludeFromLikeC4, type-based or
  name-based exclusion for keeping a resource out.
metadata:
  product: AspireC4
  repository: purview-dev/aspirec4
---

# Customising resources in the diagram

AspireC4 annotates resources **in the Aspire app model**, so each resource and relationship is
customised in the AppHost, before the `.c4` file is generated.

## `WithLikeC4Details` — the node

```csharp
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details =>
        details
            .WithLabel("Public API")
            .WithTechnology(".NET")
            .WithDescription("The public HTTP API surface.")
            .WithSummary("Handles client requests")
            .WithIcon("tech:dotnet")
            .WithKind("service")
            .WithTag("backend")
            .WithLink("https://example.com/docs", "API docs")
            .WithMetadata("Owner", "Platform Team")
    );
```

| Method | Purpose |
| --- | --- |
| `WithLabel(string)` | the display label |
| `WithTechnology(string?)` | shown beneath the label — `.NET`, `Redis`. When unset it is detected from the resource's framework, Aspire type or container image (`Astro`, `Azure Storage (Azurite)`, `SQL Server`) |
| `WithDescription(string?)` | the detail panel; Markdown |
| `WithSummary(string?)` | one line, for tooltips and the map view |
| `WithIcon(string?)` | `tech:dotnet`, `azure:storage-accounts`; `null` reverts to automatic inference |
| `WithAutoIcon(bool?)` | per-element override; `null` inherits the project setting |
| `WithKind(string?)` | element kind override; must be a valid LikeC4 identifier |
| `WithTag(string)` | adds a tag — a leading `#` is accepted and stripped |
| `WithLink(string \| Uri, string? title)` | absolute, or relative to the `.c4` file |
| `WithMetadata(string key, string value)` | a key/value pair |

**One call per resource.** `WithLikeC4Details` *replaces* any details annotation the resource
already has, so a second call (from a shared helper, a ResourceKit kit, or the AppHost) silently
drops the first one's tags, links and prose. To add to details that are already set, amend them:
`resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault()?.WithTag("pii")`.

## `WithLikeC4Element` — an element a hand-authored project already declares

```csharp
builder.AddProject<Projects.Api>("api").WithLikeC4Element("cloud.backend.api");
```

The model emits `extend cloud.backend.api { ... }` with the run-state tag, links, metadata and tags,
and nests the resource's Aspire children inside it. The label, technology, summary, description and
icon are **not** applied: LikeC4's `extend` body does not accept them, so the hand-authored project
stays their only source. Pair it with `WithLikeC4ProjectFolder` (see `purview-aspirec4-configuration`).

## `WithLikeC4Reference` — the relationship

Two methods, and the difference matters:

```csharp
// Any resource builder - describes the relationship only.
builder.AddNodeApp("app", ...)
    .WithLikeC4Reference(redis, opts => opts
        .WithLabel("Caches sessions")
        .WithTechnology("Redis Protocol")
        .WithKind("RESP"));

// A resource with a connection string - ALSO calls Aspire's WithReference.
builder.AddProject<Projects.Api>("api")
    .WithLikeC4ConnectionReference(db, opts => opts.WithLabel("Persists data"));
```

`WithLikeC4ConnectionReference` wires up Aspire's `WithReference` as well, so the connection string
reaches the consumer; it has the same name in TypeScript, Go and Python. Pass `skipAspireReference: true`
to only describe a reference that already exists.

**Do not rely on `WithLikeC4Reference(db, ...)` for the connection.** Its connection-string overload is
only chosen by C# when a connection argument (`connectionName`, `optional`, `skipAspireReference`) is
passed; otherwise the call binds to the relationship-only overload and `WithReference` is never called.

| Method | Purpose |
| --- | --- |
| `WithLabel(string)` | the arrow label |
| `WithTechnology(string?)` | protocol — `HTTP/2`, `gRPC`, `AMQP` |
| `WithDescription(string?)` | longer description |
| `WithKind(string?)` | a typed kind (`async`, `sync`), declared in the `specification` block and emitted with the configured `RelationshipKindSyntax` |
| `WithTag(string)` | a tag on the relationship |
| `WithLink(string \| Uri, string? title)` | a hyperlink |
| `WithMetadata(string key, string value)` | a key/value pair |
| `WithNavigateTo(string viewId)` | the dynamic view to open when the relationship is clicked |

Attach **one annotation per target** to the same source to customise each relationship
independently.

## `WithLikeC4Group` — grouping

```csharp
builder.AddRedis("cache").WithLikeC4Group("Platform");
```

Resources sharing a group are emitted inside a `group 'label' { include … }` block in the generated
view. A resource is in at most one group (the call replaces an earlier one), and groups do not nest.

Aspire's own `WithParentRelationship` is **not** a group. It is recorded as a `"Parent"` relationship,
and drawn as an arrow labelled `'Parent'`. Only `IResourceWithParent` children, such as a database
under its server, are drawn nested inside their parent. A Purview.Aspire.ResourceKit AppHost relies on
`"Parent"` relationships for its `[ResourceGroup]` nodes. The `purview-aspirec4-enrichment` skill shows
how to turn those into LikeC4 groups.

## Styling by tag or kind

Every tag in use is declared in the generated `specification` block, so a hand-written file must not
redeclare it. The generated view cannot be edited, so style through one of these:

- **Kind**, for every view: `options.WithElementKindSpec(new LikeC4ElementKindSpec("service").WithStyle(...))`,
  and `WithKind("service")` on the resources.
- **Tag**, for a view: in `likec4-extensions/`, `view styled extends index { style element.tag = #backend { color indigo } }`.
  Then `options.WithDefaultViewId("styled")` makes the dashboard open it. Only style tags that some
  resource carries, because an unused tag is not declared.

## Keeping a resource out

```csharp
builder.AddRedis("cache").ExcludeFromLikeC4();
```

The AspireC4 sidecar is **always** excluded; `WithIncludeAspireC4InternalResource(true)` puts it back
for debugging.

Type-based exclusion applies globally. `ParameterResource` — passwords and secrets added with
`AddParameter()`/`WithParameter()` — is excluded by default:

```csharp
builder.AddAspireC4(options => options
    .WithExcludedResourceType<SomeInternalResource>()
    .WithoutExcludedResourceType<ParameterResource>());
```

By full type name — the only form a TypeScript, Go or Python AppHost can use. Aspire's
`Aspire.Hosting.Azure.AzureEnvironmentResource` is excluded this way by default:

```csharp
builder.AddAspireC4(options => options.WithExcludedResourceType("Acme.Hosting.InternalResource"));
```

A resource is excluded when its runtime type **is, or derives from**, any type in the set. The
by-name form also matches an `internal` type, for example Aspire 17's
`Aspire.Hosting.ApplicationModel.ProjectRebuilderResource` (the `<project>-rebuilder` children).

A resource AspireC4 did not get a builder for, and whose type is too general to exclude, is excluded
by name. Aspire 17's `__dotnet-project-build` is a plain `ExecutableResource`, for example. Do this
after the last resource has been added:

```csharp
if (builder.Resources.FirstOrDefault(r => r.Name == "__dotnet-project-build") is { } build)
    builder.CreateResourceBuilder(build).ExcludeFromLikeC4();
```

Only a resource hidden in its *initial* snapshot is skipped automatically, so one the dashboard hides
later is still drawn. The `purview-aspirec4-enrichment` skill lists the usual cases: the project
build, rebuilders, JavaScript installers, ResourceKit group nodes and integration-added tools.

## Automatic Aspire metadata

### Where each resource comes from

With `Metadata` included, AspireC4 also records each resource's source. It reads it once per start,
and a missing or malformed file never fails generation:

| Key | Resource | Value |
| --- | --- | --- |
| `project-path` | .NET project, or an executable whose working directory holds `package.json`, `pyproject.toml`, `.python-version`, `requirements.txt` or `go.mod` | relative to the AppHost: `../Api/Api.csproj`, `../web` |
| `dotnet-target-framework` | .NET project | `obj/project.assets.json`, else `<TargetFramework(s)>`, else `#:property TargetFramework=` |
| `node-version` / `bun-version` / `package-manager` | JavaScript project | `.nvmrc`, `.node-version` or `engines.node`; `engines.bun`; `packageManager` |
| `python-version` | Python project | `.python-version`, else `requires-python` |
| `go-version` | Go module | `toolchain`, else the `go` directive |
| `container-image`, `container-tag`, `container-digest` | container | `mcr.microsoft.com/mssql/server:2025-latest`, or `image@sha256:…` when pinned |
| `dockerfile` | container built from a Dockerfile | relative to the AppHost |

Only the project's own directory is read: a root `.nvmrc` or a `Directory.Build.props` framework is not
followed. A key you set with `WithMetadata` wins over the detected one.

### Endpoint links and the rest

With `AutoIncludeAspireMetadata` at its default `All`, every element receives `aspire-name` and
`aspire-type` metadata plus links to its allocated HTTP/HTTPS endpoints. Endpoint URLs come from
resource **snapshots**, so they carry the correct public ports rather than the configured ones.

## Make the strings refactor-safe

Constant values passed to `WithTag`, `WithKind`, `WithLikeC4Group` and `WithMetadata` are validated
against a `[LikeC4Registry]` class, so a typo is a build diagnostic rather than a silently missing
tag:

```csharp
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details => details
        .WithTag(ArchitectureRegistry.Tags.External)
        .WithKind(ArchitectureRegistry.ElementKinds.Service))
    .WithLikeC4Group(ArchitectureRegistry.Groups.Platform);
```

See the `purview-aspirec4-validation` skill.

## Related skills

`purview-aspirec4-quickstart` to get the diagram running, `purview-aspirec4-configuration` for the
project-wide options, `purview-aspirec4-enrichment` for migrating an existing AppHost (inline or
ResourceKit) onto a curated diagram, `purview-aspirec4-validation` for the registry and diagnostics,
and `purview-aspirec4-entityframework` for the Entity Framework Core ER diagram.
