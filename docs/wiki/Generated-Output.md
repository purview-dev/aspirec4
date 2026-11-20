# Generated Output

AspireC4 writes a LikeC4 project into the output directory (`./.aspirec4/likec4/` by default) and serves it with the LikeC4 server.

Every AspireC4 diagram writes beneath the same `./.aspirec4/` root in the AppHost directory — the architecture diagram into `likec4/`, the ER diagram into `erd/`, both diagrams' images into `images/`. AspireC4 writes the `.gitignore` files that keep the generated parts out of git itself, so **your repository needs no entry for it**; see [Source control](#source-control).

> [!NOTE]
> This root is new in 13.6. `WithLegacyLayout()` restores the pre-13.6 paths for an upgrade that
> changes nothing else:
>
> | Diagram | 13.6 default | Legacy mode |
> | --- | --- | --- |
> | Architecture | `./.aspirec4/likec4/` | `./likec4/gen/` |
> | ER | `./.aspirec4/erd/` | `./erd/gen/` |
>
> An explicit `OutputDirectory` always wins over either default. AspireC4 writes its `.gitignore` into
> whichever directory it generates into, so legacy mode needs no repository entry either. See
> [Migration](Migration.md).

## Files

| File | Purpose |
| --- | --- |
| `model.gen.c4` | The generated model — see below. |
| `likec4.config.json` | LikeC4 project configuration (`name`, `title`, `include.paths`, `imageAliases`, `metadata`). Generated when `GenerateConfigFile` is `true`. |
| Additional `.c4` files | Any files registered through `WithAdditionalDSLFile` are copied here; LikeC4 discovers all `.c4` files in the project directory automatically. |

The ER diagram writes its own files beneath `./.aspirec4/erd/` — `schema.gen.json` (or `.mmd`, `.dbml`,
`.d2`, depending on the renderer) plus the viewer assets. It is a separate diagram with its own options;
see [Entity Framework Core ER Diagrams](Entity-Framework-Diagrams.md).

## Exporting images

Both diagrams export to the same `./.aspirec4/images/` folder: the architecture diagram writes one PNG
per view with an `architecture-` prefix, the ER diagram writes `schema.svg` and `schema.png`. Export runs
in the background and only when the output would differ, so startup is never delayed and an unchanged
diagram renders nothing.

The images are the one piece of generated output worth committing — a diagram in a pull request is
documentation. The screenshots embedded in this repository's READMEs and wiki pages are kept in
`docs/images/screenshots/`, captured from the sample AppHost; see its `README.md` for the file list and how
to refresh them. See [Configuration](configuration/README.md) for `ImageExportDirectory`, `ImageFormats` and
`WithoutImageExport()`.

## Source control

AspireC4 writes a `.gitignore` beside what it generates, so a repository that adopts it needs no
ignore entry and sees nothing change from one run to the next:

| Directory | Ignored | Kept visible to git |
| --- | --- | --- |
| `likec4/` | `model.gen.c4`, the generated `likec4.config.json`, copies of `WithAdditionalDSLFile` sources | anything else you put there |
| `erd/` | the diagram (`schema.gen.*`), the viewer (`index.html`, `app.js`), `vendor/` | anything else you put there |
| `images/` | the `.aspirec4-*` stamp files only | **the exported images** |

Why the split:

- **The models are build output, not source.** The architecture model is a picture of a *running*
  system — endpoint ports, dashboard links and each resource's run state legitimately differ between
  runs — so tracking it guarantees a dirty working tree. The ER model is regenerated from whatever the
  projects were last built with.
- **The images are documentation.** A diagram in a pull request is worth reviewing, and an image is
  replaced only when its bytes change, so they do not churn.
- **Exact names, not `*`.** Each `.gitignore` lists the files AspireC4 writes, anchored to its directory,
  so a directory you share with your own files — `WithOutputDirectory("./docs/architecture")` — never
  hides them.
- **Yours are never touched.** If a `.gitignore` that AspireC4 did not write is already there, it is
  left alone and a line in the log says so. AspireC4 recognises its own by the first line,
  `# Written by AspireC4.`

To commit the generated model deliberately, turn this off with `WithoutGitIgnoreFileGeneration()` (or
`"AspireC4": { "GenerateGitIgnoreFile": false }`; the ER diagram has its own
`WithoutGitIgnoreFileGeneration()`). Because files are only written when they change, you then see a diff
only when the model really did — but the architecture model's ports and run states are real changes,
so expect them. If what you need is your own LikeC4 files kept beside the generated model, the
extensions folder below does that without committing generated output.

## Extending the generated model

Anything the Aspire resource graph cannot describe — people, external systems, descriptions, extra
relationships, more views — goes in a **`likec4-extensions/`** folder beside the AppHost. AspireC4 picks it
up automatically when it exists: the folder is added to the generated `likec4.config.json` as an include
path and mounted into the LikeC4 container, exactly as `WithAdditionalDSLFolder` would.

