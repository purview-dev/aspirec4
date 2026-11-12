---
name: purview-containers-lifecycle
description: >-
  Use when reasoning about a Purview.Containers container's lifecycle or readiness - StartAsync,
  StopAsync and DisposeAsync semantics, cleanup guarantees and why there is no restart, the Wait
  strategy family and started-versus-ready, and WSL Containers networking, port mapping and the
  default-bridge DNS limitations and managed network aliases.
metadata:
  product: Purview.Containers
  repository: purview-dev/containers
---

# Lifecycle, readiness and networking

## The public surface

```csharp
public interface IContainer : IAsyncDisposable
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task DeleteAsync(CancellationToken ct = default);
    Task<long> WaitForExitAsync(CancellationToken ct = default);
    ValueTask DisposeAsync();
    // restart is intentionally NOT offered
}
```

**There is no `RestartAsync` in the shared contract.** Do not infer native runtime capabilities from
that API choice.

WSLC's `ContainerState` runs `Invalid → Created → Running → Exited → Deleted`.

| Operation | Behaviour |
| --- | --- |
| `StartAsync` | creates the session if needed, ensures the image per the pull policy, creates the container, starts it, then waits. A missing init binary throws `ArgumentException 0x80070057` with the OCI message, surfaced as `WslContainerStartupException`. |
| `StopAsync` | Signal init with SIGTERM, poll up to 10 seconds, then SIGKILL and up to 5 seconds more, bounded by `StopTimeout`. Cancellation propagates; no blocking native `Container.Stop`. |
| `DeleteAsync` | `Delete(Force)`. A double delete throws `0x80010108` (`RPC_E_DISCONNECTED`) and is swallowed. |
| `DisposeAsync` | normally stop, delete and release handles; reusable containers are retained. **Idempotent.** |

## Cleanup guarantees

These are the ones that matter when a test throws:

- Use `await using` so failures run `DisposeAsync`. The process-exit hook owns session cleanup,
  not a per-container disposal registry.
