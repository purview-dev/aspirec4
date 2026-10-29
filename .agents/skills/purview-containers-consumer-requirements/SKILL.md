---
name: purview-containers-consumer-requirements
description: >-
  Use when a project consuming Purview.Containers will not build - PCC0001 or PCC0002, CS1705 for
  Microsoft.Windows.SDK.NET, CS0234/CS0246 for Microsoft.WSL types, NETSDK1100 on a Linux build agent
  - or when choosing the target framework and platform for a consumer of the WSL Containers backend.
metadata:
  product: Purview.Containers
  repository: purview-dev/containers
---

# Consumer requirements

## Which package requires what

| Package | Target frameworks | Notes |
| --- | --- | --- |
| `Purview.Containers` | `net10.0`, any platform | the umbrella: `Core` plus both backends |
| `Purview.Containers.Core` | `net10.0`, any platform | backend-neutral abstractions |
| `Purview.Containers.<Module>` | `net10.0`, any platform | service modules; restore anywhere, need a backend to run |
| `Purview.Containers.Wsl` | `net10.0` **and** `net10.0-windows10.0.19041.0` | a portable facade plus the Windows implementation. **Everything below is its contract.** |
| `Purview.Containers.Docker` | `net10.0`, any platform | needs a reachable Docker daemon, not a Windows target framework |

A consumer of any backend other than WSL needs only a portable `net10.0` project.

## Most projects need nothing

```xml
<TargetFramework>net10.0</TargetFramework>
```

That binds the portable facade and gets automatic backend selection — WSLC on a Windows host, Docker
everywhere else. It is the recommended choice, and it is why a Windows developer and a Linux CI
runner can share one test project.

A Windows-targeting project binds the implementation directly, with no run-time load:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
  <PlatformTarget>x64</PlatformTarget>                      <!-- or arm64; defaulted for you -->
  <WindowsSdkPackageVersion>10.0.26100.80</WindowsSdkPackageVersion>  <!-- defaulted for you -->
</PropertyGroup>
```

The last two lines are optional — the package ships MSBuild defaults — but stating them makes the
requirement visible to the next person reading the project file, and the defaults only fill in a
value you have not chosen.

## The five requirements

| # | Requirement | Why |
| --- | --- | --- |
| 1 | **.NET 10 or later** | the package ships `lib/net10.0/` (facade) and `lib/net10.0-windows10.0.19041/` (implementation) |
| 2 | **A Windows target framework that names the OS version** — *only* if you want the implementation bound directly | the `Microsoft.WSL.Containers` projection ships Windows assets only |
| 3 | **64-bit** — `PlatformTarget` `x64`/`arm64`, or a matching `RuntimeIdentifier` | the native `wslcsdk.dll` ships for `win-x64` and `win-arm64` only |
| 4 | **`WindowsSdkPackageVersion` ≥ `10.0.26100.80`** | the projection is compiled against `Microsoft.Windows.SDK.NET` `10.0.26100.79`, and the pack the SDK resolves for a `10.0.19041.0` target framework is older |
| 5 | **A Windows 10/11 host with WSL Containers**, to actually run anything | the library drives WSLC; it never installs or updates it for you |

### Always spell out the Windows version

```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>  <!-- correct -->
<TargetFramework>net10.0-windows</TargetFramework>              <!-- fails -->
```

`net10.0-windows` resolves its platform version to **7.0**, older than the `10.0.19041.0` the
implementation was built against, so NuGet selects no compile assets at all. (The
`Microsoft.WSL.Containers` package is itself `net8.0-windows10.0.19041.0`, which a
`net10.0-windows10.0.19041.0` project consumes without issue.)

## What the package does for you

`Purview.Containers.Wsl` ships two `buildTransitive` files, imported by NuGet for **direct and
transitive** references:

| File | Effect |
| --- | --- |
| `…Wsl.props` | defaults `WindowsSdkPackageVersion` to `10.0.26100.80` when unset, and registers the `wsl` backend |
| `…Wsl.targets` | **Windows consumer:** defaults `PlatformTarget` to `x64` when unset or `AnyCPU` and no `RuntimeIdentifier` is selected, and fails with `PCC0001`/`PCC0002` when the target framework or platform cannot be supported. **Platform-neutral consumer:** copies the Windows implementation payload — implementation, WSLC projection, its Windows SDK dependencies, the native SDK — into `wslc/` beside the output on a Windows build host, so the facade can load it at run time. |

An explicit `PlatformTarget`, `RuntimeIdentifier` or `WindowsSdkPackageVersion` in the consumer
always wins.

A **service module** is backend-neutral and brings no backend. Reference the module *and* a backend
package, and use `PURVIEW_CONTAINERS_BACKEND` to pin which one runs.

> Because `buildTransitive` assets are framework-agnostic, NuGet no longer reports `NU1202` for an
> incompatible target framework: restore succeeds and the **guard target** produces the failure
> instead. That is deliberate. `PCC0001` names the required target framework, whereas an empty
> compile-asset set only produces `CS0246` errors in your own code — which blame the wrong thing.

## Error reference

| Error | Raised by | Means | Fix |
| --- | --- | --- | --- |
| `PCC0001` | the shipped guard target | the target framework is neither .NET 10+ on Windows `10.0.19041.0`+ nor a platform-neutral .NET 10+ framework | retarget as above |
| `PCC0002` | the shipped guard target | the consumer is not 64-bit | remove `PlatformTarget`, set it to `x64`/`arm64`, or choose a matching `RuntimeIdentifier` |
| `CS1705` naming `Microsoft.Windows.SDK.NET` | the compiler | the resolved Windows SDK targeting pack is older than the projection's | pin `WindowsSdkPackageVersion` to `10.0.26100.80` |
| `CS0234`/`CS0246` for `Microsoft.WSL`/`Purview` types | the compiler | the package contributed no compile assets — an unsupported target framework, or an unverified escape hatch | fix the target framework; `PCC0001` says the same thing more clearly |
| `NETSDK1100` | the SDK | building a Windows-targeted project on a non-Windows agent | set `EnableWindowsTargeting=true` |

### `EnableWindowsTargeting` on a non-Windows agent

It lets a Linux build agent *compile* a Windows-targeted project. It does **not** make the
implementation runnable there — the facade still reports `wsl` unavailable, and Docker is selected.
Use it to keep one solution building in CI, not as a way to run WSLC off Windows.

## Verifying all of this

The repository proves every row above rather than asserting it:

- `Wsl.UnitTests/ConsumerRequirementsTests.cs` guards the **shape** inside the normal unit run — the
  Windows build targets `.NETCoreApp,Version=v10.0`, targets `Windows10.0.19041.0`, and declares only
  that OS platform. If the target framework drifts, that test fails, and this page, the package
  READMEs and the `buildTransitive` defaults all have to move with it.
- `just verify-consumers` proves the **behaviour**: it packs, then builds throwaway consumer projects
  for every documented outcome, including the guard errors (`PlatformTarget=x86` → `PCC0002`;
  `net8.0-windows` → `PCC0001`; `net11.0-windows` without an OS version → `PCC0001`). It sits outside
  the `[Category=Unit]` filter because it restores from nuget.org.

That split is the point: **a library's own build is not a consumer**, so a claim about consumers is
only true if something builds one.

## Related skills

`purview-containers-quickstart` for packages and backend selection, and
`purview-containers-testing` for the test tiers and CI shape.
