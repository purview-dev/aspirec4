# Local CLI Runtimes

By default the LikeC4 server runs as the `ghcr.io/likec4/likec4` Docker container. When Docker is not available — or you prefer a local Node.js-based workflow — switch to a local CLI with `.WithLocalCLI()`.

```csharp
builder.AddAspireC4().WithLocalCLI();
```

The selected runtime must be installed and accessible on the system `PATH`.

## Runtime selection

```csharp
builder.AddAspireC4().WithLocalCLI(LocalCLIRuntime.Bun);
```

`LocalCLIRuntime` supports:

| Runtime | Command |
| --- | --- |
| `Auto` | Detects the first available runtime in order: npx → pnpm → yarn → bun → deno. |
| `Npx` | `npx likec4 serve <dir> --port <port>` |
| `Pnpm` | `pnpm dlx --ignore-workspace likec4 serve <dir> --port <port>` |
| `Yarn` | `yarn dlx --package likec4 --package react --package react-dom likec4 serve <dir> --port <port>` |
| `Bun` | `bunx --bun likec4 serve <dir> --port <port>` |
| `Deno` | `deno run --allow-all --node-modules-dir=none npm:likec4 serve <dir> --port <port>` |

> [!NOTE]
> `Auto` throws a `DistributedApplicationException` when no supported package manager is found. Install one of Node.js (`npx`), pnpm, yarn, bun, or Deno, or remove `WithLocalCLI()` to use the Docker container.

## Behavior notes

- The output directory is passed as an absolute path; the server process uses the system temp directory as its working directory so package managers do not walk up and treat the AppHost's parent `package.json` as a workspace root.
- `ContainerImageTag` is ignored — it only applies to the Docker container.
- Yarn's `dlx` does not install optional peer dependencies (`react`, `react-dom`) by default, so AspireC4 passes them explicitly as `--package` arguments.
- HMR is enabled with `--hmr-port` when not disabled; `DisableHMR` still applies.

## ConfigureServer

Use `ConfigureServer` to apply annotations directly to the inner server resource (e.g. `WithLikeC4Details`):

```csharp
builder.AddAspireC4()
    .ConfigureServer(server => server.WithLikeC4Details(options => options.WithLabel("Architecture diagram")));
```

> [!TIP]
> Call `ConfigureServer` **after** `.WithLocalCLI()` if you want to configure the CLI resource. Calling it first configures the Docker container that is subsequently replaced.

## Next pages

- [Getting Started](Getting-Started.md)
- [Configuration](Configuration.md)