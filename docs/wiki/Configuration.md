# Configuration

Configure the diagram through `AspireC4DiagramOptions`. Pass a callback to `AddAspireC4`:

```csharp
builder.AddAspireC4(options =>
    options
        .WithTitle("My App")
        .WithAutoIcons(false)
        .WithHideFromDashboard("Architecture")
);
```

> [!TIP]
> Options can also be bound from configuration. The section name is `AspireC4`, so `appsettings.json` entries such as `"AspireC4": { "ViewTitle": "Architecture" }` (or matching environment variables) are applied on top of the builder-time snapshot.

## Options reference

| Property | Default | Description |
| --- | --- | --- |
| `GeneratedViewId` | `null` (`index`) | LikeC4 view ID emitted in the generated `.c4` file (e.g. `view index { ... }`). Change it if the ID conflicts with a hand-authored view. |
| `DefaultViewId` | `"index"` | View ID used in the `/view/{id}` URL the Aspire dashboard links to. `null`/empty links to the server root instead. |
| `Title` | `null` | Title shown in the LikeC4 application. |
| `ViewTitle` | `"Architecture"` | Title shown in the generated view. |
| `ViewDescription` | `null` | Optional view description (Markdown supported by recent LikeC4 versions). |
| `OutputDirectory` | `"./likec4/gen/"` | Directory where the generated `.c4` file is written. |
| `FileName` | `"model.gen"` | Generated file name without extension. |
| `DisableHMR` | `false` | Disable the Hot Module Replacement channel. |
| `HMRPort` | `null` (dynamic) | Fixed HMR port when the LikeC4 server supports configurable ports (v1.57+); ignored on older versions, which always use port `24678`. |
| `ContainerImageTag` | `null` (`latest`) | Pin the `ghcr.io/likec4/likec4` image tag (ignored with `.WithLocalCLI()`). |
| `CheckLatestImageVersion` | `true` | When using the `latest` tag, run a throwaway container at startup to resolve the actual version so version-gated features (e.g. HMR port mode) are configured correctly. |
| `AutoIconsEnabled` | `true` | Infer LikeC4 icons from resource type and name. |
| `HideFromDashboard` | `false` | Hide the sidecar from the dashboard and surface the diagram as a link/command on project resources. |
| `DashboardLinkDisplayName` | `"Architecture Diagram"` | Display name for the diagram link/command when hidden from the dashboard. |
| `RelationshipKindSyntax` | `Dot` | DSL syntax for typed relationships: `Dot` (`SOURCE .KIND TARGET`) or `Bracket` (`SOURCE -[KIND]-> TARGET`). |
| `FormatGeneratedFile` | `false` | Run `npx likec4 format --files <file>` after writing the generated file. Failures are ignored. |
| `ExternalProcessTimeoutSeconds` | `30` | Max seconds to wait for the external formatter process before killing it. |
| `UseDotIfAvailable` | `true` | Use GraphViz' `dot` executable for more accurate icon inference when on `PATH`. |
| `ElementKindSpecs` | `[]` | Custom element kind specifications emitted in the `specification {}` block (style, notation, technology). |
| `RelationshipKindSpecs` | `[]` | Custom relationship kind specifications emitted in the `specification {}` block (technology). |
| `AutoIncludeAspireMetadata` | `All` | Which Aspire metadata is auto-injected: `None`, `Metadata` (`aspire-name`, `aspire-type`), `Links` (endpoint URLs), or `All`. |
| `NormaliseMetadataBehaviour` | `Normalise` | How invalid characters in metadata keys are handled: `Normalise`, `NormaliseLowercase`, or `Throw`. |
| `AdditionalDSLFiles` | `[]` | Extra user-managed `.c4` files copied into the output directory and synced to the container volume. |
| `AdditionalDSLFolders` | `[]` | Directories scanned recursively for `.c4` files, added to `include.paths` in the generated config. |
| `ImageAliases` | `{}` | Image alias definitions (keys start with `@`) written to the `imageAliases` section of the generated config. |
| `GenerateConfigFile` | `true` | Generate a `likec4.config.json` in the output directory. |
| `IncludeAspireDashboardLinks` | `true` | Add links from each element to the Aspire dashboard console/structured-logs pages (requires `AutoIncludeAspireMetadata.Links`). |
| `IncludeAspireTokenInDashboardLinks` | `false` | **Security risk** — embed the Aspire browser token in dashboard links. See below. |
| `StateTagMap` | `{}` | Override the `aspire-run-state-*` tag applied for a given resource state; `null` suppresses the tag. |
| `IncludeDefaultStateStyles` | `true` | Emit default `style element.tag = #aspire-run-state-* {}` rules in the generated view. |
| `IncludeAspireC4InternalResource` | `false` | Include the internal AspireC4 server resource in the diagram (for debugging). |
| `ExcludedResourceTypes` | `{ParameterResource}` | Resource types excluded from the diagram (type and subclasses). |
| `IconResolvers` | `[]` | Custom icon resolvers evaluated before built-in icon inference. |
| `ConfigFileMetadata` | `{}` | Additional metadata included in the generated `likec4.config.json`. |

## Fluent methods

Every property has a corresponding fluent `With*` method, for example:

- `WithGeneratedViewId(string?)`, `WithDefaultViewId(string?)`
- `WithTitle(string?)`, `WithViewTitle(string)`, `WithViewDescription(string?)`
- `WithOutputDirectory(string)`, `WithFileName(string)`
- `WithHMRDisabled(bool = true)`
- `WithContainerImageTag(string?)`, `WithCheckLatestImageVersion(bool = true)`
- `WithAutoIcons(bool = true)`
- `WithHideFromDashboard(string displayName = "Architecture Diagram")`
- `WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax)`
- `WithFormatGeneratedFile(bool = true)`
- `WithAutoIncludeAspireMetadata(AspireMetadataInclusion)`
- `WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour)`
- `WithoutConfigFileGeneration()`
- `WithAspireDashboardLinks(bool = true)`, `WithAspireTokenInDashboardLinks(bool = true)`
- `WithDefaultStateStyles(bool = true)`, `WithStateTag(string state, string? tag)`
- `WithUseDotIfAvailable(bool)`
- `WithIconResolver(Func<IconResolverContext, string?>)`
- `WithElementKindSpec(LikeC4ElementKindSpec)`, `WithRelationshipKindSpec(...)`
- `WithAdditionalDSLFile(string)`, `WithAdditionalDSLFolder(string)`, `WithImageAliasFolder(string, string)`
- `WithExcludedResourceType<T>()`, `WithoutExcludedResourceType<T>()`
- `WithIncludeAspireC4InternalResource(bool)`

See [Advanced Configuration](Advanced-Configuration.md) for the folder, alias, resolver, and spec extensions, and [Source Generator Validation](Source-Generator.md) for the `AspireC4Strict` MSBuild property.

## Common examples

### Hide the sidecar from the dashboard

```csharp
builder.AddAspireC4(options => options.WithHideFromDashboard());
```

### Disable hot reload

```csharp
builder.AddAspireC4(options => options.WithHMRDisabled());
```

### Pin the LikeC4 container version

```csharp
builder.AddAspireC4(options => options.WithContainerImageTag("1.57"));
```

### Security note — dashboard tokens in links

`IncludeAspireTokenInDashboardLinks` embeds the Aspire browser token in generated dashboard links. Only enable this if you understand the implications: the token grants the same access as a browser session and is written into the diagram file, which may be shared or stored in source control. Keep it disabled for normal development, and consider excluding the generated file from source control if you enable it.