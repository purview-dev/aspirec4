# AspireC4 Wiki

This wiki is the project documentation hub for `AspireC4.Hosting`, an [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) extension library that generates live [LikeC4](https://likec4.dev) architecture diagrams from the Aspire resource graph.

AspireC4 turns your distributed application into a living architecture diagram. On startup it builds a `.c4` model file from the Aspire resource graph, starts the official LikeC4 server as a sidecar, and keeps the diagram refreshed as resources start, stop, and change state at runtime.

## Start here

- [Getting Started](Getting-Started.md)
- [Configuration](Configuration.md)
- [Customizing Resources](Customizing-Resources.md)
- [TypeScript AppHosts](Typescript-AppHost.md)
- [Generated Output](Generated-Output.md)
- [Dashboard Integration](Dashboard-Integration.md)
- [Local CLI Runtimes](Local-CLI.md)
- [Source Generator Validation](Source-Generator.md)
- [Advanced Configuration](Advanced-Configuration.md)
- [Migration Guide](Migration.md)
- [Contributing](Contributing.md)
- [Release Flow](Release-Flow.md)

## Feature highlights

- **Live diagram generation.** `AddAspireC4()` registers a lifecycle hook that generates the `.c4` file before startup and regenerates it (debounced) whenever a resource changes state — so the diagram always reflects the current application.
- **Docker or local CLI.** The LikeC4 server runs as the `ghcr.io/likec4/likec4` container by default, or through a local JavaScript package manager CLI (`npx`, `pnpm`, `yarn`, `bun`, or `deno`) with `.WithLocalCLI()`.
- **Rich per-resource customization.** `WithLikeC4Details()` controls labels, technologies, descriptions, summaries, icons, kinds, tags, links, and metadata; `WithLikeC4Reference()` customizes relationships; `WithLikeC4Group()` groups resources in the generated view.
- **Automatic icons.** Icons are inferred from resource type and name using a bundled manifest, with custom `IconResolvers` evaluated first, and optional GraphViz `dot` assistance.
- **Compile-time validation.** A Roslyn source generator validates `.WithTag()`, `.WithKind()`, `.WithLikeC4Group()`, and `.WithMetadata()` values against a `[LikeC4Registry]` class and/or LikeC4 `specification` blocks, with severity controls from suggestions to errors.
- **TypeScript AppHost support.** The same features are available to TypeScript AppHosts through the Aspire-generated fluent API.
- **Dashboard integration.** The diagram URL is surfaced in the Aspire dashboard, with optional per-resource dashboard links, and support for hiding the sidecar in favour of links on project resources.

## Prerequisites

- **.NET 8 or later**.
- **Aspire 13.5.3 or later**, with `Aspire.Hosting.AppHost` referenced by the AppHost project.
- **Docker** (default LikeC4 server), or a local Node.js CLI runtime if you use `.WithLocalCLI()`.

See [Getting Started](Getting-Started.md) for installation and the first run.