| Path, beside the AppHost | Whose | In git |
| --- | --- | --- |
| `likec4-extensions/people.c4`, `likec4-extensions/extend.c4` | yours, included automatically | committed |
| `.aspirec4/` | generated by AspireC4 | ignored by AspireC4's own `.gitignore` files |

Generated elements are named after their Aspire resources — `builder.AddProject<Projects.Api>("api")`
becomes `api`, and a dash becomes an underscore — so hand-written files can `extend` them and draw
relationships to them. A hand-written file in LikeC4 DSL:

```text
specification {
  element person
}

model {
  customer = person 'Customer'
  customer -> api 'Places orders'

  extend api {
    link https://wiki.example.com/api 'Runbook'
  }
}
```

The names only change if the resources do. `WithExtensionsFolder("./architecture")` uses another
folder (relative to the AppHost directory, or absolute), and `WithoutExtensionsFolder()` turns the
convention off; both are also `"AspireC4": { "ExtensionsDirectory": ... }`. The convention does not apply
with `WithLikeC4ProjectFolder`, whose project already holds your files.

This is the replacement for a separate LikeC4 project that read a committed `model.gen.c4`: move its
`.c4` files into the extensions folder and the generated model is always beside them, current, and never
committed.

## Generating into your own LikeC4 project

`WithLikeC4ProjectFolder(path)` points AspireC4 at a LikeC4 project you already maintain. That directory is what the LikeC4 server serves, so the project's own `likec4.config.json`, `.c4` sources and views are all in play, and the generated model joins them as one more file:

```csharp
builder.AddAspireC4(configure: options => options
    .WithLikeC4ProjectFolder("../../architecture"));
```

| Path | What it is |
| --- | --- |
| `architecture/` | your LikeC4 project, served by LikeC4 |
| `architecture/likec4.config.json` | yours, never written to |
| `architecture/model.c4` | hand-authored |
| `architecture/.aspirec4/model.gen.c4` | generated by AspireC4 |

Generated output goes to `.aspirec4/` beneath the project so it stays out of your hand-curated files. Set `OutputDirectory` as well if you want it elsewhere — an explicit value is always honoured. **No `likec4.config.json` is generated for such a project**: if it has one, overwriting it would discard settings AspireC4 knows nothing about; if it has none, the config is optional to LikeC4 anyway.

### Attaching Aspire state to elements you already declared

`WithLikeC4Element(id)` maps an Aspire resource onto an element your project already declares, so the live state lands on that element instead of a duplicate appearing beside it:

```csharp
builder.AddProject<Projects.Api>("api")
       .WithLikeC4Element("cloud.backend.api");
```

The generated model then emits an `extend` block, in LikeC4 DSL, rather than a declaration:

```text
model {
  extend cloud.backend.api {
    #aspire-run-state-running
    link http://localhost:5123 'Endpoint: http'
    metadata {
      aspire-name 'api'
    }
  }
}
```

Every reference follows the mapping — relationships, nesting and view groups all use the fully-qualified id — and the element's kind is not re-declared in the generated `specification {}`, because your project already declares it.

> [!IMPORTANT]
> Only **tags, links, metadata, relationships and nested elements** are applied. LikeC4's `extend` body accepts nothing else: `title`, `technology`, `summary`, `description` and `icon` are parse errors inside it. Your project therefore stays the single source of those, and anything set for them through `WithLikeC4Details` is ignored for a mapped resource.

AspireC4 never reads your `.c4` sources, so it cannot tell you at build time that an id does not exist — LikeC4 reports that itself, with a proper source location.

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
- A file is written only when its bytes would change. That is checked against the file already on disk, not just the last write of this run, so restarting an unchanged application touches nothing — no content, no timestamp. The header carries no timestamp for the same reason.
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

## Technology and icons

Each element shows a technology label and an icon. Where you set neither, AspireC4 works them out
from what the resource is.

### How a technology is recognised

