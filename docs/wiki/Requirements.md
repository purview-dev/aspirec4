# Requirements

This page is the **single source** for what an application needs in order to use AspireC4. Every other
page — the README, [Getting Started](Getting-Started.md), the package READMEs on NuGet and the agent
skills — summarises it and links here. How *this repository* is built (SDK pin, C# 15 settings, the .NET 11
features adopted and avoided) is a separate concern, recorded in
[Toolchain and support matrix](Toolchain.md).

## At a glance

| Requirement | Minimum | Needed by | Why |
| --- | --- | --- | --- |
| .NET SDK | **.NET 11 SDK** (`11.0.100` or later) | every machine and IDE that **builds** a project referencing `AspireC4.Hosting` or `AspireC4.EntityFrameworkCore` | the bundled source generators are compiled against Roslyn 5.9 — see [the compiler floor](#the-compiler-floor-net-11-sdk) |
| Runtime / target framework | **.NET 10** (`net10.0`) or **.NET 11** (`net11.0`) | the AppHost | the libraries ship a `net10.0` and a `net11.0` asset |
| Aspire | **13.6.5** | the AppHost: `Aspire.AppHost.Sdk` **and** a `PackageReference` to `Aspire.Hosting.AppHost` | `AspireC4.Hosting` depends on `Aspire.Hosting` 13.6.5, so NuGet will not resolve an older Aspire |
| Container runtime | Docker or Podman | the default LikeC4 sidecar, the ER viewer sidecar and image export | optional — see [Without a container runtime](#without-a-container-runtime) |
| Node CLI runtime | `npx`, `pnpm`, `yarn`, `bun` or `deno` on `PATH` | only `.WithLocalCLI()` | runs the LikeC4 server without Docker |
| Runtime on the build machine | .NET 10 or later — the .NET 11 SDK's runtime is enough | only the optional `AspireC4.EntityFrameworkCore.ModelDump` tool | it rolls forward to the latest major; see [The model dump tool](#the-model-dump-tool) |

**.NET 8 and .NET 9 are not supported.** Nothing ships for them.

## The compiler floor: .NET 11 SDK

The SDK requirement and the target-framework requirement are different things, and it is normal to need
the .NET 11 SDK while the AppHost still targets `net10.0`.

`AspireC4.Hosting` ships the registry/DSL validation generator under `analyzers/dotnet/cs/` — it is
included in that package automatically, so there is no generator package to reference — and
`AspireC4.EntityFrameworkCore` is that kind of package in its entirety. A Roslyn component runs **inside
the compiler**, so what it needs is a compiler version, not a target framework: both are compiled against
`Microsoft.CodeAnalysis` **5.9**, which first ships in the .NET 11 SDK. The project's `TargetFramework` has
no bearing on it.

Building with an older compiler — a .NET 10 SDK, or an IDE whose bundled compiler predates Roslyn 5.9 —
does not fail cleanly. The compiler reports `CS9057` (*the analyzer assembly references a newer version of
the compiler*) and skips the generator:

| Package | What is lost on an older compiler |
| --- | --- |
| `AspireC4.Hosting` | the `ASPIREC4001`–`ASPIREC4007` validation, **and** the injected `[LikeC4Registry]`, `[KnownType]`, `[Severity]`, `LikeC4RegistryType` and `LikeC4Severity` types — code using them stops compiling (`CS0246`) |
| `AspireC4.EntityFrameworkCore` | the `[assembly: AspireC4EfModel]` descriptor; the ER diagram falls back to the `Approximate` metadata read and loses fluent configuration |

Pin it in the consuming repository's `global.json` so every machine and CI agent agrees:

```json
{
  "sdk": {
    "version": "11.0.100",
    "rollForward": "latestFeature"
  }
}
```

TypeScript AppHosts are not affected: they do not compile C#, so they never load the generator.

## Target frameworks

| Package | Install into | Target frameworks | Shipped as |
| --- | --- | --- | --- |
| `AspireC4.Hosting` | AppHost | `net10.0`, `net11.0` | `lib/net10.0/`, `lib/net11.0/` and the generator under `analyzers/dotnet/cs/` |
| `AspireC4.Hosting.EntityFrameworkCore` | AppHost | `net10.0`, `net11.0` | `lib/net10.0/`, `lib/net11.0/` |
| `AspireC4.EntityFrameworkCore` | the project declaring a `DbContext` | `netstandard2.0` | `analyzers/dotnet/cs/` only — no runtime assembly |
| `AspireC4.EntityFrameworkCore.ModelDump` | the project owning the EF Core provider | `net10.0` | `tools/` — a build-time tool run by MSBuild |

`net10.0` is the LTS and the framework an Aspire AppHost targets, so that is the asset a .NET 10 AppHost
resolves; an AppHost that has moved to .NET 11 resolves the `net11.0` asset with no package change. Both
are built from the same source.

## Aspire

The minimum is **Aspire 13.6.5**, because that is the `Aspire.Hosting` version `AspireC4.Hosting` declares
and NuGet resolves a dependency graph to the highest minimum in it. An AppHost on an older Aspire is pulled
up to 13.6.5 by the package, so match the AppHost SDK to it:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.6.5">
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="AspireC4.Hosting" />
  </ItemGroup>
</Project>
```

The explicit `Aspire.Hosting.AppHost` reference is required; relying on the AppHost SDK alone produces
`ASPIRE002`. A TypeScript AppHost states the same version as `sdk.version` and in its `Aspire.Hosting.*`
pins in `aspire.config.json` — see [TypeScript AppHosts](Typescript-AppHost.md).

## Without a container runtime

Docker (or Podman) is the default, not a hard requirement:

- the architecture diagram can run through a local Node CLI with `.WithLocalCLI()` — see
  [Local CLI Runtimes](Local-CLI.md);
- the ER diagram can skip its viewer sidecar and image export — see
  [Entity Framework Core ER Diagrams](Entity-Framework-Diagrams.md).

Image export verifies bind sharing through `Purview.Containers.Environment` with a small probe image
(`alpine:3.19` by default). An offline machine must preload it or set `CONTAINER_ENVIRONMENT_PROBE_IMAGE`.

## The model dump tool

`AspireC4.EntityFrameworkCore.ModelDump` runs after `Build` as `dotnet <tool>.dll`. The tool targets
`net10.0` and rolls forward to the **latest installed major runtime** (`RollForward=LatestMajor`), so
the .NET 11 SDK's own runtime is enough. No separate .NET 10 runtime is needed.

It rolls to the *latest* major rather than only when .NET 10 is missing, because the tool loads the
project's built assembly. A project that builds `net11.0` can only be loaded by a .NET 11 (or later)
process, even on a machine that also has .NET 10 installed. Prerelease runtimes are not chosen unless
`DOTNET_ROLL_FORWARD_TO_PRERELEASE=1` is set.

When the tool cannot run, the build still succeeds (the target is `ContinueOnError`), no dump is written,
and the AppHost falls back to the analyzer descriptor or the metadata read — the ER diagram log names the
missing dump.

## Keeping this page true

This page owns the facts above; other pages may summarise them but must link here rather than restate
the detail. When a requirement changes, update this page first, then the summaries that repeat a number:

- `Aspire.Hosting` floor — `AspireVersion` in `Directory.Packages.props`; the AppHost snippets in
  [Getting Started](Getting-Started.md), `README.md`, `src/src/AspireC4/README.md` and the
  `purview-aspirec4-quickstart` skill.
- Compiler floor — `RoslynCompilerVersion` in `Directory.Packages.props`.
- Target frameworks — Purview.BuildSdk's set; see [Toolchain and support matrix](Toolchain.md#keeping-this-page-true).
- The model dump runtime — `TargetFramework` and `RollForward` in
  `src/src/EntityFrameworkCore.ModelDump/EntityFrameworkCore.ModelDump.csproj`.
