---
name: purview-aspirec4-configuration
description: >-
  Use when tuning AspireC4DiagramOptions - the view and title settings, output directory and
  generated file, the LikeC4 container tag and HMR, icon inference, Aspire metadata injection and
  dashboard links, run-state tags and styles, additional DSL files and image aliases, and the
  security note on embedding the Aspire token.
metadata:
  product: AspireC4
  repository: purview-dev/aspirec4
---

# Configuring the diagram

```csharp
builder.AddAspireC4(options =>
    options
        .WithTitle("My App")
        .WithAutoIcons(false)
        .WithHideFromDashboard("Architecture")
);
```

Options also bind from configuration under the **`AspireC4`** section, applied **on top of** the
builder-time snapshot — so `"AspireC4": { "ViewTitle": "Architecture" }` in `appsettings.json`, or a
matching environment variable, overrides the callback. That is the answer to "why is my callback
value being ignored".

Every property has a matching fluent `With*` method.

## View and output

| Property | Default | Description |
| --- | --- | --- |
| `GeneratedViewId` | `null` (`index`) | the view ID emitted in the generated `.c4` file. Change it if it clashes with a hand-authored view |
| `DefaultViewId` | `"index"` | the view ID in the `/view/{id}` URL the dashboard links to; `null`/empty links to the server root |
| `Title` | `null` | title shown in the LikeC4 application |
| `ViewTitle` | `"Architecture"` | title shown in the generated view |
| `ViewDescription` | `null` | optional description; Markdown in recent LikeC4 versions |
| `OutputDirectory` | `"./.aspirec4/likec4/"` | where the generated `.c4` file is written |
| `FileName` | `"model.gen"` | file name, without extension |
| `ProjectDirectory` | `null` | an existing hand-authored LikeC4 project to generate into and serve — `WithLikeC4ProjectFolder(path)` |
| `UseLegacyLayout` | `false` | restores the pre-13.6 paths (`./likec4/gen/`, `./erd/gen/`) and flat dashboard names. A compatibility shim |
| `GenerateConfigFile` | `true` | emit a `likec4.config.json` into the output directory |

## Server, container and hot reload

| Property | Default | Description |
| --- | --- | --- |
| `ContainerImageTag` | `null` (`latest`) | pin `ghcr.io/likec4/likec4`. **Ignored with `.WithLocalCLI()`** |
| `CheckLatestImageVersion` | `true` | on the `latest` tag, run a throwaway container at startup to resolve the real version, so version-gated features are configured correctly |
| `DisableHMR` | `false` | disable the Hot Module Replacement channel |
| `HMRPort` | `null` (dynamic) | a fixed HMR port where LikeC4 supports it (v1.57+); ignored on older versions, which always use `24678` |
| `ExternalProcessTimeoutSeconds` | `30` | how long to wait for an external process before killing it |
| `FormatGeneratedFile` | `false` | run `npx likec4 format --files <file>` after writing. **Failures are ignored** |

`CheckLatestImageVersion` exists because `latest` does not tell you what it is: a feature gated on
LikeC4 v1.57 cannot be configured correctly without resolving the actual version first.

## The dashboard

| Property | Default | Description |
| --- | --- | --- |
| `HideFromDashboard` | `false` | hide the sidecar and surface the diagram as a link/command on project resources |
| `DashboardLinkDisplayName` | `"Architecture Diagram"` | the display name when hidden |
| `IncludeAspireDashboardLinks` | `true` | link each element to the dashboard console and structured-logs pages. Requires `AutoIncludeAspireMetadata.Links` |
| `IncludeAspireTokenInDashboardLinks` | `false` | **security risk** — see below |
| `IncludeAspireC4InternalResource` | `false` | include the AspireC4 server resource itself, for debugging |

> **`IncludeAspireTokenInDashboardLinks` embeds the Aspire browser token in generated links.** The
> token grants the same access as a browser session, and it is written **into the diagram file**,
> which may be shared or committed. Keep it off for normal development, and if you do enable it,
> consider excluding the generated file from source control.

