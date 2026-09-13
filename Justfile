set quiet

_root := "./"
_solution := "src/AspireC4.slnx"
_typescriptAppHost := "samples/typescript-app-host/"

config_default := "Debug"

pipeline_feed := "https://api.nuget.org/v3/index.json"
pipeline_tool := ".tools/purview-build/purview-build"

# List available recipes
[private]
default:
    just --list

# Install the shared Purview.Build tool (authenticated to the Purview-Dev feed) if not present
[private]
ensure-pipeline-tool:
    if [ ! -x "{{ pipeline_tool }}" ]; then \
        dotnet tool install Purview.Build --tool-path .tools/purview-build --add-source "{{ pipeline_feed }}"; \
    fi

# Run the PR pipeline (restore, build, lint, tests)
[group('Pipeline')]
pipeline-pr *args:
    just ensure-pipeline-tool
    echo "Running PR pipeline..."
    "{{ pipeline_tool }}" {{ args }}

# Run the build pipeline (restore, build, lint)
[group('Pipeline')]
pipeline-build *args:
    just ensure-pipeline-tool
    echo "Running build pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=false --Release:Mode=None {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
[group('Pipeline')]
pipeline-release *args:
    just ensure-pipeline-tool
    echo "Running release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=NuGet {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, local nuget publish)
# Note: `just` runs recipes through the shell, which strips backslashes from unquoted arguments.
# Use the LOCAL_NUGET_FEED_PATH environment variable or forward slashes, e.g.
# just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
[group('Pipeline')]
pipeline-local-release *args:
    just ensure-pipeline-tool
    just lint-fix
    echo "Running local release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=LocalNuGet {{ args }}

# Run the pipeline with tests enabled
[group('Pipeline')]
pipeline-tests *args:
    just ensure-pipeline-tool
    echo "Running tests pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=true --Release:Mode=None {{ args }}

# Initialise the repo: restore packages, install tools, and wire up git hooks.
# Safe to run multiple times.
[group('Utilities')]
init: restore
    bun install
    lefthook install

# Open the solution in the default IDE (e.g., Visual Studio or VS Code)
[group('Utilities')]
vs:
    open {{ _solution }}

# Clean up the repository by removing build artifacts, bin/obj folders etc, and shutting down the build server
[group('Utilities')]
scrub:
    find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
    just clean
    just restore --force-evaluate
    dotnet build-server shutdown

# Run all tests (unit + integration + e2e )
test-all: test test-integration test-e2e

# ── .NET ──────────────────────────────────────────────────────────────────────
# Restore NuGet packages and local tools
[group('.NET')]
restore *args:
    dotnet tool restore
    dotnet restore {{ _solution }} {{ args }}

# Build the entire solution
[group('.NET')]
build configuration=config_default *args:
    dotnet build {{ _solution }} --no-restore --configuration {{ configuration }} {{ args }}

# Cleans the solution
[group('.NET')]
clean configuration=config_default *args:
    dotnet clean {{ _solution }} --configuration {{ configuration }} {{ args }}

# Run all tests (unit + integration)
[group('.NET')]
test configuration=config_default *args:
    dotnet test --solution {{ _solution }} --configuration {{ configuration }} {{ args }}

# Run unit tests only
[group('.NET')]
test-unit configuration=config_default *args:
    dotnet test --project src/tests/AspireC4.UnitTests --configuration {{ configuration }} {{ args }}

# Run integration tests only
[group('.NET')]
test-integration configuration=config_default *args:
    dotnet test --project src/tests/AspireC4.IntegrationTests --configuration {{ configuration }} {{ args }}

# Run C# linting (CSharpier check)
[group('.NET')]
lint-check:
    dotnet csharpier check {{ _root }}

# Run C# linting and auto-fix (CSharpier format)
[group('.NET')]
lint-fix:
    dotnet csharpier format {{ _root }}

# Build and produce NuGet packages into artifacts/nuget (version read from package.json)
[group('.NET')]
pack configuration=config_default *args: (build configuration)
    dotnet pack {{ _solution }} --configuration {{ configuration }} --output artifacts/nuget "-p:Version=$(bun -p "require('./package.json').version")" {{ args }}

# ── TypeScript AppHost --------------------------------------------------------

