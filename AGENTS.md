# AGENTS.md

This file is the **primary instruction set** for AI agents and humans working in this repository. It takes
precedence where anything conflicts. Public API usage docs live in [`README.md`](README.md); the contribution
guide lives in [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Repository overview

**AspireC4.Hosting** is an [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) extension library that
generates live [LikeC4](https://likec4.dev) architecture diagrams from the Aspire resource graph. It ships:

- The `AspireC4.Hosting` library (the Aspire integration).
- An incremental Roslyn source generator (`AspireC4.SourceGenerators`) that validates registry constants and
  `.c4` DSL files against `.WithTag()`, `.WithKind()`, `.WithLikeC4Group()`, and `.WithMetadata()` call sites.
- An optional Entity Framework Core ER diagram: the `AspireC4.Hosting.EntityFrameworkCore` AppHost library,
  the analyzer-only `AspireC4.EntityFrameworkCore` package and the `AspireC4.EntityFrameworkCore.ModelDump`
  build-time tool. It is **additive** — `WithEntityFrameworkDiagram()` extends the builder `AddAspireC4()`
  returns, so consumers run the architecture diagram alone or both diagrams, never the ER diagram alone.
- A `TestAppHost` sample AppHost and a TypeScript AppHost sample.

### Packages and target frameworks

| Package | Project | Target framework | Shipped as |
|---|---|---|---|
| `AspireC4.Hosting` | `src/src/AspireC4/` | `net10.0` | `lib/net10.0/` + `analyzers/dotnet/cs/` + `build/` |
| `AspireC4.Hosting.EntityFrameworkCore` | `src/src/EntityFrameworkCore/` | `net10.0` | `lib/net10.0/` |
| `AspireC4.EntityFrameworkCore` | `src/src/EntityFrameworkCore.Analyzers/` | `netstandard2.0` | `analyzers/dotnet/cs/` only (no runtime assembly) |
| `AspireC4.EntityFrameworkCore.ModelDump` | `src/src/EntityFrameworkCore.ModelDump/` | `net10.0` | `tools/` + `build/` |

AppHosts therefore target `net10.0`. `src/Directory.Build.props` owns the framework; the two Roslyn
projects override it to `netstandard2.0`. The packed content is pinned to `lib/net10.0/...` in
`.config/purview-build.json`, so a framework change has to update that manifest too.

### Layout

| Path | Purpose |
|---|---|
| `src/src/AspireC4/` | Aspire integration library (`Aspire.Hosting.AspireC4`): lifecycle hook, Docker/CLI server resources, model builder, DSL generator, dashboard integration |
| `src/src/SourceGenerators/` | Roslyn `netstandard2.0` incremental source generator (registry + DSL strict validation) |
| `src/src/EntityFrameworkCore/` | AppHost-side EF Core → d2 ER diagram library (`AspireC4.Hosting.EntityFrameworkCore`): descriptor and metadata readers, d2 model builder, d2 DSL generator, sidecar resources, lifecycle hook |
| `src/src/EntityFrameworkCore.Analyzers/` | Roslyn `netstandard2.0` generator packed as the analyzer-only `AspireC4.EntityFrameworkCore` package, referenced by *service* projects |
| `src/src/TestAppHost/` | C# sample AppHost used by integration tests and manual runs |
| `src/src/TestAppHost.SimpleData/` | Sample EF Core model: one `DbContext`, convention-only configuration |
| `src/src/TestAppHost.ComplexData/` | Sample EF Core model: three `DbContext` types, schemas, composite keys, self-references, attribute + fluent configuration |
| `src/tests/AspireC4.UnitTests/` | Unit tests (model builder, DSL generator, options, lifecycle — no Docker) |
| `src/tests/AspireC4.IntegrationTests/` | Aspire lifecycle tests (requires Docker, pulls `ghcr.io/likec4/likec4`) |
| `src/tests/SourceGenerators.UnitTests/` | Source-generator tests (TUnit + `Purview.SourceGeneratorFramework.Testing.TUnit`) |
| `src/tests/EntityFrameworkCore.UnitTests/` | d2 model builder, d2 DSL generator and descriptor loader tests (no Docker) |
| `src/tests/EntityFrameworkCore.Analyzers.UnitTests/` | EF model generator tests, plus the generator→assembly→AppHost round-trip test |
| `src/tests/SharedTestingInfra/` | Shared test helpers for the test projects |
| `samples/typescript-app-host/` | TypeScript AppHost sample (`apphost.mts`, generated `.aspire/modules/`) |
| `assets/likec4-extensions/` | Extra LikeC4 DSL used by the samples |
| `assets/images/` | LikeC4 logos/icons used by the samples |
| `.agents/` | Bundled agent skills/agents/prompts (see [Skills](#skills)) |

### Package READMEs

Every packable project has its own `README.md` beside its `.csproj`, and that is what ships on NuGet:
`Purview.BuildSdk` packs a project's own README in preference to the repository root one. Keep each
one about its own package, and update it when that package's public API or setup changes. Do not
link the root `README.md` into a project with `Link="Sdk/README.md"`, which would override it. Use
absolute URLs in package READMEs, because relative links do not resolve on nuget.org.

### Documentation screenshots

Screenshots embedded in the root `README.md`, the wiki pages under `docs/wiki/` and the package READMEs
live in `docs/images/screenshots/`; its own `README.md` records provenance, consumers and how to refresh
them. Use a relative path from the root README, and absolute `raw.githubusercontent.com` URLs from
`docs/wiki/` (published from a separate repository) and from package READMEs (nuget.org). Skills under
`skills/` install on their own, so they never embed these files — name the diagram's export folder
(`./.aspirec4/images/`) in prose instead.

## Toolchain

| Tool | Purpose | Version source |
|---|---|---|
| .NET SDK | Build/test | `global.json` |
| Target framework | **net10.0** for all shipping and test projects (`src/Directory.Build.props`); Roslyn components override to `netstandard2.0` | `src/Directory.Build.props` |
| Bun | JS scripts, commit hooks, TS sample | `package.json` → `packageManager` |
| just | Task runner (single entry point) | system install |
| Docker | Integration tests, local diagram viewer | system install |
| Lefthook | Git hooks | global install; not pinned in-repo |
| commitlint | Conventional-commit enforcement | `commitlint.config.mts` |

The repository uses `Purview.BuildSdk` (pinned in `global.json` under `msbuild-sdks`) through
`src/Directory.Build.props` / `src/Directory.Build.targets`. The SDK classifies projects, adds references,
sets testing/telemetry defaults, and copies the bundled `.agents/**` skills into this repo. Use the
`project-placement-defaults` and `sdk-project-behavior-and-detection` skills when reasoning about its behaviour.

## Commands

Run everything through `just`. `just` with no arguments lists all recipes.

### Setup

```sh
just init        # dotnet tool restore, dotnet restore, bun install, lefthook install
```

### Build, test, lint

```sh
just build            # Build src/AspireC4.slnx. NOTE: default configuration is Debug (see Justfile config_default)
just build Release    # Build in Release
just test             # Run all tests (unit + integration) via the solution
just test-unit        # Unit tests only
just test-integration # Integration tests only (Docker required)
just lint-check       # CSharpier check (formatting). NOTE: recipe is lint-check, not "lintcheck"
just lint-fix         # CSharpier auto-fix. NOTE: recipe is lint-fix, not "lintfix"
just pack             # Pack NuGet artifacts into artifacts/nuget/
```

### Pipelines (Purview.Build)

The CI/CD pipeline is driven by the `Purview.Build` tool (`pipeline-*` recipes). GitHub workflows are:

- `.github/workflows/pr.yml` — PR build + test against `main`.
- `.github/workflows/release.yml` — push to `main` triggers the release pipeline (`release-mode: NuGet`).

```sh
just pipeline-pr             # PR pipeline (restore, build, lint, tests)
just pipeline-release        # Release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
just pipeline-local-release  # Same but to a local NuGet feed (use forward slashes in paths)
```

There is **no** `just release` recipe and **no** `.github/workflows/cd.yml`; the release workflow is
`.github/workflows/release.yml`.

### Container runtime tests

```sh
just test-e2e-docker        # Integration tests against the host Docker daemon
just test-e2e               # Docker + all local CLI runtimes (npm, pnpm, yarn, bun, deno)
just test-e2e-cli           # All local CLI runtimes only
just test-e2e-npm           # Single CLI runtime (also -pnpm, -yarn, -bun, -deno)
```

### ER diagram viewer

The interactive viewer under `src/src/EntityFrameworkCore/Assets/viewer/` only runs in a browser, so
`just test` proves nothing about it. Drive it with Playwright instead — **never hand-roll a driver**:

```sh
just erd-check             # Drive the viewer in Chromium and assert it renders, lays out and searches
just erd-mermaid-check     # The same for the Mermaid preview
just erd-vendor            # Rebuild the viewer's bundled JavaScript (--embed refreshes the shipped copies)
just erd-svg <input>       # Render an emitted Mermaid .mmd to SVG in a one-shot container
```

`erd-check` defaults to the model at `src/src/TestAppHost/.aspirec4/erd/schema.gen.json`, written by
`dotnet run --project src/src/TestAppHost`. Requires `bunx playwright install chromium` once. The checks
live in `scripts/erd-viewer-check.mts` and `scripts/erd-vendor.mts`; read them, and the viewer under
`src/src/EntityFrameworkCore/Assets/viewer/`, before changing either.

### Other

```sh
just diagrams       # Open the live LikeC4 viewer for this repo
just refresh-icons  # Regenerate the LikeC4 icon manifest (scripts/generate-icon-manifest.mts)
just ts-restore     # aspire restore for the TypeScript sample
just ts-run         # aspire run for the TypeScript sample
just ts-lint        # oxfmt check on apphost.mts
just scrub          # Remove bin/obj, clean, restore, shutdown build server
```

### Running a single test

```sh
dotnet test src/tests/AspireC4.UnitTests/AspireC4.UnitTests.csproj \
  -- --filter "FullyQualifiedName~MyTestMethod"
```

## Non-negotiables

- Always run `just test` (unit + integration) to verify changes. Unit tests alone are not sufficient.
- Never claim a task complete unless all tests pass (`just test` exits green, zero failures).
- Never skip tests (`--filter` overrides, `[Skip]`) without explicit user permission.
- Always run `just lint-check` (or `just lint-fix`) before committing — CSharpier is enforced in CI and by the
  `pre-commit` Lefthook hook.
- Never add `Co-authored-by` trailers to commit messages.
- Never edit files under `.aspire/modules/` in the TypeScript sample; they are regenerated by `aspire restore`.

## Code style

- All C# is formatted with **CSharpier**, pinned to the version in `.config/dotnet-tools.json`. The version is
  managed there (and via `Directory.Packages.props` if applicable), **not** in `.csproj` files.
- Follow the branding rules:
  - `AspireC4` = this library/plugin (e.g. `AspireC4.Hosting`, `AddAspireC4()`, `AspireC4DiagramOptions`).
  - `LikeC4` = the third-party tool (`ghcr.io/likec4/likec4`, `.c4` files, `LikeC4Model`).
  - Never use `LikeC4` to refer to this library, and never use `AspireC4` for the third-party tool.
- Namespaces: the SDK's `NamespacePrefix` is `Aspire.Hosting.AspireC4` (set in `src/Directory.Build.props`
  *before* the SDK import). Public extension methods live in the `Aspire.Hosting` namespace (matching Aspire's
  own namespace) with `#pragma warning disable IDE0130` where required.
- Central package management: all NuGet versions live in the **root** `Directory.Packages.props`. Never add a
  `Version` attribute directly in a `.csproj`; add a `PackageVersion` to `Directory.Packages.props` first.

## Git hooks and commits

Lefthook (`.config/lefthook.yml`) installs two hooks:

- `pre-commit` — runs `just lint-check` (CSharpier over the repo root). Rejects mis-formatted commits.
- `commit-msg` — runs `bunx commitlint --edit` to enforce Conventional Commits.

Commit rules (enforced by `commitlint.config.mts`):

- Format: `<type>(<optional scope>): <subject>`.
- Types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.
- Subject: lower-case, no trailing period, max 100 characters; body lines max 100 characters.
- Breaking changes: `!` after type/scope, or a `BREAKING CHANGE:` footer.
- Bypass temporarily with `git commit --no-verify` only for work-in-progress; never bypass on `main`-bound commits.

## Tests

All tests **must use TUnit** — never xUnit, NUnit, or MSTest. The SDK wires TUnit + TUnit.Mocks + Bogus into test
projects automatically (test projects declare just `<Project Sdk="Microsoft.NET.Sdk" />`). Mocking is done with
**TUnit.Mocks** (`.Returns(...)` API); NSubstitute is not referenced. For source-generator tests, use
`Purview.SourceGeneratorFramework.Testing.TUnit` (see the `source-generator-testing` and `tunit-test-authoring`
skills).

```csharp
[Test]
public async Task Something_Should_DoX()
{
    // Arrange
    // Act
    // Assert
    await Assert.That(result).IsEqualTo(expected);
}

[Before(Test)]
public async Task SetUpAsync() { ... }

[After(Test)]
public async Task TearDownAsync() { ... }

[Before(Class)]
public static async Task ClassSetUpAsync(CancellationToken cancellationToken) { ... }
```

Test authoring requirements (from the source-generator test authoring agent in `.agents/agents/`):

- Use AAA pattern with explicit `// Arrange`, `// Act`, `// Assert` comments.
- Method naming: `{SubjectUnderTest}_{Scenario}_{Expectation}`, `public async Task`.
- One dedicated `{Class}Tests` class per class under test, in the same namespace.
- Build dependencies/SUTs through `CreateXXX` helpers with optional parameters; never instantiate inside a test.
- Pass a `CancellationToken` wherever a downstream method accepts one.

### Integration tests and the project runner

Run `AspireC4.IntegrationTests` through `just test-integration` or `just test`, **not** from an IDE test
runner. Aspire's project-resource runner detects the IDE and fails with
`run session could not be started: IDE returned ... 500 Internal Server Error`, so project resources such as
`data-service` never start. The fixture only waits for the architecture diagram, so the suite still reports
green; the missing ER diagram content is the only signal. The ER tests now fail with the descriptor
diagnostics in that situation, but a broken project runner is still a runner problem, not a product one.

## Source generator work

The generator in `src/src/SourceGenerators/` is built on `Purview.SourceGeneratorFramework`. Follow:

- `.agents/skills/source-generator-codewriter-modernization/SKILL.md` — CodeWriter emission, incremental pipeline
  design, value equality, Roslyn best practices.
- `.agents/skills/source-generator-testing/SKILL.md` and `.agents/skills/tunit-test-authoring/SKILL.md` — test
  runner/base classes, `CodeQuery`, incremental cache tests.
- `.agents/agents/source-generator-framework-writer.agent.md` and `.agents/agents/test-author-writer.agent.md` —
  specialist agents for emitter and test authoring.
- `.agents/prompts/refactor-source-generator-to-codewriter.prompt.md` and
  `.agents/prompts/modernize-test-to-codequery-tunit.prompt.md` — prompt templates.

Diagnostic IDs are allocated across two generators and must stay unique:

| Range | Generator | Declared in | Tracked in |
|---|---|---|---|
| `ASPIREC4001`–`ASPIREC4007` | Registry/DSL strict validator | `src/src/SourceGenerators/Helpers/DiagnosticLibrary.cs` | `src/src/SourceGenerators/AnalyzerReleases.*.md` |
| `ASPIREC4008`–`ASPIREC4010` | EF Core model generator | `src/src/EntityFrameworkCore.Analyzers/Helpers/DiagnosticLibrary.cs` | `src/src/EntityFrameworkCore.Analyzers/AnalyzerReleases.*.md` |

When adding a diagnostic, update the owning generator's release-tracking files.

### EF Core model discovery

The ER diagram draws from three sources, in precedence order.

1. **Build-time dump** (opt-in). `src/src/EntityFrameworkCore.ModelDump/` ships as the dedicated
   `AspireC4.EntityFrameworkCore.ModelDump` package, referenced by the project that owns the EF Core
   provider; its `build/` target runs automatically. It builds each context's real `IModel` through the
   project's `IDesignTimeDbContextFactory` and writes the diagram contract to `obj/aspirec4/ef/`. That
   directory is never copied to `bin/` or `publish/` and is gitignored, so the dump has no deployment
   footprint. `EfModelDumpReader.TryReadGraph` reads it from the project *and every project it
   references* — the provider is usually a referenced persistence project, not the service — via
   `obj/project.assets.json`, and falls back when it is missing or stale. This is the only source that
   sees conventions, value converters and composite foreign keys. Use `IDesignTimeModel`, not
   `DbContext.Model`: the runtime model is read-optimized and drops relational annotations.

2. **Analyzer descriptor** (opt-in). `src/src/EntityFrameworkCore.Analyzers/` runs in the **service**
   project (where the `DbContext` lives) and emits the complete model as
   `[assembly: AspireC4EfModel("<json>")]`; the AppHost reads it back with `System.Reflection.Metadata`.
   The generator must stay pure — `AddSource` only, never writing files, or compiler determinism and
   caching break.

3. **Metadata reader** (default). `src/src/EntityFrameworkCore/EfModelMetadataReader.cs` recovers an
   approximate model from a project's build output with a reflection-only `MetadataLoadContext`, so no
   code in the assembly executes and no project needs the analyzer. `OnModelCreating` is a method body
   and is absent from metadata, so fluent configuration cannot be recovered this way.

Every context carries an `EfModelFidelity`, so an approximate model is never mistaken for a complete
one, and `EfModelLoader.Merge` de-duplicates by fully-qualified type name, preferring the higher
fidelity. `EfColumn.StoreType` is always the real database type; a column's **domain type** — a value
object or an enum — is carried in `EfColumn.DomainTypeName` with its `EfColumn.DomainTypeKind`
(`ValueObject`, `ComplexValueObject`, `Enum`), and shown beside the store type according to
`AspireC4EfDiagramOptions.DomainTypeDisplay` (default `DomainTypeFirst`: `AssetId (guid)`,
`ActorType (int, enum)`). Recognising a scalar value object — a non-generic struct with a public
`Value` member, as the Purview value-object generator emits — is also what makes its foreign keys, and
so its relationships, resolve. A complex value object, marked
`[ValueObject(EFMapping = ComplexType)]`, is flattened into its owner's table. `EfColumnTypeDisplay`
owns the split, and every renderer keeps the database type in its annotation slot when it is not the
headline.

## LikeC4 DSL work

When writing or validating `.c4`/`.likec4` files, load the `likec4-dsl` skill
(`.agents/skills/likec4-dsl/SKILL.md` plus its `references/*.md`) — install it first if it is missing, since
`.agents/skills/` is gitignored. Key contracts:

- Validate with `likec4 validate` (never `check`/`lint`/`build`), prefer
  `likec4 validate --json --no-layout --file <edited-file> <project-dir>`.
- Prefer `--outdir`/`-o` for export output.
- Remember FQN/identifier rules (dots separate FQN hierarchy), scoped-predicate semantics (`*`, `_`, `**`), and
  exact relationship matcher specificity (`kind`, `title`).

## Release process

- Version source of truth: `package.json` `version` (e.g. `13.5.3.1`). The CD workflow
  (`.github/workflows/release.yml`, delegating to the `purview-dev/build` reusable `purview-release.yml` with
  `release-mode: NuGet`) reads and validates it, builds, tests, packs, builds release notes from conventional
  commits, and creates a GitHub Release with the `.nupkg`/`.snupkg`. It does **not** push to nuget.org.
- [Release Flow](docs/wiki/Release-Flow.md) and `.config/purview-build.json` describe the current pipeline —
  keep both in sync when the release process changes. Prefer `just pipeline-release` / `pipeline-local-release`.
- Roslyn diagnostics (`ASPIREC4001`–`ASPIREC4007`) are tracked in
  `src/src/SourceGenerators/AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`. Move unshipped rows into
  a new `## Release <version>` shipped section when cutting a release that adds, removes, or changes a rule. See
  [Release Flow](docs/wiki/Release-Flow.md) for details.

## Skills

Skills installed from other repositories live in `.agents/skills/` (gitignored); `skills-lock.json`
records what is installed. The families:

- `purview-architecture*`, `purview-engineering-principles`, `purview-testing-conventions` — architecture
  and test conventions.
- `purview-buildsdk-*` — Purview.BuildSdk behaviour and configuration.
- `purview-containers-*` — the Purview.Containers packages this library consumes.
- `purview-sourcegenerator-*` — Roslyn generator authoring, packaging and tests.
- `purview-telemetry*` — Purview.Telemetry.SourceGenerator usage (library telemetry).
- `purview-persistence` — the EF Core conventions the ER diagram is drawn from.
- Agents and prompts live in `.agents/agents/` and `.agents/prompts/`.

## Agent skills

Authored skills live in the repository-root `skills/` folder. **Nothing packs them into a NuGet
package** - `Purview.BuildSdk` 1.0.6 removed skill packaging and the build-time `.agents/` mirror
outright. The skills CLI clones the *git* repository and only discovers a root `skills/` folder, so
that is the one delivery route.

`.agents/skills/` is the CLI's install location for skills from **other** repositories, and is
gitignored; `skills-lock.json` is the tracked record of what was installed there. This repository's
own skills are never installed into it - that would leave two copies to drift apart. Read them at
their authored path, `skills/<name>/SKILL.md`.

`.github/skills/` is a third, separate place: skills about working *in* this repository, which would
be meaningless to a consumer who installed the published set. A skill that tells you how to run this
repo's own tooling belongs in `.github/skills/`, never in
`skills/` - `SkillTests` requires everything under `skills/` to carry the `purview-aspirec4` prefix
because those names land in a directory shared with every skill a developer has installed.

```bash
npx skills add purview-dev/aspirec4
```

| Skill | Answers |
| -- | -- |
| `skills/purview-aspirec4-quickstart/` | installing, `AddAspireC4`, what happens on startup, Docker sidecar versus local CLI, and when to add the ER diagram as well |
| `skills/purview-aspirec4-configuration/` | every `AspireC4DiagramOptions` property and its fluent method |
| `skills/purview-aspirec4-resources/` | `WithLikeC4Details`, `WithLikeC4Reference`, `WithLikeC4Group`, and exclusion |
| `skills/purview-aspirec4-entityframework/` | `WithEntityFrameworkDiagram`, the three model-discovery sources, renderers, domain types, and ER image export |
| `skills/purview-aspirec4-validation/` | the `[LikeC4Registry]` class, severity levels, and the `ASPIREC4xxx` diagnostics |

Every skill carries the `purview-aspirec4` prefix. Skills install into one flat directory per agent,
shared with every skill a developer has installed from anywhere, so an unprefixed name collides and
the loser is whichever installed second.

The `.agents/skills/` content under `samples/typescript-app-host/` belongs to that sample and is not
part of this repository's published set.

`src/tests/Repository.UnitTests/SkillTests.cs` validates the authored skills on every test run
through `Purview.Skills.Testing`. The `[InheritsTests]` attribute there is load-bearing - TUnit does
not walk base types during discovery, so without it the run is green and nothing is checked.

A relative link from one `SKILL.md` into another skill's folder is a rule violation: only a skill's
own folder is installed with it. Name the other skill in prose instead.
