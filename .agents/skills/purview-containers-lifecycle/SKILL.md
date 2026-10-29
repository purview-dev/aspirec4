---
name: purview-containers-lifecycle
description: >-
  Use when reasoning about a Purview.Containers container's lifecycle or readiness - StartAsync,
  StopAsync and DisposeAsync semantics, cleanup guarantees and why there is no restart, the Wait
  strategy family and started-versus-ready, and WSL Containers networking, port mapping and the
  absence of inter-container DNS.
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
    ValueTask DisposeAsync();
    // restart is intentionally NOT offered
}
```

**There is no `RestartAsync`.** WSLC has no restart operation, and stop-then-start is not equivalent,
so offering one would be a method that quietly means something different per backend.

WSLC's `ContainerState` runs `Invalid → Created → Running → Exited → Deleted`.

| Operation | Behaviour |
| --- | --- |
| `StartAsync` | creates the session if needed, ensures the image per the pull policy, creates the container, starts it, then waits. A missing init binary throws `ArgumentException 0x80070057` with the OCI message, surfaced as `WslContainerStartupException`. |
| `StopAsync` | `Stop(SIGTERM, grace)`, then `Stop(SIGKILL, …)` if still running. **Idempotent** — WSLC accepts stopping an already-stopped container. |
| `DeleteAsync` | `Delete(Force)`. A double delete throws `0x80010108` (`RPC_E_DISCONNECTED`) and is swallowed. |
| `DisposeAsync` | stop if running → delete → release the WinRT object. **Idempotent.** |

## Cleanup guarantees

These are the ones that matter when a test throws:

- `DisposeAsync` runs in a `finally` by callers, **and** the container object is registered for
  cleanup by the runtime, so disposal happens even if a test throws after `StartAsync`.
- `EnableAutoRemove` defaults to **`false`**: the library owns deletion. WSLC auto-removal would
  delete a one-shot container before its state or output could be inspected — precisely when you need
  them. Opt in with `.WithAutoRemove()`.
- A **partial startup failure** — image pull, create, start or wait — tears down through the same
  `DisposeAsync` path. So does a **cancelled** `StartAsync` or wait.
- A **container crash** fires `InitProcess.Exited`; the container is then `Exited` and `DisposeAsync`
  deletes it.
- A **session crash** marks the runtime session dead, and subsequent operations throw
  `WslContainerSessionTerminatedException` rather than failing obscurely.
- **Test-process termination** disposes the shared session through a process-exit hook, freeing the
  name. A *crashed* process leaves an orphaned session that only reserves its name; sweep it with
  `wslc --session <name> system session terminate`.

## Secrets and names

Container names are generated uniquely as `{service}-{processId}-{random8}` and **never contain
credentials**; session names are `wslc-{pid}-{random8}`. Diagnostics redact secrets through a
`Secret` wrapper whose `ToString()` returns `<redacted>`.

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
- The default timeout is the container's `StartupTimeout` — 5 minutes, overridable with
  `.WithStartupTimeout(…)` or per-strategy.
- A timeout diagnostic includes the container name, image, state, mapped ports, the strategy type,
  the last check error, and a bounded, secret-redacted tail of stdout/stderr.
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
- There is **no name or DNS resolution between containers**. `http://<hostname>:8080/` and
  `http://<container-name>:8080/` fail with "bad address"; `/etc/hosts` holds only localhost and the
  container's own `IP <short-id>`.
- There are **no managed network objects** — `Microsoft.WSL.Containers` has no `network
  create/connect`, unlike the CLI.

How the library models it: `GetMappedPublicPort(containerPort)` reads
`Inspect().Ports["<port>/tcp"][0].HostPort`; `IContainer.NetworkIp` is the bridge IP after start
(WSLC reads `Inspect().NetworkSettings.Networks.bridge.IPAddress`, Docker the Testcontainers
`IpAddress`; `null` before start or without a network); TCP and HTTP waits connect to
`127.0.0.1:<hostPort>`.

**What not to model.** Do not invent Docker-style `Network` objects — the semantics are just "bridged
with IPs". Do not claim container-name resolution works; it does not. Containers are wired
together by passing a dependency's `NetworkIp` into configuration or environment: a module that owns a
dependency starts it first and injects the address from `ContainerBase.ConfigureForStart` (the Azure
Service Bus Emulator does this with its SQL Server sidecar). A general multi-container environment
builder is still deferred.

## Related skills

`purview-containers-quickstart` for packages, backends and connection strings;
`purview-containers-testing` for how a suite is organised and why it runs serially.
