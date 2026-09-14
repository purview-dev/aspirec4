---
name: release
description: Creates a release for AspireC4. Use this skill when asked to create a release, publish a new version, bump the version, or run the release process.
---

## AspireC4 Release Process

There is **no** `just release` recipe, no `scripts/release.mts`, and no changeset flow. Releases are driven by the
`Purview.Build` pipeline (`just pipeline-*` recipes) and the GitHub workflow `.github/workflows/release.yml`.

### Version source of truth

- `package.json` `version` (e.g. `13.5.3.1`) is the sole version source. Maintain it manually with valid SemVer.
- Never add a `Version` attribute directly in a `.csproj`; NuGet versions are centrally managed in the root
  `Directory.Packages.props`.

### Preparing a release

1. Choose an unused version and update the `version` field in `package.json`.
2. Use conventional commit subjects for changes you want in the release notes (`feat:`, `fix:`, `perf:`,
   `refactor:`, `revert:`). Commits beginning with `chore:`, `build:`, `ci:`, `test:`, `docs:`, or `style:` are
   omitted from release notes.
3. Commit the version update and push it to `main`.

### Running the release pipeline

The pipeline requires the `Purview.Build` tool, installed on demand into `.tools/purview-build` by
`just ensure-pipeline-tool`:

```sh
just pipeline-release        # restore, build, lint, tests, pack, publish, GitHub release
just pipeline-local-release  # Same but to a local NuGet feed (use forward slashes in paths)
```

`pipeline-local-release` also runs `just lint-fix` first. When passing a local feed path, use forward slashes,
e.g.:

```sh
just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
```

### CI release workflow

A push to `main` triggers `.github/workflows/release.yml`, which delegates to the `purview-dev/build` reusable
`purview-release.yml` with `release-mode: NuGet`. It:

1. Reads and validates the version from `package.json`.
2. Builds the solution and runs unit and integration tests.
3. Packs the NuGet packages using the exact manual version.
4. Builds release notes from conventional commits since the previous release.
5. Creates a GitHub Release with the `.nupkg`/`.snupkg` files attached.

The workflow does **not** push to nuget.org. Download the package from GitHub Releases and push it to the desired
feed manually.

### Troubleshooting

- `just pipeline-*` requires the pipeline tool; `ensure-pipeline-tool` installs it automatically.
- For a dry run against the current tree, use `just pipeline-pr` (restore, build, lint, tests — no pack/publish).