# Recipes for AspireC4. Run `just` for the list.
#
# The shared recipes come from .build/just/, copied verbatim from the purview-buildsdk-justfile
# skill. Change them there, not here; this file holds what belongs to this repository.

set quiet

# Let a definition here override a module's default variable or no-op hook.
set allow-duplicate-variables
set allow-duplicate-recipes

import ".build/just/index.just"

solution_file := "src/AspireC4.slnx"
artifacts_folder := "./artifacts/nuget"

_typescriptAppHost := "samples/typescript-app-host/"
_goAppHost := "samples/go-app-host/"
_pythonAppHost := "samples/python-app-host/"

[private]
default:
    just --list

# Initialise the repo: restore tools and packages, install JS dependencies, wire up git hooks (safe to re-run)
[group('Utilities')]
init: ensure-tools restore
    bun install
    lefthook install

# ── Tests ─────────────────────────────────────────────────────────────────────
# `test` itself comes from .build/just/dotnet.just. The TypeScript suite runs with it, through
# Repository.UnitTests/TypeScriptTests, so the shared pipeline executes it in the same pass.

# Run unit tests only
[group('Build and Test')]
test-unit *args:
    just test "/*/*/*/*[Category=Unit]" {{ args }}

# Run integration tests only: the C# AppHost on both Aspire lines, then the polyglot AppHosts (Docker required)
[group('Build and Test')]
test-integration configuration=build_configuration *args:
    dotnet test --project src/tests/AspireC4.IntegrationTests --configuration {{ configuration }} {{ args }}
    dotnet test --project src/tests/AspireC4.Aspire13.IntegrationTests --configuration {{ configuration }} {{ args }}
    just test-apphosts {{ configuration }} {{ args }}

# Run the TypeScript, Go and Python sample AppHosts against the shared sample contract. Tests whose
# toolchain is missing skip; set ASPIREC4_REQUIRE_APPHOSTS=true to make that a failure, as CI does.
[doc('Run the TypeScript, Go and Python AppHosts against the shared sample contract')]
[group('Build and Test')]
test-apphosts configuration=build_configuration *args:
    dotnet test --project src/tests/AspireC4.AppHosts.IntegrationTests --configuration {{ configuration }} {{ args }}

# Run all tests: unit, integration and every e2e runtime
[group('Build and Test')]
test-all: test test-integration test-e2e

# ── TypeScript AppHost ────────────────────────────────────────────────────────

# Restore the TypeScript AppHost sample: its Bun dependencies, then its generated bindings
[group('Typescript')]
ts-restore:
    cd {{ _typescriptAppHost }} && bun install
    aspire restore --apphost {{ _typescriptAppHost }}

# Run the TypeScript AppHost sample with Aspire
[group('Typescript')]
ts-run:
    aspire run --apphost {{ _typescriptAppHost }}

# Check the TypeScript AppHost's formatting with oxfmt
[group('Typescript')]
ts-lint:
    cd {{ _typescriptAppHost }} && bun run lint

# Run the TypeScript test suite directly
[group('Typescript')]
ts-test:
    bun test

# ── Go and Python AppHosts ────────────────────────────────────────────────────

# Generate the Go AppHost sample's bindings (.aspire/modules)
[group('Go')]
go-restore:
    aspire restore --apphost {{ _goAppHost }}

# Run the Go AppHost sample with Aspire
[group('Go')]
go-run:
    aspire run --apphost {{ _goAppHost }}

# Check the Go AppHost's formatting and vet it (run go-restore first)
[group('Go')]
go-lint:
    test -z "$(gofmt -l {{ _goAppHost }}apphost.go)" || { gofmt -d {{ _goAppHost }}apphost.go; exit 1; }
    cd {{ _goAppHost }} && go vet ./...

# Python AppHosts are experimental in Aspire 17. The sample enables them in its own aspire.config.json,
# which the CLI reads from the working directory, so these recipes run from inside the sample.

# Generate the Python AppHost sample's bindings and virtual environment
[group('Python')]
py-restore:
    cd {{ _pythonAppHost }} && aspire restore

# Run the Python AppHost sample with Aspire
[group('Python')]
py-run:
    cd {{ _pythonAppHost }} && aspire run

