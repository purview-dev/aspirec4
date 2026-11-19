# AppHost languages

AspireC4 draws the same LikeC4 architecture diagram from an Aspire AppHost in C#, TypeScript, Go or
Python, from the one `AspireC4.Hosting` package. This page compares the five supported AppHosts — C# on
Aspire 17 and on 13.6.5, and the Aspire 17 TypeScript, Go and Python AppHosts — and what differs
between them.

## The AppHosts

| AppHost | Entry point | Declared in | Aspire | Sample | Run it |
| --- | --- | --- | --- | --- | --- |
| **C#**, Aspire 17 and later | `AppHost.cs` | a `Microsoft.NET.Sdk` project referencing `Aspire.Hosting.AppHost` | 17.0.0+ | `src/src/TestAppHost` | `dotnet run --project src/src/TestAppHost` |
| **C#**, the Aspire 13 line | `AppHost.cs` | a `Aspire.AppHost.Sdk/13.6.5` project | 13.6.5+ | `src/src/TestAppHost.Aspire13` | `dotnet run --project src/src/TestAppHost.Aspire13` |
| [**TypeScript**](TypeScript.md) | `apphost.mts` | `aspire.config.json`, `"language": "typescript/nodejs"` | 13.6.5+ | `samples/typescript-app-host` | `just ts-run` |
| [**Go**](Go.md) | `apphost.go` | `aspire.config.json`, `"language": "go"` | 17.0.0 | `samples/go-app-host` | `just go-run` |
| [**Python**](Python.md) (experimental in Aspire 17) | `apphost.py` | `aspire.config.json`, `"language": "python"` | 17.0.0 | `samples/python-app-host` | `just py-run` |

The two C# AppHosts are covered in [Requirements](../Requirements.md#aspire) and
[Getting Started](../Getting-Started.md). The TypeScript, Go and Python AppHosts declare AspireC4 in the
`packages` of their `aspire.config.json`, and `aspire restore` generates a binding for it in that
language under `.aspire/modules/`. Never edit the generated files; run `aspire restore` again.

## One API in every language

The bindings are generated from the same exported C# API, so every method has the same name, in each
language's spelling:

| C# | TypeScript | Go | Python |
| --- | --- | --- | --- |
| `AddAspireC4` | `addAspireC4` | `AddAspireC4` | `add_aspire_c4` |
| `WithLikeC4Details` | `withLikeC4Details` | `WithLikeC4Details` | `with_like_c4_details` |
| `WithLikeC4Reference` | `withLikeC4Reference` | `WithLikeC4Reference` | `with_like_c4_reference` |
| `WithLikeC4ConnectionReference` | `withLikeC4ConnectionReference` | `WithLikeC4ConnectionReference` | `with_like_c4_connection_reference` |
| `WithLikeC4Group` | `withLikeC4Group` | `WithLikeC4Group` | `with_like_c4_group` |
| `WithLink`, `WithMetadata` (node and relationship details) | `withLink`, `withMetadata` | `WithLink`, `WithMetadata` | `with_link`, `with_metadata` |
| `WithServerLikeC4Details` | `withServerLikeC4Details` | `WithServerLikeC4Details` | `with_server_like_c4_details` |
| `WithExcludedResourceType(string)` | `withExcludedResourceType` | `WithExcludedResourceType` | `with_excluded_resource_type` |
| `WithLocalCLI` | `withLocalCLI` | `WithLocalCLI` | `with_local_cli` |
| `WithEntityFrameworkDiagram` | `withEntityFrameworkDiagram` | `WithEntityFrameworkDiagram` | `with_entity_framework_diagram` |

Options are passed the way each language passes them: C# lambdas, TypeScript option objects with async
callbacks, Go option structs (`&aspire.WithLikeC4DetailsOptions{...}`), and Python keyword arguments.

## What differs between them

| | C# | TypeScript | Go and Python |
| --- | --- | --- | --- |
| Label the LikeC4 server node | `WithServerLikeC4Details` or `ConfigureServer` | `withServerLikeC4Details` or `configureServer` | **`WithServerLikeC4Details` only** — the bindings cannot pass `ConfigureServer`'s `IResource` callback argument |
| Exclude a resource type | `WithExcludedResourceType<T>()` or by full type name | by full type name | by full type name |
| Compile-time [registry validation](../configuration/Source-Generator.md) (`ASPIREC4xxx`) | yes | no — it does not compile C#, so the .NET 11 SDK floor does not apply | no |
Generated output lands in the same place for every AppHost: `.aspirec4/` beside the AppHost — the
project for C#, and the folder holding `apphost.mts`, `apphost.go` or `apphost.py` otherwise — with the
architecture diagram in `likec4/`, the ER diagram in `erd/` and exported images in `images/`. Relative
output paths are resolved against the AppHost's folder, not the process's working directory: a TypeScript,
Go or Python AppHost runs AspireC4 inside the Aspire CLI's own AppHost server, whose working directory is
under `.aspire/`. See [Generated Output](../Generated-Output.md); to write elsewhere, set the output
directory in code (`withOutputDirectory`) or through configuration, such as the `AspireC4__OutputDirectory`
environment variable.

> [!NOTE]
> The [Entity Framework Core ER diagram](../Entity-Framework-Diagrams.md) works from every AppHost
> language, but it is built purely from .NET Entity Framework Core code, so it only ever draws the .NET
> project resources in the graph (`AddDotnetProject` or `AddProject<>`), never a Node.js, Go or Python
> service.

## The samples share one contract

All five samples build the same resource graph — Azure Redis and Postgres running as local containers,
Redis Commander and PgWeb, local Redis and Postgres in a group, a Node.js app connected to all four, the
.NET data service with its ER diagram, and hidden parameters — and every pull request and release
checks that each draws the same diagram:

- the C# AppHosts, through the integration suites' `SampleAppHostContractTests`;
- the TypeScript, Go and Python AppHosts, through `AspireC4.AppHosts.IntegrationTests`, which starts
  each one with the Aspire CLI (`just test-apphosts` locally).

A sample that drifts, or a binding that can no longer express a call, fails that check. Change all five
samples together.

## Keeping the Aspire version in step

`AspireAppHostVersion` in `Directory.Packages.props`, the Aspire line the repository's C# AppHost runs
on, is the single source of truth. The TypeScript, Go and Python samples repeat it in their
`aspire.config.json`, both as `sdk.version` and in the `Aspire.Hosting.*` package pins, because the Aspire
CLI reads that file rather than MSBuild. `just samples-validate` compares them and fails on drift;
`just samples-validate-fix` rewrites the samples to match. The same check runs on every commit.

## Next pages

- [TypeScript AppHosts](TypeScript.md)
- [Go AppHosts](Go.md)
- [Python AppHosts](Python.md)
- [Customizing Resources](../configuration/Customizing-Resources.md)
