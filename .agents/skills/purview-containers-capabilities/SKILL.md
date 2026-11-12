---
name: purview-containers-capabilities
description: >-
  Use when configuring Testcontainers-style capabilities on Purview.Containers Docker or WSLC:
  entrypoint, user, labels, exec overrides, file copy, resource mappings, one-shot exit waits,
  managed networks and DNS aliases, Dockerfile image builds, reusable containers, cancellation
  deadlines, and the WSLC SDK versus CLI capability boundary.
metadata:
  product: Purview.Containers
  repository: purview-dev/containers
---

# Capabilities on Docker and WSLC

Docker uses Testcontainers for .NET. WSLC ordinarily uses its managed SDK (3.0.5): lifecycle, images,
named networks and aliases, Linux capabilities and host loopback are SDK-backed; entrypoint/user/labels,
reuse, image builds and copy need the `wslc` CLI on PATH. The CLI targets the same
SDK-owned session and image store, not the user's default CLI session. There is no Docker dependency
on the WSLC path and no silent runtime fallback when an operation is unsupported.

## Resolve once for related resources

```csharp
var backend = await ContainerBackends.ResolveAsync(ct);
await using var network = await backend.CreateNetworkAsync(cancellationToken: ct);
await using var container = new ContainerBuilder(backend)
    .WithImage("alpine:latest")
    .WithNetwork(network.Name)
    .WithNetworkAliases("worker")
    .WithEntrypoint("/bin/sh")
    .WithCommand("-c", "echo ready; sleep 300")
    .WithUser("1000:1000")
    .WithLabel("suite", "example")
    .WithWaitStrategy(Wait.ForLogMessage("ready"))
    .Build();
await container.StartAsync(ct);
```

Declare networks/images before their containers so reverse-order async disposal removes containers
first. Containers on a custom bridge can resolve its aliases. The default WSLC bridge still lacks
container-name DNS. `ConnectionMode.Container` remains unsupported by the built-in connection-string
providers; build the service URL from its explicit network alias and internal service port.

## Files and one-shot jobs

`WithResourceMapping(hostSource, absoluteContainerTarget)` copies before init starts.
`CopyAsync(hostSource, absoluteContainerTarget, ct)` copies while running.
A file source names the destination file; a directory source copies its contents under the target.
`ReadFileAsync(absoluteContainerFile, ct)` returns exact bytes. Copy is preferable to a bind mount
when the daemon cannot access the test host's files.

```csharp
await using var job = new ContainerBuilder(backend)
    .WithImage("alpine:latest")
    .WithEntrypoint("/bin/sh")
    .WithCommand("-c", "exit 7")
    .WithWaitStrategy(Wait.ForExitCode(7))
    .Build();
await job.StartAsync(ct);
long exitCode = await job.WaitForExitAsync(ct);
```

An unexpected exit fails immediately with `ContainerExitCodeException`, not readiness retries.

Exec supports environment and working-directory overrides on both backends:

```csharp
var result = await container.ExecAsync(["sh", "-c", "printf '%s' \"$VALUE\""], new ExecOptions
{
    Environment = new Dictionary<string, string> { ["VALUE"] = "literal value" },
    WorkingDirectory = "/tmp",
    Timeout = TimeSpan.FromSeconds(10),
}, ct);
```

Commands are argv, not automatically a shell script. Invoke a shell explicitly when needed.
Interactive stdin is not exposed by capture-based exec and is rejected.

## Builds and reuse

```csharp
await using var image = await backend.BuildImageAsync(new ImageBuildConfiguration
{
    ContextDirectory = Path.GetFullPath("test-image"),
    Name = $"suite-image:{Guid.NewGuid():N}",
    Dockerfile = "Dockerfile",
    Timeout = TimeSpan.FromMinutes(5),
}, ct);
// Pass image.Name to WithImage; dispose all its containers before image.
```

`WithName("stable-name").WithReuse()` keeps a container instead of deleting it on disposal.
It requires an explicit name and cannot use auto-remove. Docker follows Testcontainers' experimental
reuse semantics and disables normal reaper cleanup. WSLC reopens a matching named container only in
the owning session; its configuration fingerprint must match. WSLC reuse is NOT cross-process.
`DeleteAsync(ct)` explicitly removes either reusable container. Reuse does not isolate test data.
Delete before disposing the wrapper; reopen a retained container with matching reuse settings to
delete it later.
The WSLC fingerprint checks configuration, not the contents of mapped files or mutable image tags;
delete/recreate when those inputs change.

## Cancellation and defaults

| Operation | Default | Override |
| --- | --- | --- |
| Full startup, including image and readiness | 5 minutes | `WithStartupTimeout` |
| Exec and exit wait | 1 minute | `WithExecTimeout`, `ExecOptions.Timeout` |
| Stop including escalation | 15 seconds | `WithStopTimeout` |
| Cleanup/delete | 30 seconds | `WithCleanupTimeout` |
| Copy/read/log snapshot | 1 minute | `WithOperationTimeout` |
| Network create/delete | 1 minute | `NetworkConfiguration.Timeout` |
| Build | 5 minutes | `ImageBuildConfiguration.Timeout` |

Always forward caller tokens. Deadlines are positive finite durations. Operation cancellation/deadlines
throw `OperationCanceledException`; readiness exhaustion throws `ContainerTimeoutException`.
Live logs need an enumeration cancellation token and intentionally have no automatic deadline.
Startup failure/cancellation performs bounded cleanup before propagating the original exception;
create a new container instance to retry.

Do NOT claim "zero hangs": synchronous native WSLC calls cannot be forcibly interrupted safely.
Managed gate waits, polling and CLI commands are bounded. Native SDK calls already entered can
outlive a token deadline. CLI cancellation kills the owned command tree, not server-side operations
already submitted. Custom readiness callbacks must still stop their own work on cancellation.
Docker exec cancellation stops the client wait, not necessarily the remote process: Docker has no
exec-process kill endpoint. WSLC signals its owned exec process. Dispose the container to end remaining
processes and never assume cancellation rolls back application side effects.

CLI-only creation currently rejects privileged mode and multi-line environment values. Ordinary
SDK-backed privileged containers are unaffected, and `WithCapabilityAdd`/`WithCapabilityDrop` map to
the managed SDK (or `--cap-add`/`--cap-drop` on the CLI path). Never put secrets in names or labels; CLI errors
omit raw command/build output to prevent accidental disclosure.

## Related skills

Use `purview-containers-quickstart` for installation/selection,
`purview-containers-lifecycle` for lifecycle ownership, and
`purview-containers-testing` for test execution.