# Check the Python AppHost's formatting and lint it with ruff (through uvx)
[group('Python')]
py-lint:
    cd {{ _pythonAppHost }} && uvx ruff format --check apphost.py && uvx ruff check apphost.py

# ── Polyglot samples ──────────────────────────────────────────────────────────

# Verify every polyglot sample pins the Aspire version the C# AppHost runs on (AspireAppHostVersion)
[group('Samples')]
samples-validate:
    bun scripts/validate-aspire-version.mts

# Rewrite every polyglot sample's Aspire version to match AspireAppHostVersion
[group('Samples')]
samples-validate-fix:
    bun scripts/validate-aspire-version.mts --fix

# ── LikeC4 diagrams ───────────────────────────────────────────────────────────

# Regenerate the LikeC4 icon manifest from the upstream GitHub repository
[group('Diagrams')]
refresh-icons:
    bun scripts/generate-icon-manifest.mts

# View all LikeC4 diagrams in this repository
[group('Diagrams')]
diagrams:
    just _run-likec4 .

[private]
_run-likec4 path=justfile_dir():
    just _try-docker {{ path }} || just _try-bun {{ path }}

[private]
_try-docker path=justfile_dir():
    echo "Using Docker..."
    docker run --rm \
        -v "{{ justfile_directory() }}:/data" \
        --init -t \
        -p 5173:5173 -p 24678:24678 \
        -e CHOKIDAR_USEPOLLING=1 \
        -e CHOKIDAR_INTERVAL=200 \
        ghcr.io/likec4/likec4 serve {{ path }}

[private]
_try-bun path=justfile_dir():
    echo "Docker not available, falling back to Bun..."
    sh -c 'set -- --use-hash-history; if command -v dot >/dev/null 2>&1; then set -- "$@" --use-dot-bin; fi; bunx likec4 serve "{{ path }}" "$@"'

# ── Container runtime tests ───────────────────────────────────────────────────

[private]
_e2e_dockerfile_cli := "tests/Docker/Dockerfile.e2e-cli"

# Run integration tests against the host Docker runtime (Docker Desktop or Rancher Desktop).
# Runs natively on the host so bind-mount paths are real host paths that Docker can resolve.
[doc('Run integration tests against the host Docker runtime')]
[group('Container Tests')]
test-e2e-docker configuration=build_configuration:
    dotnet test \
        --project src/tests/AspireC4.IntegrationTests \
        --configuration {{ configuration }}

# Build and run integration tests with npx as the LikeC4 server (WithLocalCLI Npx)
[group('Container Tests')]
test-e2e-npm configuration=build_configuration: (_e2e-cli-image "aspirec4-e2e-npm" "npm")
    just _e2e-cli-run aspirec4-e2e-npm {{ configuration }}

# Build and run integration tests with pnpm dlx as the LikeC4 server (WithLocalCLI Pnpm)
[group('Container Tests')]
test-e2e-pnpm configuration=build_configuration: (_e2e-cli-image "aspirec4-e2e-pnpm" "pnpm")
    just _e2e-cli-run aspirec4-e2e-pnpm {{ configuration }}

# Build and run integration tests with yarn dlx as the LikeC4 server (WithLocalCLI Yarn)
[group('Container Tests')]
test-e2e-yarn configuration=build_configuration: (_e2e-cli-image "aspirec4-e2e-yarn" "yarn")
    just _e2e-cli-run aspirec4-e2e-yarn {{ configuration }}

# Build and run integration tests with bunx as the LikeC4 server (WithLocalCLI Bun)
[group('Container Tests')]
test-e2e-bun configuration=build_configuration: (_e2e-cli-image "aspirec4-e2e-bun" "bun")
    just _e2e-cli-run aspirec4-e2e-bun {{ configuration }}

# Build and run integration tests with deno as the LikeC4 server (WithLocalCLI Deno)
[group('Container Tests')]
test-e2e-deno configuration=build_configuration: (_e2e-cli-image "aspirec4-e2e-deno" "deno")
    just _e2e-cli-run aspirec4-e2e-deno {{ configuration }}

