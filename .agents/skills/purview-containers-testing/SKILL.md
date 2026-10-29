---
name: purview-containers-testing
description: >-
  Use when organising or running a test suite that uses Purview.Containers - the Unit and Integration
  tiers, why test modules must run serially under WSL Containers, pinning or skipping a backend,
  running the Docker suites in CI and the WSL suites on a self-hosted runner, and verifying the
  consumer contract.
metadata:
  product: Purview.Containers
  repository: purview-dev/containers
---

# Testing with containers

## Tiers

The SDK stamps every test assembly with a TUnit category derived from the **project name**, so the
tier is a property of the project rather than of a hand-written attribute:

| Project | Category | Runtime needed |
| --- | --- | --- |
| `*.UnitTests` | `Unit` | none — logic, parsing, configuration, wait-strategy units, in-process fakes |
| `Wsl.*` and module `*.IntegrationTests` | `Integration` | a **WSLC host** |
| `Docker.IntegrationTests`, `Modules.DockerIntegrationTests` | `Integration` | any **Docker daemon**; these target `net10.0`, so they run on Linux too |

The shared pipeline filters to `/*/*/*/*[Category=Unit]`, so **the standard pipeline never starts a
container**. The Docker suites are the exception: the PR workflow adds an `integration-docker` job on
`ubuntu-latest`. That job is the standing proof that the library and every service module work off
Windows and WSL — the same modules a developer runs on WSLC locally.

```powershell
just test                                                       # every discovered project
just test '/*/*/*/*[Category=Unit]'                             # unit only
just test '/*/*/*/*[Category=Integration]' --max-parallel-test-modules 1
just test src/tests/Docker.IntegrationTests/Docker.IntegrationTests.csproj
```

Filtering uses TUnit's `--treenode-filter`, not `--filter`: this runs on Microsoft.Testing.Platform,
where `dotnet test --filter` reports "Zero tests ran".

## Why test modules run serially

A WSLC session takes an **exclusive lock on its `storage.vhdx`**, and takes it lazily on first store
access rather than at session start. Running test assemblies in parallel therefore makes the losers
fail:

```text
0x80070020: The process cannot access the file because it is being used by another process.
```

`just test` passes `--max-parallel-test-modules 1` for exactly that reason. The runtime verifies the
store once and transparently falls back to an isolated per-process store, but serial modules keep the
**warm shared image cache** — no per-process re-pull — and are the fastest option.

In Visual Studio, untick **Run Tests in Parallel** (or set *Maximum Parallel Test Projects* to 1)
before running the WSLC integration suites.

## Choosing a backend in a suite

Automatic detection prefers WSLC and falls back to Docker, so a WSLC host runs the WSLC suites and a
Docker-only host runs the Docker ones. Pin one to **fail loudly instead of falling back** —
usually what you want in CI:

```bash
PURVIEW_CONTAINERS_BACKEND=wsl   # or docker
```

Both halves skip themselves when their runtime is unreachable: the Docker suites when no daemon
responds, the WSLC suites when the host lacks the WSL Containers components. That is what lets a
machine without WSLC still run the unit tier — and it is also why a mis-provisioned CI runner reports
*successes with skips* rather than real coverage. Print `wsl --version` and `wslc version` as the
job's first step so that failure is visible immediately.

## CI shape

- **Docker half** — runs on every pull request, on `ubuntu-latest`.
- **WSL half** — cannot: it needs a Windows host with WSL Containers, which no GitHub-hosted runner
  provides. It is a **manual** workflow on a self-hosted runner:

  ```bash
  gh workflow run "Integration (WSL Containers)" --ref main
  gh run watch
  ```

  It runs `Wsl.IntegrationTests` then each service-module suite **one project at a time**, so the
  shared WSLC image store is never contended. Inputs: `ref` and `filter` (a treenode filter).

Self-hosted runner prerequisites: a **Windows x64** runner labelled `self-hosted` and `wslc`, with
WSL Containers installed (`wsl --install --no-distribution`) and the pinned .NET SDK. Tell
`actionlint` about the custom label in `.github/actionlint.yaml`.

> The shared pipeline runs on `ubuntu-latest`, so it builds the portable `net10.0` projects **and**
> the Windows-targeted test projects on Linux. That works only because `src/Directory.Build.props`
> sets `EnableWindowsTargeting=true`; without it the SDK reports `NETSDK1100`.

## Expectations

`Wsl.IntegrationTests` runs many real containers in one session and can take several minutes, because
WSLC serialises container operations. Slow-test warnings while it runs are expected, not a signal.

## Verifying the consumer contract

Two layers, and the split matters.

**Shape, inside the unit run.** `Wsl.UnitTests/ConsumerRequirementsTests.cs` asserts that the Windows
build targets `.NETCoreApp,Version=v10.0`, targets `Windows10.0.19041.0`, and declares only that OS
platform. If the target framework drifts, that test fails — and the consumer-requirements page, the
package READMEs and the shipped `buildTransitive` defaults all have to move with it.

**Behaviour, outside it.** `just verify-consumers` packs the packages and builds throwaway consumer
projects for every documented outcome — the happy paths *and* the documented guard errors. It is
deliberately outside the `[Category=Unit]` filter because it packs and restores from nuget.org:

```powershell
just verify-consumers          # 21 consumer projects, all assertions
just verify-consumers -Keep    # keep the generated projects for inspection
```

This is the general rule worth copying: **a library's own build is not a consumer**, so a claim about
consumers is only true if something builds one.

## Related skills

`purview-containers-quickstart` for the packages and backend selection,
`purview-containers-lifecycle` for readiness and cleanup, and
`purview-containers-consumer-requirements` for the target-framework contract and `PCC` guards.
