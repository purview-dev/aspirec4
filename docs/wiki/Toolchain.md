# Toolchain and support matrix

This page records which .NET versions AspireC4 supports, how the packages are built, and which .NET 11
features are deliberately used or deliberately not used. It is the reference for "can I move this
project to .NET 11?" questions; the consumer-facing requirements are repeated in
[Getting Started](Getting-Started.md).

## Support matrix

**.NET 8 and .NET 9 are not supported.** They were removed from the build, the tests, the packages and
the container test images; nothing in this repository targets them.

| Component | Targets | Shipped as | Notes |
| --- | --- | --- | --- |
| `AspireC4.Hosting` | `net10.0`, `net11.0` | `lib/net10.0/`, `lib/net11.0/` | the AppHost assembly; NuGet picks the asset that matches the consumer |
| `AspireC4.Hosting.EntityFrameworkCore` | `net10.0`, `net11.0` | `lib/net10.0/`, `lib/net11.0/` | the ER diagram assembly |
| `AspireC4.EntityFrameworkCore` | `netstandard2.0` | `analyzers/dotnet/cs/` | analyzer only, no runtime assembly |
| `AspireC4.EntityFrameworkCore.ModelDump` | `net10.0` | `tools/` | build-time tool, run once by MSBuild |

The two libraries multi-target the **lowest common supported pair**: `net10.0` is the LTS and the
framework an Aspire AppHost targets, and `net11.0` is the current release. A `net10.0` asset is what a
.NET 10 AppHost resolves; a consumer that has moved to .NET 11 gets the `net11.0` asset without a
package change. Both are built from the same source.

Projects that cannot sensibly multi-target declare a single framework **in their own project file**:

| Project | Targets | Why single-target |
| --- | --- | --- |
| `TestAppHost` | `net10.0` | Aspire starts it with `dotnet run`, which cannot pick a TFM, and an AppHost targets the framework Aspire supports |
| `TestAppHost.DataService`, `TestAppHost.SimpleData`, `TestAppHost.ComplexData` | `net10.0` | sample projects the AppHost starts |
| `EntityFrameworkCore.ModelDump` | `net10.0` | ships once under `tools/` and runs as `dotnet <tool>.dll`, so it needs one runtime |

Test projects inherit the SDK's pair, so the unit and integration suites run against both assets.

## SDK and language version

| Setting | Value | Where |
| --- | --- | --- |
| SDK | `11.0.100` (GA), `rollForward: latestFeature`, `allowPrerelease: false` | `global.json` |
| Target frameworks | Purview.BuildSdk's `Current` set: `net10.0;net11.0`; single-target projects declare `net10.0` locally | SDK default / project files |
| Language version | `15.0` | `src/Directory.Build.props` |
| Test runner | Microsoft.Testing.Platform | `global.json` |
| CSharpier | pinned in `.config/dotnet-tools.json` | build-time |

`rollForward` is `latestFeature`, not `latestMajor`: the build takes the newest **11.0.x** feature band
the machine has, and never silently switches to a future major SDK. `allowPrerelease` is `false` now
that .NET 11 is GA, so a preview SDK cannot satisfy the pin.

The target frameworks are **owned by Purview.BuildSdk** and are deliberately not restated in
`src/Directory.Build.props`. The SDK reads a project's TFM from the project file text, so a
`TargetFramework` set in a directory-level props file after the import only shadows the SDK's
`TargetFrameworks`: MSBuild honours the singular property, the project builds one TFM, and `pack`
still resolves the output path for the list. That failure (`NU5026`) is why the declaration lives in
the project file when it exists at all.

`LangVersion` is the GA `15.0`, not `preview`, so the language does not change underneath the build
when the SDK is serviced.

The container test images in `tests/Docker/` are pinned to `mcr.microsoft.com/dotnet/sdk:11.0` to
match, because they build this repository from inside the container.

## .NET 11 features in use

