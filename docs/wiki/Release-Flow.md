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

## See also

- [Contributing](Contributing.md)