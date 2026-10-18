---
name: purview-aspirec4-resources
description: >-
  Use when customising how an Aspire resource appears in an AspireC4 LikeC4 diagram - WithLikeC4Details
  for labels, technology, icons, kinds, tags, links and metadata; WithLikeC4Reference for typed
  relationships; WithLikeC4Group for grouping; and ExcludeFromLikeC4 or type-based exclusion for
  keeping a resource out.
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
| `WithTechnology(string?)` | shown beneath the label — `.NET`, `Redis` |
| `WithDescription(string?)` | the detail panel; Markdown |
| `WithSummary(string?)` | one line, for tooltips and the map view |
| `WithIcon(string?)` | `tech:dotnet`, `azure:storage`; `null` reverts to automatic inference |
| `WithAutoIcon(bool?)` | per-element override; `null` inherits the project setting |
| `WithKind(string?)` | element kind override; must be a valid LikeC4 identifier |
| `WithTag(string)` | adds a tag — a leading `#` is accepted and stripped |
| `WithLink(string \| Uri, string? title)` | absolute, or relative to the `.c4` file |
| `WithMetadata(string key, string value)` | a key/value pair |

## `WithLikeC4Reference` — the relationship

Two overloads, and the difference matters:

```csharp
// Any resource builder - describes the relationship only.
builder.AddNodeApp("app", ...)
    .WithLikeC4Reference(redis, opts => opts
        .WithLabel("Caches sessions")
        .WithTechnology("Redis Protocol")
        .WithKind("RESP"));

// A resource with a connection string - ALSO calls Aspire's WithReference.
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Reference(db, opts => opts.WithLabel("Persists data"), connectionName: "postgres");
```

The `IResourceWithConnectionString` overload wires up Aspire's `WithReference` as well, so the
connection string reaches the consumer. Pass `skipAspireReference: true` to opt out — useful when the
reference already exists and you only want to describe it.

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
view.

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

A resource is excluded when its runtime type **is, or derives from**, any type in the set.

## Automatic Aspire metadata

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
project-wide options, `purview-aspirec4-validation` for the registry and diagnostics, and
`purview-aspirec4-entityframework` for the Entity Framework Core ER diagram.
