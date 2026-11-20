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
| `WithTechnology(string?)` | Technology string shown beneath the label (e.g. `.NET`, `Redis`). When unset it is detected; see [Technology and icons](../Generated-Output.md#technology-and-icons). |
| `WithDescription(string?)` | Longer description rendered in the detail panel (Markdown). |
| `WithSummary(string?)` | One-line summary shown in tooltips/map view. |
| `WithIcon(string?)` | Icon identifier (e.g. `tech:dotnet`, `azure:storage-accounts`); `null` reverts to automatic inference. |
| `WithAutoIcon(bool?)` | Per-element override for auto-icon inference (`null` inherits the project setting). |
| `WithKind(string?)` | Element kind override (e.g. `service`); must be a valid LikeC4 identifier. |
| `WithTag(string)` | Adds a tag; a leading `#` is accepted and stripped. |
| `WithLink(string \| Uri, string? title)` | Adds a hyperlink (absolute, or relative to the `.c4` file). |
| `WithMetadata(string key, string value)` | Adds a metadata key/value pair. |

## Relationships — `WithLikeC4Reference` and `WithLikeC4ConnectionReference`

Customizes how the relationship from a resource to a target appears in the diagram:

```csharp
// Draws the relationship only. Target is any resource builder.
builder.AddNodeApp("app", ...)
    .WithLikeC4Reference(redis, opts => opts.WithLabel("Caches sessions").WithTechnology("Redis Protocol").WithKind("RESP"));

// Draws the relationship AND calls Aspire's WithReference, so the connection string reaches the consumer.
// Target is a resource that exposes a connection string.
builder.AddProject<Projects.Api>("api")
    .WithLikeC4ConnectionReference(db, opts => opts.WithLabel("Persists data"));
```

The names are the same in every AppHost language (`withLikeC4ConnectionReference`,
`WithLikeC4ConnectionReference`, `with_like_c4_connection_reference`). Pass `connectionName` to name the
connection, or `skipAspireReference: true` to only describe a reference that already exists.

> [!WARNING]
> `WithLikeC4Reference` also has a connection-string overload, kept for compatibility, but C# only
> chooses it when you pass one of its connection arguments. `WithLikeC4Reference(db, opts => ...)` binds
> to the relationship-only overload and does **not** call `WithReference`. Use
> `WithLikeC4ConnectionReference` when you want the connection.

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

Types can also be named by their full type name, which is the form a TypeScript, Go or Python AppHost
uses (their bindings cannot pass a C# type argument), and works for a type from a package the AppHost
does not reference. Aspire's Azure provisioning environment is excluded this way by default: it is
infrastructure, and its fallback `system` kind collides with the `system` element kind most LikeC4
projects declare.

```csharp
builder.AddAspireC4(options => options
    .WithExcludedResourceType("Acme.Hosting.InternalResource")
    .WithoutExcludedResourceType("Aspire.Hosting.Azure.AzureEnvironmentResource"));
```

```typescript
opts.withExcludedResourceType("Acme.Hosting.InternalResource");
```

## Aspire metadata injection

When `AutoIncludeAspireMetadata` is `All` (the default), each element automatically receives `aspire-name`/`aspire-type` metadata and links to its allocated HTTP/HTTPS endpoints. Endpoint URLs come from resource snapshots so they use the correct public ports.

### Where each resource comes from

With `Metadata` included, AspireC4 also records where each resource comes from. A local project gets
its path and the runtime version its own files declare. A container gets its image:

```likec4
data_service = component 'data-service' {
  metadata {
    aspire-name 'data-service'
    project-path '../DataService/DataService.csproj'
    dotnet-target-framework 'net10.0'
  }
}
sql = container 'sql' {
  metadata {
    aspire-name 'sql'
    container-image 'mcr.microsoft.com/mssql/server:2025-latest'
    container-tag '2025-latest'
  }
}
```

| Key | Resource | Read from |
| --- | --- | --- |
| `project-path` | .NET project; an executable whose working directory holds `package.json`, `pyproject.toml`, `.python-version`, `requirements.txt` or `go.mod` | the project file (`.csproj`, or the `.cs` file of a file-based app), or the project directory |
| `dotnet-target-framework` | .NET project | `obj/project.assets.json`, then `<TargetFramework(s)>` in the project file, or `#:property TargetFramework=` in a file-based app |
| `node-version` | Node project | `.nvmrc`, then `.node-version`, then `engines.node` in `package.json` |
| `bun-version` | Bun project | `engines.bun` in `package.json` |
| `package-manager` | JavaScript project | `packageManager` in `package.json`, without Corepack's `+sha512` suffix |
| `python-version` | Python project | `.python-version`, then `requires-python` in `pyproject.toml` |
| `go-version` | Go module | `toolchain` in `go.mod`, then its `go` directive |
| `container-image` | container | the full image reference: `registry/image:tag`, or `registry/image@sha256:…` when pinned by digest |
| `container-tag`, `container-digest` | container | the tag, or the digest, separately |
| `dockerfile` | container built from a Dockerfile | the Dockerfile path |

- **Paths are relative to the AppHost directory**, with forward slashes, so the same model is generated
  on every machine.
- **Only the project's own directory is read.** A `.nvmrc` at the repository root, or a framework set in a
  `Directory.Build.props`, is not followed. For .NET, `project.assets.json` resolves the framework
  however it was set, so it is present once the project has been restored.
- **It is read once per AppHost start**, not on every regeneration, and it is best-effort: a missing or
  malformed file contributes nothing and never stops the diagram being generated.
- **A key you set wins.** `WithLikeC4Details(d => d.WithMetadata("project-path", "src/Api"))` replaces the
  detected value, as it does for `aspire-name`. `WithAutoIncludeAspireMetadata(AspireMetadataInclusion.Links)`
  turns all of it off.

## Next pages

- [Generated Output](../Generated-Output.md)
- [Advanced Configuration](Advanced.md)
- [Source Generator Validation](Source-Generator.md)