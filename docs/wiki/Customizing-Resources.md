# Customizing Resources

AspireC4 annotates resources in the Aspire app model, so each resource and relationship can be customized before the `.c4` file is generated.

## Element details — `WithLikeC4Details`

Customizes how a resource appears as a node in the diagram:

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

### Available node options

| Method | Purpose |
| --- | --- |
| `WithLabel(string)` | Display label on the element node. |
| `WithTechnology(string?)` | Technology string shown beneath the label (e.g. `.NET`, `Redis`). |
| `WithDescription(string?)` | Longer description rendered in the detail panel (Markdown). |
| `WithSummary(string?)` | One-line summary shown in tooltips/map view. |
| `WithIcon(string?)` | Icon identifier (e.g. `tech:dotnet`, `azure:storage`); `null` reverts to automatic inference. |
| `WithAutoIcon(bool?)` | Per-element override for auto-icon inference (`null` inherits the project setting). |
| `WithKind(string?)` | Element kind override (e.g. `service`); must be a valid LikeC4 identifier. |
| `WithTag(string)` | Adds a tag; a leading `#` is accepted and stripped. |
| `WithLink(string \| Uri, string? title)` | Adds a hyperlink (absolute, or relative to the `.c4` file). |
| `WithMetadata(string key, string value)` | Adds a metadata key/value pair. |

## Relationships — `WithLikeC4Reference`

Customizes how the relationship from a resource to a target appears in the diagram. There are two overloads:

```csharp
// Target is any resource builder.
builder.AddNodeApp("app", ...)
    .WithLikeC4Reference(redis, opts => opts.WithLabel("Caches sessions").WithTechnology("Redis Protocol").WithKind("RESP"));

// Target is a resource that exposes a connection string — also calls Aspire's WithReference.
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Reference(db, opts => opts.WithLabel("Persists data"), connectionName: "postgres");
```

> [!NOTE]
> The connection-string overload (`IResourceWithConnectionString`) additionally wires up Aspire's `WithReference`, so the connection string is available to the consumer. Pass `skipAspireReference: true` to opt out.

### Available relationship options

| Method | Purpose |
| --- | --- |
| `WithLabel(string)` | Short label on the relationship arrow. |
| `WithTechnology(string?)` | Technology/protocol (e.g. `HTTP/2`, `gRPC`, `AMQP`). |
| `WithDescription(string?)` | Longer relationship description. |
| `WithKind(string?)` | Typed relationship kind (e.g. `async`, `sync`), declared in the `specification` block and emitted with the configured `RelationshipKindSyntax`. |
| `WithTag(string)` | Adds a tag to the relationship. |
| `WithLink(string \| Uri, string? title)` | Adds a hyperlink to the relationship. |
| `WithMetadata(string key, string value)` | Adds a metadata key/value pair. |
| `WithNavigateTo(string viewId)` | LikeC4 dynamic view to navigate to when the relationship is clicked. |

Attach multiple annotations to the same source — one per target — to customize each relationship independently.

## Groups — `WithLikeC4Group`

Assigns a resource to a named group. Resources sharing a group are emitted inside a `group 'label' { include ... }` block in the generated view:

```csharp
builder.AddRedis("cache").WithLikeC4Group("Platform");
```

## Excluding resources — `ExcludeFromLikeC4`

```csharp
builder.AddRedis("cache").ExcludeFromLikeC4();
```

The AspireC4 sidecar itself is always excluded from the diagram. Set `WithIncludeAspireC4InternalResource(true)` in the options callback if you want to inspect it.

### Type-based exclusion

By default `ParameterResource` (passwords/secrets added via `AddParameter()`/`WithParameter()`) is excluded. Add or remove types globally:

```csharp
builder.AddAspireC4(options => options
    .WithExcludedResourceType<SomeInternalResource>()
    .WithoutExcludedResourceType<ParameterResource>());
```

A resource is excluded when its runtime type is the same as, or a subclass of, any type in the set.

## Aspire metadata injection

When `AutoIncludeAspireMetadata` is `All` (the default), each element automatically receives `aspire-name`/`aspire-type` metadata and links to its allocated HTTP/HTTPS endpoints. Endpoint URLs come from resource snapshots so they use the correct public ports.

## Next pages

- [Generated Output](Generated-Output.md)
- [Advanced Configuration](Advanced-Configuration.md)
- [Source Generator Validation](Source-Generator.md)