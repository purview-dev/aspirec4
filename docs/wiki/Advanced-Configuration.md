# Advanced Configuration

Configuration topics beyond the core options in [Configuration](Configuration.md).

## Additional DSL files

Copy extra user-managed `.c4` files into the output directory (and sync them to the Docker volume in container mode). LikeC4 discovers all `.c4` files in the project directory, so they are included in the diagram without further configuration:

```csharp
builder.AddAspireC4(options => options.WithAdditionalDSLFile("likec4/views.c4"));
```

Relative paths are resolved from the current working directory; the file must exist.

## Additional DSL folders

Register directories whose `.c4` files are included via the `include.paths` field of the generated `likec4.config.json`. LikeC4 recursively scans each directory:

```csharp
builder.AddAspireC4(options => options.WithAdditionalDSLFolder("../../assets/likec4-extensions"));
```

Each entry must be an absolute path to an existing directory (the method validates this at call time). In Docker container mode, each folder is bind-mounted read-only into the container at a deterministic path under `/data/ext/`.

## Image aliases

Register a shorthand key (e.g. `@icons`) mapping to a directory of image files. Aliases are written to the `imageAliases` section of the generated `likec4.config.json`:

```csharp
builder.AddAspireC4(options => options.WithImageAliasFolder("@", "C:/images"));
```

The key must start with `@` and the directory must exist at call time. In Docker container mode, each image directory is bind-mounted read-only at a deterministic path under `/data/img/`. Use aliases in DSL files, for example `icon @/likec4/likec4-logo.svg`.

## Custom icon resolvers

Resolvers are evaluated before built-in auto-icon inference, in registration order; the first non-`null` result wins. Each receives an `IconResolverContext` with:

- `Resource` — the visible Aspire resource being rendered.
- `HiddenOriginal` — the hidden Azure resource when a local surrogate was created via `RunAsContainer()` (useful for richer type-based icon selection).

```csharp
builder.AddAspireC4(options =>
    options.WithIconResolver(ctx =>
        ctx.Resource is MyCustomResource ? "tech:dotnet" : null));
```

## Element kind specifications

Declare custom element kinds with optional style, notation, and technology in the `specification {}` block. These are additive — kinds listed here but not present in the model are still declared:

```csharp
builder.AddAspireC4(options =>
    options.WithElementKindSpec(
        new LikeC4ElementKindSpec("service")
            .WithTechnology("HTTP")
            .WithNotation("API service")
            .WithStyle(new LikeC4ElementKindStyle(
                Shape: "queue",
                Color: "blue",
                Icon: "tech:dotnet",
                Border: "dashed",
                Opacity: 80))
    ));
```

`LikeC4ElementKindStyle` accepts `Shape`, `Color`, `Icon`, `Border`, and `Opacity` tokens.

## Relationship kind specifications

Declare custom relationship kinds with an optional default technology:

```csharp
builder.AddAspireC4(options => options.WithRelationshipKindSpec("async", "AMQP"));
```

When a relationship kind in the model matches an entry with a technology, the full body is emitted rather than a bare `relationship KIND` line.

## Relationship kind syntax

`RelationshipKindSyntax` controls how typed relationships are emitted:

- `Dot` (default): `SOURCE .KIND TARGET`
- `Bracket`: `SOURCE -[KIND]-> TARGET`

```csharp
builder.AddAspireC4(options => options.WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax.Bracket));
```

## Metadata key normalization

`NormaliseMetadataBehaviour` controls how invalid characters in metadata keys are handled. Valid LikeC4 metadata key characters are letters, digits, hyphens, and underscores:

- `Normalise` (default): replace any other character with `_` (`"Azure SKU"` → `"Azure_SKU"`).
- `NormaliseLowercase`: same, but also lowercases (`"Azure SKU"` → `"azure_sku"`).
- `Throw`: throw an `ArgumentException` for invalid keys.

## Aspire metadata inclusion

`AutoIncludeAspireMetadata` (`WithAutoIncludeAspireMetadata`) controls which Aspire runtime metadata is injected into generated elements:

- `None` — no automatic metadata.
- `Metadata` — `aspire-name` and `aspire-type` entries.
- `Links` — allocated HTTP/HTTPS endpoint URLs as element links.
- `All` (default) — both.

## Config file generation

`GenerateConfigFile` (default `true`) produces `likec4.config.json` in the output directory with the project title, `include.paths`, and `imageAliases`. Disable with `WithoutConfigFileGeneration()` to manage the config manually. When generation is enabled, `ConfigFileMetadata` adds extra key/value pairs to the config's `metadata` section.

## Type-based exclusions

`ExcludedResourceTypes` controls which resource types are omitted from the diagram (type and subclasses). The default excludes `ParameterResource`. See [Customizing Resources](Customizing-Resources.md).

## Formatting timeouts

`ExternalProcessTimeoutSeconds` (default 30) caps how long the optional `npx likec4 format` step can block startup. See [Generated Output](Generated-Output.md).

## Including the internal resource

`WithIncludeAspireC4InternalResource(true)` includes the AspireC4 server sidecar in the diagram, which exists purely for debugging/monitoring. It is excluded by default.

## Next pages

- [Configuration](Configuration.md)
- [Generated Output](Generated-Output.md)