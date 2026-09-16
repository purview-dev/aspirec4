# Generated Output

AspireC4 writes a LikeC4 project into the output directory (`./likec4/gen/` by default) and serves it with the LikeC4 server.

## Files

| File | Purpose |
| --- | --- |
| `model.gen.c4` | The generated model — see below. |
| `likec4.config.json` | LikeC4 project configuration (`name`, `title`, `include.paths`, `imageAliases`, `metadata`). Generated when `GenerateConfigFile` is `true`. |
| Additional `.c4` files | Any files registered through `WithAdditionalDSLFile` are copied here; LikeC4 discovers all `.c4` files in the project directory automatically. |

## The generated `.c4` file

The file starts with an auto-generated header and is split into `specification {}`, `model {}`, and `views {}` blocks:

- **`specification {}`** declares every element kind, relationship kind, and tag used in the model, plus any specs from `ElementKindSpecs`/`RelationshipKindSpecs`. When `IncludeDefaultStateStyles` is enabled, all known state tags are declared up-front so the block is stable regardless of the current resource states.
- **`model {}`** emits each element as `<id> = <kind> 'label' { ... }` with tags, technology, summary, description, icon, links, and metadata, plus relationships using the configured `RelationshipKindSyntax` (`SOURCE .KIND TARGET` or `SOURCE -[KIND]-> TARGET`).
- **`views {}`** emits the `index` view (or the `GeneratedViewId`) with its title/description, `group 'label' { include ... }` blocks, `include *`, and `style element.tag = #aspire-run-state-* {}` rules for the built-in state styles.

> [!WARNING]
> The file is auto-generated. Do not edit it manually — changes are overwritten on the next regeneration (at startup or on a resource state change).

## Regeneration behavior

- The file is written **before** the application starts.
- At runtime, resource state changes trigger a **debounced** regeneration (300 ms), so the diagram reflects the current state of each resource.
- Writes are skipped when the generated content is unchanged, avoiding needless file churn and git noise.
- In publish mode (`aspire publish`), the file is generated once and the server is not started.

## Formatting

When `FormatGeneratedFile` is `true`, AspireC4 runs `npx likec4 format --files <file>` against the generated file immediately after writing it. The formatter modifies the file in place so the on-disk copy is human-readable; the formatted content is also what is synced to the container workspace. Failures are silently ignored. `ExternalProcessTimeoutSeconds` caps how long this can block startup (default 30 s).

## Config file

`likec4.config.json` uses the LikeC4 `$schema` and includes:

- `name` — the LikeC4 project name (unique within the workspace).
- `title` — when `Title` is set.
- `include.paths` — for each `AdditionalDSLFolders` entry (LikeC4 recursively scans the folder for `.c4` files).
- `imageAliases` — for each `ImageAliases` entry (keys start with `@`).
- `metadata` — for each `ConfigFileMetadata` entry.

Set `WithoutConfigFileGeneration()` (or `GenerateConfigFile = false`) to manage `likec4.config.json` manually, for example when the output directory is already part of a hand-curated LikeC4 project.

## Icons

- **Auto icons** (`AutoIconsEnabled`, default `true`): icons are inferred from the resource type and name using the bundled icon manifest.
- **Custom resolvers** (`IconResolvers`): evaluated in registration order before built-in inference; the first non-`null` result wins. Each resolver receives an `IconResolverContext` exposing the visible `Resource` and the `HiddenOriginal` Azure resource (when a local surrogate was created via `RunAsContainer()`).
- **GraphViz `dot`** (`UseDotIfAvailable`, default `true`): when `dot` is on `PATH`, LikeC4 uses it for more accurate inference (e.g. database icons) and can infer icons from container/component relationships.

## Version detection

When the container image tag resolves to `latest` (default), `CheckLatestImageVersion` (default `true`) runs a throwaway container at startup to call `likec4 --version` and resolve the actual version. The resolved version configures version-gated features (such as configurable HMR ports) and is surfaced as a **LikeC4 Version** property on the dashboard resource. Set it to `false` for faster startup if you pin an explicit tag or accept possible misconfiguration.

## Live state reflection

Each element is tagged `aspire-run-state-<state>` based on the live Aspire resource state, and the default view styles color it accordingly:

| State tag | Default style |
| --- | --- |
| `aspire-run-state-starting` | sky |
| `aspire-run-state-waiting` | sky |
| `aspire-run-state-running` | green |
| `aspire-run-state-stopping` | slate, 60% opacity |
| `aspire-run-state-exited` | muted, 30% opacity |
| `aspire-run-state-finished` | muted, 30% opacity |
| `aspire-run-state-runtimeunhealthy` | amber |
| `aspire-run-state-failedtostart` | red |

Override per-state tags with `WithStateTag(state, tag)` (pass `null` to suppress), and disable the built-in style rules with `WithDefaultStateStyles(false)` if you prefer to style state tags in your own DSL.

## Next pages

- [Dashboard Integration](Dashboard-Integration.md)
- [Configuration](Configuration.md)
- [Advanced Configuration](Advanced-Configuration.md)