- `EnableCleanUp` defaults to **`true`** (Testcontainers' `WithCleanUp`): `DisposeAsync` stops and
  deletes the container; Docker also removes its anonymous volumes. Named `WithVolumeMount` volumes
  are shared data and survive (WSLC volumes live on the session). `.WithCleanUp(false)` stops the
  container and keeps it — and its volumes — for inspection; remove it with `DeleteAsync` before
  disposal, or with the backend's own tooling.
- `EnableAutoRemove` defaults to **`false`**: the library owns deletion. WSLC auto-removal would
  delete a one-shot container before its state or output could be inspected — precisely when you need
  them. Opt in with `.WithAutoRemove()`. It is independent of cleanup and wins when the init process
  exits.
- `WithReuse()` implies cleanup off, and `WithReuse().WithCleanUp(true)` is rejected at build time.
- Named volumes are **retained by default**, matching Docker. Set
  `ContainerCleanup.CleanUpVolumesOnExit = true`, or `PURVIEW_CONTAINERS_CLEAN_VOLUMES=true` before the
  first container is created, to delete the volumes the active backend created when the process exits or
  its session terminates. `ContainerBackends.CleanUpAsync()` / `IContainerBackend.CleanUpAsync()` does it
  explicitly; pre-existing volumes, the shared image store and host bind mounts are never touched. On WSLC,
  `WslContainerRuntimeOptions.CleanUpVolumesOnTerminate` overrides the shared switch.
- A **partial startup failure** — image pull, create, start or wait — tears down through the same
  `DisposeAsync` path. So does a **cancelled** `StartAsync` or wait.
- Builder-produced containers serialize lifecycle operations; disposal cancels an in-flight startup
  before waiting for cleanup. A failed-start wrapper is terminal: build a new one to retry.
- A **container crash** is observable through live state/exit-code reads, even if an exit event is
  missed; normal disposal deletes it.
- A **session crash** marks the runtime session dead, and subsequent operations throw
  `WslContainerSessionTerminatedException` rather than failing obscurely.
- **Test-process termination** disposes the shared session through a process-exit hook, freeing the
  name. A *crashed* process leaves an orphaned session; the runtime sweeps `purview-*` (and legacy
  `wslc-*`) sessions whose creator PID is gone before the next session starts, deletes their
  per-session storage directories, removes stale containers whose generated name embeds a dead PID and
  prunes unused anonymous volumes. `WslContainerRuntime.SweepOrphanedSessionsAsync()` does the session
  sweep explicitly; `CleanUpAsync()` also removes container residue.

## Secrets and names

Container names are generated uniquely as `{service}-{processId}-{random8}` and **never contain
credentials**; session names are `purview-{pid}-{random8}`. Diagnostics redact secrets through a
`Secret` wrapper whose `ToString()` returns `<redacted>`.
Application output can contain secrets; do not assume log snapshots or timeout diagnostics are safe
to publish. CLI failures omit raw command/build output.

## Wait strategies

**A started container is not a ready service.** `Container.Start()` returns once the init process is
running; readiness is a separate check, and it is the one your test actually depends on.

```text
IWaitStrategy
  TimeSpan? Timeout         // null -> the container's StartupTimeout
  TimeSpan  Interval        // default 250 ms
  int?      Retries         // optional max failed checks
  Task<bool> UntilAsync(WaitContext context, CancellationToken ct)

WaitContext { IContainer Container; string? NetworkIp; int? GetHostPort(ushort); int? FirstHostPort; }
```

| Factory | Checks |
| --- | --- |
| `Wait.ForContainerRunning()` | `State == Running`. **Started, not ready.** |
| `Wait.ForTcpPort(8080)` | connects to `127.0.0.1:<mapped host port>`. IPv4 only. |
| `Wait.ForHttp("/health")` | `.ForPort`, `.ForScheme`, `.ForStatusCode`, `.ForStatusPredicate`, `.ForHeader`, `.AllowInsecureTls` |
| `Wait.ForLogMessage(string \| Regex)` | case-insensitive substring, or regex, against **accumulated** init-process output |
| `Wait.ForCommand("pg_isready", "-U", "postgres")` | runs it via `ExecAsync`; ready at the expected exit code (default 0, `.ForExitCode(…)`) |
| `Wait.ForCustom((context, ct) => …)` | anything else |
| `Wait.ForAll(…)` / `Wait.ForAny(…)` | every strategy ready on the same check / at least one |

Fluent modifiers: `.WithTimeout(…)`, `.WithInterval(…)`, `.WithRetries(…)`.

Details worth knowing:

- Waits run **inside** `StartAsync()`, after the container starts. `StartAsync` only returns when
  every strategy is ready, or throws `ContainerTimeoutException`.
- `StartupTimeout` is a five-minute total managed budget for backend resolution, image, creation,
  start and readiness. A strategy may shorten that budget, never extend it.
- A timeout diagnostic includes the container name, image, state, mapped ports, strategy and a bounded
  log tail. Application logs can contain secrets; do not print them indiscriminately.
- The container buffers output from process start, so a log message emitted **before** the waiter's
  first check still matches.
- HTTP TLS validation is **on** by default. `AllowInsecureTls()` disables it for that request handler
  only, never globally.
- Transient exec failures during startup count as not-ready, not as failure.

Modules pick their own readiness check: PostgreSQL `pg_isready`, Redis `redis-cli ping`, RabbitMQ
`rabbitmq-diagnostics ping`, SQL Server a host-side connection.

## Networking

| `ContainerNetworkingMode` | Effect | Port mappings |
| --- | --- | --- |
| `null` (raw default) | Docker `NetworkMode: "none"` — no network, no IP | `CreateContainer` throws `0x80070057` |
| `None` | the same | no |
| `Bridged` | `172.17.0.x` on the session bridge; host→container mapped ports work | yes |

`ContainerBuilder` defaults to **`Bridged`**, because a container with no network is not useful.

Verified WSLC behaviour, and the parts that surprise people:

- The host bind address is **IPv4 loopback only** (`HostIp: 127.0.0.1`). IPv6 `::1` does not connect.
- **Clients must use `127.0.0.1`, not `localhost`.** `localhost` resolves to `::1` first on .NET.
  Npgsql and StackExchange.Redis fall back to IPv4; `Microsoft.Data.SqlClient` **hangs** without
  falling back.
- Containers in the same session **can** reach each other by IP under `Bridged`.
- On the **default bridge** there is no name/DNS resolution between containers. `http://<hostname>:8080/` and
  `http://<container-name>:8080/` fail with "bad address"; `/etc/hosts` holds only localhost and the
  container's own `IP <short-id>`.
- Use `IContainerBackend.CreateNetworkAsync` and `WithNetwork`/`WithNetworkAliases` for managed
  networks with alias DNS on both backends. WSLC uses the managed SDK network API in the SDK-owned session.

How the library models it: `GetMappedPublicPort(containerPort)` reads
`Inspect().Ports["<port>/tcp"][0].HostPort`; `IContainer.NetworkIp` is the bridge IP after start
(WSLC reads `Inspect().NetworkSettings.Networks.bridge.IPAddress`, Docker the Testcontainers
`IpAddress`; `null` before start or without a network); TCP and HTTP waits connect to
`127.0.0.1:<hostPort>`.

Built-in modules can still wire dependencies by `NetworkIp` through `ConfigureForStart`. Managed
networks add explicit alias DNS; they do not automatically add `ConnectionMode.Container` providers.
Dispose attached containers before the owned network.

## Cancellation, exit waits and reuse

Defaults are one minute for exec/exit waits and copy/read/log snapshots, 15 seconds for stop and
30 seconds for cleanup. Use `WithExecTimeout`, `WithStopTimeout`, `WithCleanupTimeout` and
`WithOperationTimeout` to override. `ExecOptions.Timeout` overrides one exec call.
Cancellation/operation deadlines throw `OperationCanceledException`, never a successful exec result.
Readiness exhaustion throws `ContainerTimeoutException`. Live log tails need a caller token.

Native synchronous WSLC calls cannot be interrupted safely. Managed waits and CLI processes are
bounded, but do not claim hard zero-hang guarantees for a blocked native runtime.
`Wait.ForExitCode(expected)` handles one-shot jobs; `WaitForExitAsync(ct)` returns the actual code.
`WithName(...).WithReuse()` retains a named container on disposal, with cleanup implied off; `DeleteAsync(ct)` explicitly removes
it. WSLC reuse ends with the owning session; Docker follows Testcontainers reuse semantics.

## Related skills

`purview-containers-quickstart` for packages, backends and connection strings;
`purview-containers-testing` for how a suite is organised and why it runs serially;
`purview-containers-capabilities` for networks, files, builds and the SDK/CLI boundary.
