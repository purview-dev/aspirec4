---
name: purview-aspirec4-configuration
description: >-
  Use when tuning AspireC4DiagramOptions - the view and title settings, output directory and
  generated file, the LikeC4 container tag and HMR, icon inference, Aspire metadata injection and
  dashboard links, run-state tags and styles, additional DSL files and image aliases, exporting the
  views as images on startup, and the security note on embedding the Aspire token.
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

## Legacy layout

`UseLegacyLayout` / `WithLegacyLayout()` turns on **legacy mode**: the whole pre-13.6 layout, not just
the architecture diagram's path.

- Architecture output: `./likec4/gen/` instead of `./.aspirec4/likec4/`.
- ER output: `./erd/gen/` instead of `./.aspirec4/erd/`. There is no separate ER switch; the ER diagram
  follows the architecture diagram so the two cannot disagree.
- Dashboard rows stay flat as `aspirec4` and `aspirec4-er`, with no shared parent and no `-likec4`
  suffix.

```csharp
builder.AddAspireC4(options => options.WithLegacyLayout());
```

It can also be set from configuration — `"AspireC4": { "UseLegacyLayout": true }` or
`AspireC4__UseLegacyLayout=true` — and, unlike most options, is read when the builder runs rather than
when the runtime options are resolved. An explicit `OutputDirectory` still wins over the legacy
default, and `WithLikeC4ProjectFolder` is unaffected: generated output always stays beneath the project
being served.

It is a compatibility shim and will be removed in a future major version. Prefer adopting
`**/.aspirec4/` in `.gitignore` and updating anything that addresses the `aspirec4` resource by name.

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

## Image export

Every view is written out as an image whenever the generated model changes — on by default.

| Property | Default | Description |
| --- | --- | --- |
| `ExportImages` | `true` | write each view out as an image when the generated model changes |
| `ImageFormats` | `{Png}` | `Png` and/or `Jpg`. **There is no SVG** — see below |
| `ImageExportDirectory` | `null` (`./.aspirec4/images/`) | where images are written; relative paths resolve against the **AppHost directory** |
| `ImageExportTimeoutSeconds` | `300` | cap on one `likec4 export` invocation |
| `ContainerRuntimeCommand` | `"docker"` | the runtime for the one-shot export container; set to `podman` where that is what Aspire uses |

**Both diagrams export into the same folder**, `./.aspirec4/images/`, so there is one place to look
rather than one per diagram:

```text
apphost/.aspirec4/images/
  architecture-index.png        <- architecture diagram, one file per view
  architecture-services.png
  schema.svg                    <- ER diagram
  schema.png
```

The architecture diagram's files carry an `architecture-` prefix, because `likec4 export` names a file
after its view id and a bare `index.png` says nothing in a shared folder. The ER diagram row in the
dashboard carries an **Images** property with the resolved path and an **Open Diagram Images** command
that opens the folder.

Because the default is resolved from the AppHost directory rather than from `OutputDirectory`, setting
`ImageExportDirectory` on only one of the two diagrams splits them apart again.

**`likec4 export` has no SVG format.** It renders by screenshotting each view in a headless browser, and
offers only `png` and `jpg`. For a vector, export the model and render it with another tool. The ER
diagram *does* export SVG, because it renders through d2 — see `purview-aspirec4-entityframework`.

Two consequences follow from the headless browser:

- Export takes tens of seconds and needs a Chromium. Container mode (the default) gets one from the
  `ghcr.io/likec4/likec4` image, which already carries it; **`WithLocalCLI()` mode needs a Playwright
  Chromium installed on the host.**
- It runs in the background and **only when the generated model actually changed**, so the resource
  state churn that regenerates an identical model exports nothing, and startup is never delayed.

Failures are best-effort: they are reported and the generated `.c4` model is unaffected.

```csharp
builder.AddAspireC4(options => options.WithoutImageExport());
builder.AddAspireC4(options => options
    .WithImageExport(LikeC4ImageFormat.Png, LikeC4ImageFormat.Jpg)
    .WithImageExportDirectory("docs/diagrams")   // relative to the AppHost directory
);
```

`WithImageExport()` with no format turns export off, the same as `WithoutImageExport()`.

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
WithImageExport(params LikeC4ImageFormat[])
WithImageExportDirectory(string)          WithoutImageExport()
```

## Related skills

`purview-aspirec4-quickstart` for installing and running it, `purview-aspirec4-resources` for
per-resource customisation, `purview-aspirec4-validation` for the registry that keeps the kind
and tag strings honest, and `purview-aspirec4-entityframework` for the Entity Framework Core ER
diagram and its own options.