## Icons, metadata and state

| Property | Default | Description |
| --- | --- | --- |
| `AutoIconsEnabled` | `true` | infer LikeC4 icons from resource type and name |
| `UseDotIfAvailable` | `true` | use GraphViz `dot` for more accurate icon inference when it is on `PATH` |
| `IconResolvers` | `[]` | custom resolvers, evaluated **before** built-in inference |
| `AutoIncludeAspireMetadata` | `All` | `None`, `Metadata` (`aspire-name`, `aspire-type`), `Links` (endpoint URLs), or `All` |
| `NormaliseMetadataBehaviour` | `Normalise` | how invalid characters in metadata keys are handled: `Normalise`, `NormaliseLowercase`, or `Throw` |
| `ExcludedResourceTypes` | `{ParameterResource}` | types excluded from the diagram, including subclasses |
| `StateTagMap` | `{}` | override the `aspire-run-state-*` tag for a given state; `null` suppresses it |
| `IncludeDefaultStateStyles` | `true` | emit default `style element.tag = #aspire-run-state-* {}` rules |

`ParameterResource` is excluded by default because that is where `AddParameter()`/`WithParameter()`
puts passwords and secrets.

## DSL, specification and assets

| Property | Default | Description |
| --- | --- | --- |
| `RelationshipKindSyntax` | `Dot` | `Dot` (`SOURCE .KIND TARGET`) or `Bracket` (`SOURCE -[KIND]-> TARGET`) |
| `ElementKindSpecs` | `[]` | custom element kinds emitted in the `specification {}` block — style, notation, technology |
| `RelationshipKindSpecs` | `[]` | custom relationship kinds in the same block |
| `AdditionalDSLFiles` | `[]` | extra user-managed `.c4` files copied into the output directory and synced to the container volume |
| `AdditionalDSLFolders` | `[]` | directories scanned recursively for `.c4` files, added to `include.paths` in the generated config |
| `ImageAliases` | `{}` | alias definitions (keys begin with `@`) written to `imageAliases` in the generated config |
| `ConfigFileMetadata` | `{}` | extra metadata for the generated `likec4.config.json` |

## The fluent surface

```csharp
WithGeneratedViewId(string?)              WithDefaultViewId(string?)
WithTitle(string?)                        WithViewTitle(string)          WithViewDescription(string?)
WithOutputDirectory(string)               WithFileName(string)
WithHMRDisabled(bool = true)
WithContainerImageTag(string?)            WithCheckLatestImageVersion(bool = true)
WithAutoIcons(bool = true)                WithUseDotIfAvailable(bool)
WithIconResolver(Func<IconResolverContext, string?>)
WithHideFromDashboard(string displayName = "Architecture Diagram")
WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax)
WithFormatGeneratedFile(bool = true)
WithAutoIncludeAspireMetadata(AspireMetadataInclusion)
WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour)
WithoutConfigFileGeneration()
WithAspireDashboardLinks(bool = true)     WithAspireTokenInDashboardLinks(bool = true)
WithDefaultStateStyles(bool = true)       WithStateTag(string state, string? tag)
WithElementKindSpec(LikeC4ElementKindSpec)
WithRelationshipKindSpec(LikeC4RelationshipKindSpec)
WithAdditionalDSLFile(string)             WithAdditionalDSLFolder(string)
WithImageAliasFolder(string, string)
WithExcludedResourceType<T>()             WithoutExcludedResourceType<T>()
WithIncludeAspireC4InternalResource(bool)
WithLegacyLayout()                        WithLikeC4ProjectFolder(string)
```

## Related skills

`purview-aspirec4-quickstart` for installing and running it, `purview-aspirec4-resources` for
per-resource customisation, `purview-aspirec4-validation` for the registry that keeps the kind
and tag strings honest, and `purview-aspirec4-entityframework` for the Entity Framework Core ER
diagram and its own options.