AspireC4 first checks your own [technology overrides](#overriding-it), then a catalogue of known
technologies. The first match wins:

1. **The app's framework, read from its project.** A JavaScript app's `package.json`, or a Python
   app's `pyproject.toml` or `requirements.txt`, in its working directory. A meta-framework beats what
   it builds on, so an Astro site using Vite reads as **Astro**, a Next.js app as **Next.js** rather
   than React, and a plain Vite app as **Vite**. Recognised frameworks: Astro, Next.js, Nuxt, SvelteKit,
   Remix, Gatsby, Docusaurus, Eleventy, Qwik, Angular, NestJS, Hono, Fastify, Express, Koa, SolidJS,
   Vue, React, Svelte and Vite; for Python, Django, FastAPI, Flask, Streamlit and Gradio. An app run by
   `bun` or `deno` with no known framework reads as Bun or Deno.

   A **.NET project** is read the same way: its MSBuild SDK, NuGet packages and framework references,
   from the restore output (`obj/project.assets.json`) and the project file, or from a file-based app's
   `#:sdk` and `#:package` directives. In priority order it reads as **Azure Functions**, **Orleans**,
   **YARP**, **ASP.NET Core gRPC**, **Blazor WebAssembly**, **ASP.NET Core** (the Web SDK or a
   `Microsoft.AspNetCore.App` framework reference) or **.NET Worker Service**, all with the .NET icon.
   Only the project's own references count, not ones it inherits. A project that is none of these still
   reads as **.NET**.

   A **package-manager installer** resource runs the package manager, not the app, so it is named by its
   command: **npm**, **pnpm**, **Yarn**, **Bun**, **Deno**, **pip**, **Poetry** or **uv**.
2. **The resource's type.** Aspire's and the community toolkit's types are matched across their base
   classes, and by the resource type the dashboard reports. Azure services are named as Azure names
   them: `AzureStorageResource` is **Azure Storage**, with the storage-account icon, and its blob,
   queue and table children each have their own. When an Azure resource runs locally, the label says
   what is running it: **Azure Storage (Azurite)**, **Azure Cosmos DB (emulator)**, or **Azure
   Database for PostgreSQL (container)** after `RunAsContainer()`. The icon stays the Azure service's.
3. **The container image.** For example, `mcr.microsoft.com/mssql/server` is SQL Server, `pgvector/pgvector`
   is PostgreSQL (pgvector), `confluentinc/confluent-local` is Apache Kafka, and `datalust/seq` is Seq.
   The registry, the `library/` namespace, the tag and any digest are ignored. An image name with no
   path, such as `postgres`, also matches any namespace that holds it, such as `bitnami/postgres`.

A recognised technology always uses its curated icon, or none when LikeC4 has no icon for it (Seq,
Keycloak, Valkey, Ollama, MinIO, Mailpit). It never falls back to guessing, because a confident wrong
icon, such as Seq drawn as Sequelize, is worse than none.

Anything not in the catalogue falls back to **fuzzy matching**: the resource's type and name are
scored against icon names in the bundled manifest. A .NET project with no recognised flavour shows
**.NET**, and a container shows its image name.

### Overriding it

When the catalogue gets a resource wrong or does not know it, correct it in one of two places.

**For the whole AppHost**, add a technology override to the options. An override names the label,
and optionally the icon, for every resource it matches. Overrides are checked before the catalogue:

```csharp
builder.AddAspireC4(configure: options => options
    // A dependency of the resource's own project: npm, Python, or a .NET package, framework reference or SDK.
    .WithPackageTechnology("@tanstack/react-start", "TanStack Start", "tech:react")
    .WithPackageTechnology("MassTransit", "MassTransit")
    // A resource type or any subclass: a full name, a short name, or the type itself.
    .WithResourceTypeTechnology("Acme.Hosting.WidgetResource", "Widget", "@/icons/widget.svg")
    .WithResourceTypeTechnology<KeycloakResource>("Keycloak (identity)")
    // A container image; the registry, tag and digest are ignored.
    .WithContainerImageTechnology("acme/billing-engine", "Billing Engine", "tech:java"));
```

- Package rules are checked first, then resource-type rules, then container-image rules. Within each
  kind, the first one you registered wins. An installer is never matched by a package rule.
- A resource-type name with a dot is a full name and must match exactly. A short name matches the
  class name with or without its `Resource` suffix. The resource type the dashboard reports, such as
  `Azure.SqlServer`, is matched too.
- Image patterns follow the catalogue's rules. A pattern with a slash must match the whole path. One
  without, such as `widget`, matches that image in any namespace.
- An override's label is shown exactly as you wrote it. An Azure emulator suffix is not added.
- An override with an icon always uses it, even with auto icons off. One without an icon has it
  matched from its label, so `WithContainerImageTechnology("acme/cache", "Redis")` gets the Redis icon.
- From a TypeScript, Go or Python AppHost the same methods are `withPackageTechnology`,
  `withResourceTypeTechnology` (by name) and `withContainerImageTechnology`.

**For one resource**, use its details. They win over every override:

- `WithLikeC4Details(d => d.WithTechnology("…"))` sets the label. A label you set is also used to
  pick the icon, so `WithTechnology("Azure Postgres")` still matches an Azure icon.
- `WithLikeC4Details(d => d.WithIcon("tech:…"))` sets the icon outright.

A **custom icon resolver** (below) runs before overrides and the catalogue, but it only picks the icon.

### Icon settings

- **Auto icons** (`AutoIconsEnabled`, default `true`): turns the recognition and matching above on or
  off; `WithAutoIcon(false)` turns it off for one element.
- **Technology overrides** (`TechnologyOverrides`): the application-wide rules [above](#overriding-it).
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
- [Configuration](configuration/README.md)
- [Advanced Configuration](configuration/Advanced.md)