# Restore dependencies for the TypeScript AppHost sample
[group('Typescript')]
ts-restore:
    aspire restore --apphost {{ _typescriptAppHost }}

# Run the TypeScript AppHost sample with Aspire
[group('Typescript')]
ts-run:
    aspire run --apphost {{ _typescriptAppHost }}

[group('Typescript')]
ts-lint:
    cd {{ _typescriptAppHost }} && bun run lint

# ── Icon manifest ─────────────────────────────────────────────────────────────

# Regenerate the LikeC4 icon manifest from the upstream GitHub repository
[group('.NET')]
refresh-icons:
    bun scripts/generate-icon-manifest.mts

# ── LikeC4 diagram viewer ─────────────────────────────────────────────────────

# View all LikeC4 diagrams in this repository
[group('Diagrams')]
diagrams:
    just _run-likec4 .

# ── Container runtime tests ───────────────────────────────────────────────────

[private]
_e2e_dockerfile_cli := "tests/Docker/Dockerfile.e2e-cli"

# Run integration tests against the host Docker runtime (Docker Desktop or Rancher Desktop).
# Runs natively on the host so bind-mount paths are real host paths that Docker can resolve.
[group('Container Tests')]
test-e2e-docker configuration=config_default:
    dotnet test \
        --project src/tests/AspireC4.IntegrationTests \
        --configuration {{ configuration }}

# Build and run integration tests with npx as the LikeC4 server (WithLocalCLI Npx)
[group('Container Tests')]
test-e2e-npm configuration=config_default: (_e2e-cli-image "aspirec4-e2e-npm" "npm")
    just _e2e-cli-run aspirec4-e2e-npm {{ configuration }}

# Build and run integration tests with pnpm dlx as the LikeC4 server (WithLocalCLI Pnpm)
[group('Container Tests')]
test-e2e-pnpm configuration=config_default: (_e2e-cli-image "aspirec4-e2e-pnpm" "pnpm")
    just _e2e-cli-run aspirec4-e2e-pnpm {{ configuration }}

# Build and run integration tests with yarn dlx as the LikeC4 server (WithLocalCLI Yarn)
[group('Container Tests')]
test-e2e-yarn configuration=config_default: (_e2e-cli-image "aspirec4-e2e-yarn" "yarn")
    just _e2e-cli-run aspirec4-e2e-yarn {{ configuration }}

# Build and run integration tests with bunx as the LikeC4 server (WithLocalCLI Bun)
[group('Container Tests')]
test-e2e-bun configuration=config_default: (_e2e-cli-image "aspirec4-e2e-bun" "bun")
    just _e2e-cli-run aspirec4-e2e-bun {{ configuration }}

# Build and run integration tests with deno as the LikeC4 server (WithLocalCLI Deno)
[group('Container Tests')]
test-e2e-deno configuration=config_default: (_e2e-cli-image "aspirec4-e2e-deno" "deno")
    just _e2e-cli-run aspirec4-e2e-deno {{ configuration }}

# Build and run all e2e integration tests: Docker container runtime + all local CLI runtimes
[group('Container Tests')]
test-e2e configuration=config_default: (test-e2e-docker configuration) (test-e2e-cli configuration)

# Build and run integration tests for all local CLI runtimes (npm, pnpm, yarn, bun, deno)
[group('Container Tests')]
test-e2e-cli configuration=config_default: (test-e2e-npm configuration) (test-e2e-pnpm configuration) (test-e2e-yarn configuration) (test-e2e-bun configuration) (test-e2e-deno configuration)

# Build (or rebuild) a named e2e test runner image from its Dockerfile
[private]
_e2e-image image dockerfile:
    docker build -f {{ dockerfile }} -t {{ image }} .

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
        -v aspirec4-testhost-bin://workspace/src/src/AspireC4.TestAppHost/bin \
        -v aspirec4-testhost-obj://workspace/src/src/AspireC4.TestAppHost/obj \
        -v aspirec4-nodeapp-modules://workspace/samples/node-app/node_modules \
        -w //workspace \
        {{ image }} \
        dotnet test \
            --project src/tests/AspireC4.IntegrationTests \
            --verbosity normal \
            --configuration {{ configuration }}
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