# Build and run all e2e integration tests: Docker container runtime + all local CLI runtimes
[group('Container Tests')]
test-e2e configuration=build_configuration: (test-e2e-docker configuration) (test-e2e-cli configuration)

# Build and run integration tests for all local CLI runtimes (npm, pnpm, yarn, bun, deno)
[group('Container Tests')]
test-e2e-cli configuration=build_configuration: (test-e2e-npm configuration) (test-e2e-pnpm configuration) (test-e2e-yarn configuration) (test-e2e-bun configuration) (test-e2e-deno configuration)

# Build (or rebuild) a named local-CLI e2e image from the shared Dockerfile.e2e-cli
[private]
_e2e-cli-image image target:
    docker build --target {{ target }} -f {{ _e2e_dockerfile_cli }} -t {{ image }} .

# Run a pre-built local-CLI e2e image.
# Workspace is writable (needed by NuGet restore). Named volumes redirect build artefacts
# and package manager caches so they don't accumulate on the host across runs.
# Lock-file isolation is enforced in the entrypoint (pre-installs run from /tmp).
[private]
_e2e-cli-run image configuration:
    docker run --rm --privileged \
        -v "{{ justfile_directory() }}://workspace" \
        -v aspirec4-nuget-cache://root/.nuget/packages \
        -v aspirec4-testbin://workspace/src/tests/AspireC4.IntegrationTests/bin \
        -v aspirec4-testobj://workspace/src/tests/AspireC4.IntegrationTests/obj \
        -v aspirec4-testhost-bin://workspace/src/src/TestAppHost/bin \
        -v aspirec4-testhost-obj://workspace/src/src/TestAppHost/obj \
        -v aspirec4-nodeapp-modules://workspace/samples/node-app/node_modules \
        -w //workspace \
        {{ image }} \
        dotnet test \
            --project src/tests/AspireC4.IntegrationTests \
            --verbosity normal \
            --configuration {{ configuration }}

# ── ER diagrams ───────────────────────────────────────────────────────────────

# Rebuild the viewer's bundled JavaScript. Pass --embed to refresh the copies shipped with the
# package (after a dependency bump); pass a directory to write plain files into <dir>/vendor.
[doc('Rebuild the ER viewer bundled JavaScript (--embed refreshes the shipped copies)')]
[group('Diagrams')]
erd-vendor target="--embed":
    bun scripts/erd-vendor.mts "{{ target }}"

# Drive the interactive viewer in a real browser and assert it renders, lays out, searches and
# animates. Takes a generated model JSON. Requires: bunx playwright install chromium
[doc('Drive the ER viewer in Chromium and assert it renders, lays out and searches')]
[group('Diagrams')]
erd-check model="src/src/TestAppHost/.aspirec4/erd/schema.gen.json":
    bun scripts/erd-vendor.mts artifacts/erd-viewer-check
    bun scripts/erd-viewer-check.mts "{{ model }}"

# Drive the Mermaid preview in a real browser. Takes an optional .mmd; uses a representative
# diagram when omitted. Requires: bunx playwright install chromium
[doc('Drive the ER Mermaid preview in Chromium')]
[group('Diagrams')]
erd-mermaid-check diagram="":
    bun scripts/erd-vendor.mts artifacts/erd-mermaid-check
    bun scripts/erd-mermaid-check.mts "{{ diagram }}"

# Render an emitted Mermaid .mmd to SVG using mermaid-cli 12 in a one-shot container.
# Animated edges are browser-only and will not appear in this output.
[doc('Render an emitted Mermaid .mmd to SVG in a one-shot container')]
[group('Diagrams')]
erd-svg input output="artifacts/erd/schema.svg":
    # justfile_directory() rather than $(pwd): under Git Bash on Windows the latter resolves to the
    # shell's own install root. MSYS_NO_PATHCONV stops Git Bash rewriting the in-container /work
    # paths into Windows ones before docker ever sees them.
    mkdir -p artifacts/erd
    MSYS_NO_PATHCONV=1 docker run --rm -v "{{ justfile_directory() }}:/work" \
        ghcr.io/mermaid-js/mermaid-cli/mermaid-cli:12.0.0 \
        -i "/work/{{ input }}" -o "/work/{{ output }}"
