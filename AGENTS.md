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
- A `TestAppHost` sample AppHost and a TypeScript AppHost sample.

### Layout

| Path | Purpose |
|---|---|
| `src/src/AspireC4/` | Aspire integration library (`Aspire.Hosting.AspireC4`): lifecycle hook, Docker/CLI server resources, model builder, DSL generator, dashboard integration |
| `src/src/SourceGenerators/` | Roslyn `netstandard2.0` incremental source generator (registry + DSL strict validation) |
| `src/src/TestAppHost/` | C# sample AppHost used by integration tests and manual runs |
| `src/tests/AspireC4.UnitTests/` | Unit tests (model builder, DSL generator, options, lifecycle — no Docker) |
| `src/tests/AspireC4.IntegrationTests/` | Aspire lifecycle tests (requires Docker, pulls `ghcr.io/likec4/likec4`) |
| `src/tests/SourceGenerators.UnitTests/` | Source-generator tests (TUnit + `Purview.SourceGeneratorFramework.Testing.TUnit`) |
| `src/tests/SharedTestingInfra/` | Shared test helpers for the test projects |
| `samples/typescript-app-host/` | TypeScript AppHost sample (`apphost.mts`, generated `.aspire/modules/`) |
| `assets/likec4-extensions/` | Extra LikeC4 DSL used by the samples |
| `assets/images/` | LikeC4 logos/icons used by the samples |
| `.agents/` | Bundled agent skills/agents/prompts (see [Skills](#skills)) |

## Toolchain

| Tool | Purpose | Version source |
|---|---|---|
| .NET SDK | Build/test | `global.json` |
| Bun | JS scripts, commit hooks, TS sample | `package.json` → `packageManager` |
| just | Task runner (single entry point) | system install |
| Docker | Integration tests, local diagram viewer | system install |
| Lefthook | Git hooks | global install; not pinned in-repo |
| commitlint | Conventional-commit enforcement | `commitlint.config.mts` |

The repository uses `Purview.DotNetProjectSdk` (pinned in `global.json` under `msbuild-sdks`) through
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

Diagnostic IDs (`ASPIREC4001`–`ASPIREC4007`) are declared in
`src/src/SourceGenerators/Helpers/DiagnosticLibrary.cs` and tracked in
`src/src/SourceGenerators/AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`. When adding a
diagnostic, update those release-tracking files.

## LikeC4 DSL work

When writing or validating `.c4`/`.likec4` files, load `.agents/skills/likec4-dsl/SKILL.md` and its
`references/*.md`. Key contracts:

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
- Note: `.github/skills/release/SKILL.md` is **out of date** — it references `just release`,
  `scripts/release.mts`, a changeset flow, and `.github/workflows/cd.yml`, none of which exist in this repo.
  Prefer `just pipeline-release` / `pipeline-local-release`.

## Skills

Relevant bundled skills in `.agents/skills/`:

- `likec4-dsl` — LikeC4 DSL/CLI work.
- `project-placement-defaults`, `sdk-configuration-reference`, `sdk-project-behavior-and-detection` —
  Purview.DotNetProjectSdk behaviour and configuration.
- `source-generator-codewriter-modernization`, `source-generator-testing`, `tunit-test-authoring` —
  Roslyn generator work and tests.
- `telemetry-sourcegenerator-*` — Purview.Telemetry.SourceGenerator usage (library telemetry).
- Agents and prompts live in `.agents/agents/` and `.agents/prompts/`.