| Feature | How it is used | Why it matters here |
| --- | --- | --- |
| GA SDK and compiler | `global.json` pins 11.0.100 | deterministic, non-preview build |
| `net11.0` target | Purview.BuildSdk's default set | consumers on .NET 11 get an asset built for that runtime; .NET 10 hosts keep the LTS asset |
| Runtime Async | `runtime-async=on` on the `net11.0` leg only (SDK-applied) | lower await overhead and cleaner live stack traces in the .NET 11 asset, with no change to the `net10.0` asset |
| C# 15, `LangVersion 15.0` | `src/Directory.Build.props` | same compiler behaviour on both legs; new language features available |
| Closed hierarchies | `EfModelDumpRead` in `src/src/EntityFrameworkCore/` | the set of dump outcomes is complete, so a switch over it is exhaustive and a new outcome fails the build |
| MSBuild build server | `DOTNET_CLI_USE_MSBUILD_SERVER=1` in the `Justfile` | warm evaluation for repeated `just build` / `just test` runs |
| Multithreaded MSBuild | SDK-bootstrapped `Directory.Build.rsp` with `-mt` | runs a build's projects concurrently in one process |
| `dotnet test` improvements | `global.json` selects Microsoft.Testing.Platform | `just test` shows in-flight tests and per-assembly counts for both TFMs |
| JIT improvements | none needed | bounds-check elimination, devirtualisation and `SequenceEqual` folding apply to these assemblies when they run on the .NET 11 runtime |

## .NET 11 features deliberately not used

| Feature | Why not |
| --- | --- |
| C# 15 updated memory-safety rules (`MemorySafetyRules=2`) | still preview-only: compiling it forces `LangVersion=preview`, which would let preview syntax into every project. The repository has no unsafe code, so the tier buys nothing. Set to `0` in `src/Directory.Build.props` until the rules are GA. |
| .NET 11-only library APIs | `Process` run-and-capture, the new `Stream` types, `FullJoin`, async validation, and Zstandard compression only exist in the .NET 11 reference assemblies. The shared source also has to compile for the `net10.0` leg, and the diagram work does not need them. |
| `net11.0`-only asset | the packages ship `net11.0` **in addition to** `net10.0`, never instead of it. Dropping `net10.0` would raise the floor above the Aspire AppHost. |
| Union types | no surface needs one today; see below. |
| NuGet package validation baseline | the custom pack validation in `.config/purview-build.json` already asserts package contents. An API-compatibility baseline would need a published-baseline version pinned, which the release flow does not guarantee at build time. |
| `IsTrimmable` / `IsAotCompatible` | these libraries host Aspire resources and reflection-based metadata reads; they are not intended for trimming or AOT publishing, so the analyzers would produce noise rather than guidance. |

## Package versions

Runtime and Microsoft package references are pinned to the **lowest** target framework's servicing band —
currently `10.0.15` — never to `11.0.x`. The code that consumes them compiles for `net10.0`, so a
`11.0.x` reference would either fail to resolve or drag a .NET 11-only binary into a .NET 10 process.
Versions live in `Directory.Packages.props`; the rule when bumping is "newest patch in the band that
matches the lowest target framework", not "newest version published".

Build-only tooling may track the SDK instead, because it is never part of a consumer's output:
`Microsoft.SourceLink.GitHub` is `11.0.100`, and CSharpier and `dotnet-inspect` are pinned in
`.config/dotnet-tools.json`.

`CA1307` is suppressed on the `net11.0` leg only: it suggests `string.StartsWith(char,
StringComparison)`-style overloads that do not exist on `net10.0`, and the source must compile
unchanged for both.

## C# 15 features in use

### Closed hierarchies

`EfModelDumpRead` models the outcome of looking for a build-time EF Core dump:

```csharp
closed record class EfModelDumpRead;
sealed record EfModelDumpLoaded(EfModel Model) : EfModelDumpRead;
sealed record EfModelDumpUnavailable(EfModelDumpStatus Status) : EfModelDumpRead;
```

`EfModelLoader.LoadFromProject` switches over it without a discard arm. The hierarchy used to be a
`bool` plus an `out EfModel`, which could not distinguish a missing dump from a stale one — the
difference between "reference the ModelDump package" and "rebuild the project". That reason is now
carried into `EfModelLoadDiagnostics.DumpStatus` and the no-model warning.

