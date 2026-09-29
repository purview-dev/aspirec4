# Release Flow

The version in `package.json` is maintained manually and is the sole version source used by the release pipeline. Versions must use valid SemVer, including an optional prerelease suffix when required.

## Preparing a release

1. Choose an unused version and update `package.json`.
2. Use conventional commit subjects for noteworthy changes:
   - `feat:` for features
   - `fix:` for bug fixes
   - `perf:`, `refactor:`, or `revert:` for other noteworthy changes
3. Commit the version update and merge or push it to `main`.

Commits beginning with `chore:`, `build:`, `ci:`, `test:`, `docs:`, or `style:` are intentionally omitted from release notes. When no noteworthy commits exist, the release notes contain "Improvements ongoing."

## Running the release pipeline

The pipeline is driven by the `Purview.Build` tool through `just`:

```sh
just pipeline-release        # restore, build, lint, tests, pack, publish, GitHub release
just pipeline-local-release  # Same but to a local NuGet feed (use forward slashes in paths)
```

## CI release workflow

A push to `main` triggers `.github/workflows/release.yml`, which delegates to the `purview-dev/build` reusable `purview-release.yml` with `release-mode: NuGet`. It:

1. Reads and validates the version from `package.json`.
2. Builds the solution and runs unit and integration tests.
3. Packs the NuGet package using the exact manual version.
4. Builds release notes from noteworthy commits since the previous release.
5. Creates a GitHub Release with the `.nupkg` and `.snupkg` files attached.

The workflow does not publish to NuGet. Download the package from GitHub Releases and push it to the desired feed manually.

## Analyzer release tracking

The source generator ships Roslyn diagnostics (`ASPIREC4001`–`ASPIREC4007`). Their release history is tracked with
the standard `Microsoft.CodeAnalysis.Analyzers` release-tracking files:

- `src/src/SourceGenerators/AnalyzerReleases.Shipped.md` — rules that have already shipped, grouped under a
  `## Release <version>` heading.
- `src/src/SourceGenerators/AnalyzerReleases.Unshipped.md` — rules added, changed, or removed since the last release.
  This file starts empty at the beginning of every release.

Use these exact file names, at the `SourceGenerators` project root. `Microsoft.CodeAnalysis.Analyzers` only
auto-includes files called `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` as compiler
`AdditionalFiles`; any other name (for example `Analysis.Shipped.md`) is silently ignored and no tracking occurs.

### During a release

1. Add any new, changed, or removed diagnostics to `AnalyzerReleases.Unshipped.md` as they are introduced.
2. When the release is cut, move every row from `AnalyzerReleases.Unshipped.md` into a new
   `## Release <version>` section in `AnalyzerReleases.Shipped.md` and leave the unshipped file empty again.
3. If the release adds, removes, or changes no diagnostics, do not add a shipped section — the unshipped file
   simply stays empty.

Only three descriptor attributes count as a "change": category, default severity, and enabled-by-default status.
Message text and descriptions can change without a tracking entry.

Build the generator project to verify the tracking files remain valid and complete:

```sh
dotnet build src/src/SourceGenerators/SourceGenerators.csproj -c Release
```

The build must produce no `RS2000`–`RS2008` diagnostics.

## See also

- [Contributing](Contributing.md)