# Python AppHosts

Use AspireC4 from an Aspire 17 Python AppHost (`apphost.py`): the Aspire CLI generates a Python binding
for AspireC4 from `aspire.config.json`, so a Python AppHost gets the same live LikeC4 architecture
diagram, resource details, relationships and optional Entity Framework Core ER diagram as C#. How the
languages compare is in [AppHost languages](README.md).

> [!NOTE]
> Python AppHosts are **experimental** in Aspire 17. The CLI accepts one only when
> `features.experimentalPolyglotPython` is on, and reads that switch from the `aspire.config.json` in the
> working directory, so run the CLI from the AppHost's own folder.

## aspire.config.json

Declare AspireC4, and the integrations it draws, in `packages`, and turn the feature on:

```json
{
  "appHost": {
    "path": "apphost.py",
    "language": "python"
  },
  "sdk": {
    "version": "17.0.0"
  },
  "features": {
    "experimentalPolyglotPython": "true"
  },
  "packages": {
    "Aspire.Hosting.JavaScript": "17.0.0",
    "AspireC4.Hosting": "<version>"
  }
}
```

Replace `<version>` with the current [`AspireC4.Hosting` release on NuGet](https://www.nuget.org/packages/AspireC4.Hosting).
The repository sample points it at `../../src/src/AspireC4/AspireC4.csproj` instead, so it exercises the
local source. `aspire restore` writes the binding to `.aspire/modules/` as the `aspire_app` package and
creates the `.venv` the AppHost runs in (with uv when it is installed, otherwise pip).

## apphost.py

```python
from aspire_app import create_builder

with create_builder() as builder:
    builder.add_aspire_c4(
        configure=lambda opts: opts.with_title("My distributed application").with_view_title("Architecture")
    ).with_server_like_c4_details(configure=lambda opts: opts.with_label("Architecture diagram"))

    cache = builder.add_redis("cache")

    (
        builder.add_node_app("api", "../api", "index.ts")
        .with_like_c4_details(
            configure=lambda opts: opts.with_label("API").with_technology("Node.js").with_tag("backend")
        )
        # Draws the relationship and passes the connection string through Aspire's WithReference.
        .with_like_c4_connection_reference(
            cache, configure=lambda opts: opts.with_label("Caches sessions").with_kind("RESP")
        )
    )

    builder.run()
```

Options are keyword arguments, and callbacks receive the builder for the options or details they
configure. Python's code generator shortens some of Aspire's own method names, such as
`add_parameter_from_config`; AspireC4's methods keep their full names.

> [!IMPORTANT]
> Label the LikeC4 server node with `with_server_like_c4_details`, not `configure_server`. In the Python
> binding that callback receives a bare handle rather than a resource, so an AppHost that calls it fails
> at startup.

## Entity Framework Core diagrams

Declare `AspireC4.Hosting.EntityFrameworkCore`, and `Aspire.Hosting.Dotnet` to add the .NET project
that owns the `DbContext`, then run `aspire restore`:

```python
def er_diagram(opts):
    opts.title = "Order schema"

c4 = builder.add_aspire_c4()
c4.with_entity_framework_diagram(configure=er_diagram)

builder.add_dotnet_project("orders", "../Orders.Api/Orders.Api.csproj").with_entity_framework_model(
    label="Orders"
)
```

The ER diagram is built purely from .NET Entity Framework Core code, so it only ever draws .NET
projects, never a Python service. See [Entity Framework Core ER Diagrams](../Entity-Framework-Diagrams.md).

## Run the Python sample

The sample at `samples/python-app-host` builds the same graph as every other sample AppHost (see
[The samples share one contract](README.md#the-samples-share-one-contract)). Prerequisites are Docker,
the Aspire CLI, uv (or Python 3.11 or later with pip), and npm for the sample Node.js service. The
recipes run the CLI from inside the sample, where its feature switch is:

```shell
just py-restore   # aspire restore: bindings and .venv
just py-run       # aspire run
just py-lint      # ruff format and ruff check, through uvx
```

Generated output lands in `.aspirec4/` beside `apphost.py`, as beside a C# AppHost project; see
[AppHost languages](README.md#what-differs-between-them).

## Next pages

- [AppHost languages](README.md)
- [TypeScript AppHosts](TypeScript.md) and [Go AppHosts](Go.md)
- [Customizing Resources](../configuration/Customizing-Resources.md)