`closed` needs `System.Runtime.CompilerServices.IsClosedTypeAttribute`, which only exists from .NET 11.
The `net11.0` leg uses the runtime type; the `net10.0` leg gets an internal copy from
`src/src/EntityFrameworkCore/Polyfills/CompilerServices.cs`, guarded by `#if !NET11_0_OR_GREATER`.
Roslyn looks the type up by full name, so the compiler recognises the polyfill both in this assembly
and in a consumer compiling against it.

### Union types

No type in the packages is a union today: the closed hierarchy above covers the one "one of these
outcomes" shape, and the other discriminated shapes are enums that are already exhaustive by
construction. Introducing a union purely to use the feature would add a wrapper without removing one.

If a union is added, follow the compiler's **non-boxing access pattern** rather than relying on the
generated `Value` property, so value-type cases are not boxed during pattern matching:

```csharp
readonly record struct Percentage(double Value);

[System.Runtime.CompilerServices.Union]
public struct ColumnWidth : System.Runtime.CompilerServices.IUnion
{
    readonly int _chars;
    readonly Percentage _percent;
    readonly byte _tag; // 0 = none, 1 = chars, 2 = percent

    public ColumnWidth(int chars) => (_chars, _percent, _tag) = (chars, default, 1);

    public ColumnWidth(Percentage percent) => (_chars, _percent, _tag) = (default, percent, 2);

    public object? Value => _tag switch { 1 => _chars, 2 => _percent, _ => null };

    public bool HasValue => _tag != 0;

    public bool TryGetValue(out int chars)
    {
        chars = _chars;
        return _tag == 1;
    }

    public bool TryGetValue(out Percentage percent)
    {
        percent = _percent;
        return _tag == 2;
    }
}
```

`TryGetValue` is what the compiler calls for a type pattern, and `HasValue` is what it calls for a null
pattern; without them it falls back to `Value` and boxes. The same polyfill rule applies:
`UnionAttribute` and `IUnion` are .NET 11 runtime types and would need an internal declaration on the
`net10.0` leg.

## What the move to the .NET 11 toolchain changed

| Dimension | Change | Where it shows |
| --- | --- | --- |
| Performance | the SDK's JIT improvements (bounds-check elimination, devirtualisation, `SequenceEqual` folding) apply to both assemblies when they run on .NET 11; no hot-path rewrite was needed, because the diagram work is I/O- and string-shaped, not compute-shaped | runtime behaviour, no source change |
| Stability | GA SDK pinned, no preview SDK, no roll-forward to a future major, no shadowed TFM evaluation | `global.json`, `src/Directory.Build.props` |
| Stability | the sample AppHost now resolves `Aspire.Hosting.AppHost` from `global.json` instead of a stale SDK-embedded 13.6.3, which carried a high-severity advisory (`NU1903`) | `TestAppHost.csproj` |
| Runtime | a .NET 11 host gets a `net11.0` assembly built against the .NET 11 reference assemblies; a .NET 10 host keeps the LTS assembly | `lib/net10.0/`, `lib/net11.0/` |
| Async | the `net11.0` assembly is compiled with Runtime Async, so its awaits avoid compiler-generated state machines | `Features=runtime-async=on` on that leg |
| Testing | the suite runs twice, once per TFM, on Microsoft.Testing.Platform — 2,468 tests, including both integration runs | `just test` |
| Build | warm MSBuild server, SDK-bootstrapped `-mt` multithreaded builds, SDK-owned TFM sets | `Directory.Build.rsp`, `Justfile` |
| Debug | cleaner async stack traces on the `net11.0` assembly; a missing or stale model dump is now named in the ER diagram log instead of being indistinguishable from "no model" | `ErdLog.ProjectProducedNoModel` |
| Packaging | both framework assets ship and are asserted by pack validation; package versions stay on the `10.0.x` band | `.config/purview-build.json`, `Directory.Packages.props` |

## Keeping this page true

When the target frameworks, SDK pin, or a package band change, update:

- `src/Directory.Build.props` (language version, memory-safety tier, CA1307 suppression)
- `global.json` (SDK and test runner)
- `.config/purview-build.json` (`lib/net10.0/...` **and** `lib/net11.0/...` in `RequiredContent`)
- `tests/Docker/Dockerfile.e2e` and `Dockerfile.e2e-cli` (SDK image)
- the single-target project files listed above
- this page, `README.md`, `AGENTS.md`, and [Getting Started](Getting-Started.md)
