---
name: purview-containers-quickstart
description: >-
  Use when adding throwaway containers to a .NET test suite with Purview.Containers - which package to
  reference, how the WSL Containers and Docker backends are selected automatically, running a generic
  container with ContainerBuilder, using a typed service module, and getting a connection string.
metadata:
  product: Purview.Containers
  repository: purview-dev/containers
---

# Purview.Containers quickstart

Throwaway Linux containers for .NET tests, behind one API, over either **WSL Containers** (no Docker
installation on Windows) or **Docker** (anywhere). The same test code runs on both.

> **Experimental and prerelease.** The API, defaults and packaging rules can change between releases.
> Pin the version you adopt.

## Which package

| Package | Use it when |
| --- | --- |
| `Purview.Containers` | the umbrella: `Core` plus both backends. One reference, zero config — WSLC on Windows, Docker elsewhere. **The default.** |
| `Purview.Containers.Wsl` | WSL Containers only — a Windows machine with no Docker. |
| `Purview.Containers.Docker` | Docker only — any platform, CI, Linux/macOS. A Linux-only job can skip the WSL backend's ~19 MB payload with `Core` + `.Docker`. |

A service module is backend-neutral, so it needs a backend (or the umbrella) alongside it:

```bash
dotnet add package Purview.Containers.PostgreSql
dotnet add package Purview.Containers
```

## The zero-config shape

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Purview.Containers.Redis" Version="..." />
    <PackageReference Include="Purview.Containers.PostgreSql" Version="..." />
    <PackageReference Include="Purview.Containers" Version="..." />
  </ItemGroup>
</Project>
```

Three things make it work, and nothing else is required:

- **A platform-neutral target framework** (`net10.0`). `Purview.Containers.Wsl` is multi-targeted; a
  `net10.0` project binds its **portable facade**, which runs the WSLC implementation on Windows and
  reports `wsl` unavailable everywhere else. A Windows target framework binds the implementation
  directly — and then cannot run on a Linux CI runner.
- **The umbrella package**, bringing `Core` and both backends.
- **No registration line and no environment variable.** Each backend package ships `buildTransitive`
  assets that generate a module initializer in your assembly, so the process discovers the registered
  backends itself. Those assets flow transitively through the umbrella.

## Backend selection

| Host | Selected | Why |
| --- | --- | --- |
| Windows with WSL Containers | `wsl` | the facade loads the WSLC implementation, and WSLC is preferred |
| Windows with Docker only | `docker` | the facade reports `wsl` unavailable, so selection falls through |
| Linux / macOS | `docker` | the facade only ever activates on Windows |

Selection probes the registered backends in priority order (`wsl` is `0`, `docker` is `100`) and
returns the first that is both **available** and **compatible**. When none is usable, the exception
lists each backend's availability, version and missing components.

```csharp
foreach (var backend in await ContainerBackends.ProbeAllAsync())
    Console.WriteLine($"{backend.Name}: usable={backend.IsUsable} version={backend.Version}");
```

```bash
PURVIEW_CONTAINERS_BACKEND=docker   # or wsl - fail loudly instead of falling back
```

A **named** backend never silently falls back: if it cannot run, the test fails with that backend's own
diagnostics. That is usually what you want in CI.

The library never installs a runtime. `WslContainerRuntime.GetInfoAsync()` and
`DockerContainerBackend.GetInfoAsync()` report what is missing. Verify a host with `wsl --version` and
`wslc version`, or `docker info`.

## A generic container

```csharp
using Purview.Containers;
using Purview.Containers.Waiting;

await using var container = new ContainerBuilder()
    .WithImage("docker.io/library/redis:latest")
    .WithPortBinding(6379, assignRandomHostPort: true)
    .WithWaitStrategy(Wait.ForTcpPort(6379))
    .Build();

await container.StartAsync();

ushort port = container.GetMappedPublicPort(6379);
```

`Build()` validates the accumulated configuration and resolves the backend when the container starts.
`StartAsync()` creates the container, starts it, and **only returns once every configured wait
strategy is satisfied**. `DisposeAsync()` stops and deletes the container — and never terminates a
shared WSLC session.

## A typed module

```csharp
using Npgsql;
using Purview.Containers.PostgreSql;

await using var postgres = new PostgreSqlBuilder()
    .WithDatabase("tests")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

await postgres.StartAsync();   // returns once pg_isready succeeds

await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
await connection.OpenAsync();
```

No image names, no ports, no `docker`, no `wslc`. The default image, the random host port mapping and
the readiness check all come from the module.

Modules: PostgreSQL, Redis, SQL Server, MySQL, RabbitMQ, Azurite, NATS, the Azure Key Vault
Emulator and the Azure Service Bus Emulator. Each ships a README inside its own package.

## Connection strings

Every container implements `IConnectionStringProvider`, so you can ask any container without knowing
its module type:

```csharp
string connectionString = container.GetConnectionString();                 // ConnectionMode.Host
string same             = container.GetConnectionString(ConnectionMode.Host);
```

| Mode | Meaning | Support |
| --- | --- | --- |
| `Host` | test host → container, via the mapped host port (`127.0.0.1:{port}`) | both backends |
| `Container` | container → container, via the container network | **not supported** — throws `ConnectionStringModeNotSupportedException` |

Container-to-container is deliberately deferred: WSLC has no managed networks or inter-container DNS,
and the Docker backend does not attach containers to a shared network.

**Use `127.0.0.1`, never `localhost`.** `localhost` resolves to `::1` first on .NET, and while most
clients fall back to IPv4, `Microsoft.Data.SqlClient` **hangs** on `::1` without falling back — which
is why the SQL Server module's connection strings are explicitly `127.0.0.1,{port}`.

### A custom provider

```csharp
sealed class ReadOnlyPostgreSql : ContainerConnectionStringProvider<PostgreSqlContainer, PostgreSqlConfiguration>
{
    protected override string GetHostConnectionString() =>
        $"{Container.GetConnectionString()};ApplicationName=readonly";
}

var postgres = new PostgreSqlBuilder()
    .WithConnectionStringProvider(new ReadOnlyPostgreSql())
    .Build();
```

`Configure` runs once, **after** the container has started, so runtime-assigned ports are available. A
provider producing an empty string throws `ConnectionStringNotAvailableException`; one used before
`Configure` throws `ConnectionStringProviderNotConfiguredException`. The named overload
(`GetConnectionString(name, mode)`) exists for modules with several endpoints and otherwise throws
`ConnectionStringNameNotSupportedException`.

## Related skills

`purview-containers-lifecycle` for start/stop/dispose semantics, wait strategies and networking;
`purview-containers-testing` for suite layout, serial execution and CI; and
`purview-containers-consumer-requirements` for the target-framework contract and the `PCC` guards.
