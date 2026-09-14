# Contributing to AspireC4.Hosting

Thank you for contributing! This guide covers the tools, conventions, and processes used in this repository.

---

## Table of contents

- [Prerequisites](#prerequisites)
- [Getting started](#getting-started)
- [Branding](#branding)
- [Just — task runner](#just--task-runner)
- [Code style — CSharpier](#code-style--csharpier)
- [Git hooks — Lefthook](#git-hooks--lefthook)
- [Commit messages](#commit-messages)
- [Tests](#tests)
- [Release guide](#release-guide)

---

## Prerequisites

| Tool | Purpose |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) (version from `global.json`) | Build and test |
| [Bun](https://bun.sh/) (version from `package.json` → `packageManager`) | Repository scripts and commit hooks |
| [just](https://just.systems/man/en/packages.html) | Task runner |
| [Lefthook](https://github.com/evilmartians/lefthook) | Git hooks |
| [Docker](https://www.docker.com/) | Integration tests, local diagram viewer |

After cloning, install all dependencies:

```sh
just init         # JS dependencies (Bun), NuGet packages, local tools, and Git hooks
```

See [Git hooks — Lefthook](#git-hooks--lefthook) for hook configuration.

---

## Getting started

```sh
just build        # Build the solution (Debug by default; pass Release to build Release)
just test         # Run all tests (unit + integration)
just lint-check   # Check formatting
```

---

## Branding

Two distinct brands exist in this repository. Use them consistently:

| Brand | What it is | Examples |
|---|---|---|
| **AspireC4** | This library / plugin | `AspireC4.Hosting` NuGet package, `AspireC4DiagramOptions`, `IAspireC4Builder`, `AddAspireC4()` |
| **LikeC4** | The third-party visualisation tool this library integrates | `ghcr.io/likec4/likec4` container, `LikeC4Model`, `LikeC4DslGenerator`, `.c4` file format |

**Rules:**

- Public extension methods and user-facing types use the `AspireC4` prefix.
- Types that directly represent LikeC4 DSL concepts keep the `LikeC4` prefix.
- Never use `LikeC4` to refer to this library, and never use `AspireC4` to refer to the third-party tool.

---

## Just — task runner

`just` is the single entry point for all development tasks. Run `just` with no arguments to list all recipes.

### .NET

| Recipe | Description |
|---|---|
| `just restore` | Restore NuGet packages and local .NET tools |
| `just build [Debug\|Release]` | Build the solution (default: `Debug`) |
| `just clean` | Clean build outputs |
| `just test` | **Run all tests** (unit + integration) |
| `just test-unit` | Run unit tests only |
| `just test-integration` | Run integration tests only |
| `just lint-check` | Check formatting with CSharpier |
| `just lint-fix` | Auto-fix formatting with CSharpier |
| `just pack` | Build and pack NuGet artifacts into `artifacts/nuget/` |

### Container runtime tests (local only)

| Recipe | Description |
|---|---|
| `just test-e2e-docker` | Integration tests against the host Docker daemon |
| `just test-e2e` | Docker + all local CLI runtimes (npm, pnpm, yarn, bun, deno) |
| `just test-e2e-cli` | All local CLI runtimes only (npm, pnpm, yarn, bun, deno) |
| `just test-e2e-npm` | Single CLI runtime (also `-pnpm`, `-yarn`, `-bun`, `-deno`) |

### Diagrams

| Recipe | Description |
|---|---|
| `just diagrams` | Open the live LikeC4 diagram viewer for this repository |

### Filtering a single test

```sh
dotnet test src/tests/AspireC4.UnitTests/AspireC4.UnitTests.csproj \
  -- --filter "FullyQualifiedName~MyTestMethod"

dotnet test src/tests/AspireC4.IntegrationTests/AspireC4.IntegrationTests.csproj \
  -- --filter "FullyQualifiedName~MyTestMethod"
```

---

## Code style — CSharpier

All C# code is formatted with [CSharpier](https://csharpier.com/), pinned to the version in `.config/dotnet-tools.json`. It is installed as a local .NET tool via `just restore`.

```sh
just lint-check   # Report formatting violations
just lint-fix     # Auto-fix formatting violations
```

CSharpier runs automatically on every `git commit` via Lefthook. Commits with formatting violations are rejected. Always run `just lint-fix` before committing if you have unsaved format changes, or configure your editor to format on save using the CSharpier extension.

**Do not pin a specific CSharpier version in `.csproj` files.** The version lives exclusively in `.config/dotnet-tools.json`.

---

## Git hooks — Lefthook

[Lefthook](https://github.com/evilmartians/lefthook) manages two hooks, configured in `.config/lefthook.yml`:

| Hook | What it does |
|---|---|
| `pre-commit` | Runs `just lint-check` (CSharpier over the repo root). Rejects the commit if any file is mis-formatted. |
| `commit-msg` | Runs `commitlint` to enforce [conventional commit](#commit-messages) format. |

Lefthook installs when you run `just init`. To verify it is active:

```sh
bunx lefthook install
```

If you need to bypass a hook temporarily (e.g., a work-in-progress commit you will amend):

```sh
git commit --no-verify -m "wip: ..."
```

Do not bypass hooks on commits intended for `main`.

---

## Commit messages

Commit messages must follow [Conventional Commits](https://www.conventionalcommits.org/) and are enforced by `commitlint` (via Lefthook). Rules are in `commitlint.config.mts`.

**Format:**

```
<type>(<optional scope>): <subject>

<optional body>

<optional footer>
```

**Allowed types:**

| Type | When to use |
|---|---|
| `feat` | New user-facing feature |
| `fix` | Bug fix |
| `refactor` | Code restructuring with no behaviour change |
| `perf` | Performance improvement |
| `test` | Adding or fixing tests |
| `docs` | Documentation only |
| `ci` | CI/CD workflow changes |
| `build` | Build system changes |
| `chore` | Maintenance, tooling, dependency updates |
| `style` | Formatting, whitespace (code not logic) |
| `revert` | Reverting a previous commit |

**Rules:**

- Subject must be lower-case, no trailing period, max 100 characters.
- Body lines max 100 characters.
- Breaking changes: append `!` after the type/scope, or add `BREAKING CHANGE:` in the footer.

```sh
# Good
feat(core): add image alias resolution for azure resources
fix: correct hmr port fallback on windows
chore(deps): bump aspire.hosting to 9.2.0

# Bad — upper-case subject, trailing period
Fix: Correct HMR port fallback on Windows.
```

---

## Tests

All tests in this repository **must use [TUnit](https://github.com/thomhurst/TUnit)**. Do not use xUnit, NUnit, or MSTest. Test projects declare just `<Project Sdk="Microsoft.NET.Sdk" />`; the SDK (Purview.DotNetProjectSdk) wires TUnit, TUnit.Mocks, and Bogus into them automatically.

### Key patterns

```csharp
// Test method
[Test]
public async Task Something_Should_DoX()
{
    var result = ComputeSomething();

    await Assert.That(result).IsEqualTo(expected);
}

// Setup / teardown
[Before(Test)]
public async Task SetUpAsync() { ... }

[After(Test)]
public async Task TearDownAsync() { ... }

// Shared class-level setup (used by integration tests)
[Before(Class)]
public static async Task ClassSetUpAsync(CancellationToken cancellationToken) { ... }

[After(Class)]
public static async Task ClassTearDownAsync(CancellationToken cancellationToken) { ... }
```

### Mocking

Use [TUnit.Mocks](https://github.com/thomhurst/TUnit) for mocking. Also wired in automatically by the SDK. Mock an
interface or type via its `.Mock()` extension and configure members with `.Returns(...)`:

```csharp
var config = IConfiguration.Mock();
config.Item("AppHost:BrowserToken").Returns("value");
```

### Project structure

| Project | What to test here |
|---|---|
| `AspireC4.UnitTests` | `LikeC4ModelBuilder`, `LikeC4DslGenerator`, annotations, options — no Docker required |
| `AspireC4.IntegrationTests` | Full Aspire lifecycle: container startup, file generation, endpoint availability |

Integration tests require Docker to be running. They pull `ghcr.io/likec4/likec4` on first run.

### CI behaviour

- Pull requests run the `PR` workflow (`.github/workflows/pr.yml`), which delegates to the `purview-dev/build`
  reusable `purview-build.yml` to build and test the solution.
- A push to `main` triggers the release workflow — see [Release guide](#release-guide).

---

## Release guide

The version in `package.json` is maintained manually and is the sole version source used by the release pipeline. Versions must use valid SemVer, including an optional prerelease suffix when required.

### Preparing a release

1. Choose an unused version and update `package.json`.
2. Use conventional commit subjects for noteworthy changes:
   - `feat:` for features
   - `fix:` for bug fixes
   - `perf:`, `refactor:`, or `revert:` for other noteworthy changes
3. Commit the version update and merge or push it to `main`.

Commits beginning with `chore:`, `build:`, `ci:`, `test:`, `docs:`, or `style:` are intentionally omitted from release notes. When no noteworthy commits exist, the release notes contain "Improvements ongoing."

### Running the release pipeline

The pipeline is driven by the `Purview.Build` tool through `just`:

```sh
just pipeline-release        # restore, build, lint, tests, pack, publish, GitHub release
just pipeline-local-release  # Same but to a local NuGet feed (use forward slashes in paths)
```

### CI release workflow

A push to `main` triggers `.github/workflows/release.yml`, which delegates to the `purview-dev/build` reusable `purview-release.yml` with `release-mode: NuGet`. It:

1. Reads and validates the version from `package.json`.
2. Builds the solution and runs unit and integration tests.
3. Packs the NuGet package using the exact manual version.
4. Builds release notes from noteworthy commits since the previous release.
5. Creates a GitHub Release with the `.nupkg` and `.snupkg` files attached.

The workflow does not publish to NuGet. Download the package from GitHub Releases and push it to the desired feed manually.
