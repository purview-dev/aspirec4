# TypeScript AppHosts

Use AspireC4 from an Aspire TypeScript AppHost (`apphost.mts`): the same live LikeC4 architecture
diagram, resource customisation and optional Entity Framework Core ER diagram as a C# AppHost, through
the TypeScript API the Aspire CLI generates. How the languages compare is in
[AppHost languages](README.md).

The C# registry source generator does not run here, because a TypeScript AppHost does not compile C#;
configure tags, kinds, groups and metadata through the generated fluent API instead.

## aspire.config.json

Declare AspireC4, and the integrations it draws, in `aspire.config.json`:

```json
{
  "appHost": {
    "path": "apphost.mts",
    "language": "typescript/nodejs"
  },
  "sdk": {
    "version": "17.0.0"
  },
  "packages": {
    "Aspire.Hosting.JavaScript": "17.0.0",
    "AspireC4.Hosting": "<version>"
  }
}
```

Replace `<version>` with the current [`AspireC4.Hosting` release on NuGet](https://www.nuget.org/packages/AspireC4.Hosting).
The Aspire pins must be at least 13.6.5, the minimum in [Requirements](../Requirements.md#aspire). The
repository sample points `AspireC4.Hosting` at `../../src/src/AspireC4/AspireC4.csproj`, so it exercises
the local source instead of a published package.

## apphost.mts

`aspire restore` generates the TypeScript API under `.aspire/modules/`. Import `createBuilder` from it
and add AspireC4 to the builder:

```typescript
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

await builder
	.addAspireC4({
		configure: async (options) => {
			options
				.withTitle("My distributed application")
				.withViewTitle("Architecture")
				.withViewDescription("Generated from the Aspire resource graph");
		},
	})
	.withServerLikeC4Details({
		configure: async (options) => {
			options.withLabel("Architecture diagram");
		},
	});

const cache = await builder.addRedis("cache");

await builder
	.addNodeApp("api", "../api", "index.ts")
	.withLikeC4Details({
		configure: async (options) => {
			options.withLabel("API").withTechnology("Node.js").withTag("backend");
		},
	})
	// Draws the relationship and passes the connection string through Aspire's WithReference.
	.withLikeC4ConnectionReference(cache, {
		configure: async (options) => {
			await options.withLabel("Caches sessions").withKind("RESP");
		},
	});

const app = await builder.build();
await app.run();
```

The method names match C# — `withLink`, `withMetadata`, `withLikeC4ConnectionReference` — so the
[Customizing Resources](../configuration/Customizing-Resources.md) reference applies as written. `configureServer`
also works in TypeScript, but `withServerLikeC4Details` is the form every AppHost language shares.

## Generated modules

Do not edit files under `.aspire/modules/`; Aspire owns and regenerates them. If the folder is missing or
stale after a pull, clean or branch switch, run:

```shell
aspire restore
```

Add another Aspire integration with `aspire add <package>`, so Aspire updates `aspire.config.json` and
regenerates the API. `.aspire/modules/aspire.mts` shows what is currently available to `apphost.mts`.

## Entity Framework Core diagrams

The ER diagram is an optional second diagram and is additive, exactly as in a C# AppHost:
`addAspireC4()` is the base, and the ER diagram is added on top of it. The AppHost's language does not
change what it reads: the ER diagram is built purely from .NET Entity Framework Core code, so it only
ever draws the .NET project resources in the graph, never a Node.js service. Declare the package, and
`Aspire.Hosting.Dotnet` to add the .NET project, then run `aspire restore`:

```json
{
  "packages": {
    "Aspire.Hosting.Dotnet": "17.0.0",
    "Aspire.Hosting.JavaScript": "17.0.0",
    "AspireC4.Hosting": "<version>",
    "AspireC4.Hosting.EntityFrameworkCore": "<version>"
  }
}
```

```typescript
const c4 = builder.addAspireC4();

await c4.withEntityFrameworkDiagram({
	configure: async (options) => {
		await options.title.set("Order schema");
	},
});

await builder
	.addDotnetProject("orders", "../Orders.Api/Orders.Api.csproj")
	.withEntityFrameworkModel({ label: "Orders" });
```

The per-service fidelity packages (`AspireC4.EntityFrameworkCore` and
`AspireC4.EntityFrameworkCore.ModelDump`) are C# project references, so they apply to whichever .NET
project declares the `DbContext`. See [Entity Framework Core ER Diagrams](../Entity-Framework-Diagrams.md).

## Run the TypeScript sample

The sample at `samples/typescript-app-host` builds the same graph as every other sample AppHost (see
[The samples share one contract](README.md#the-samples-share-one-contract)): Azure Redis and Postgres as
local containers, Redis Commander and PgWeb, a local dev group, a Node.js service connected to all four
stores, and the .NET data service with its ER diagram.

Prerequisites are Docker, the Aspire CLI, Bun for the AppHost, and npm for the sample Node.js service.
From the repository root:

```shell
just ts-restore   # bun install and aspire restore
just ts-run       # aspire run
```

Or by hand, from the sample folder:

```shell
cd samples/typescript-app-host
bun install
aspire restore
aspire start
```

Open the Aspire dashboard URL the CLI prints, and select the AspireC4 resource or its
architecture-diagram link. Stop a background session with `aspire stop`.

## Output paths and legacy mode

The architecture diagram is written to `.aspirec4/likec4/`, the ER diagram to `.aspirec4/erd/` and the
exported images to `.aspirec4/images/`, all beside `apphost.mts`, exactly as beside a C# AppHost project.
Relative paths are resolved against the AppHost's folder, not the working directory of the Aspire CLI's
AppHost server, which is under `.aspire/`. Set `withOutputDirectory`, or the `AspireC4__OutputDirectory`
environment variable, to write the architecture diagram elsewhere.

If you are upgrading from 13.5 and need the pre-13.6 layout unchanged, call `withLegacyLayout()` inside
the `configure` callback:

```typescript
await builder.addAspireC4({
	configure: async (options) => {
		options.withLegacyLayout();
	},
});
```

That restores `./likec4/gen/` and `./erd/gen/` and keeps the dashboard rows flat as `aspirec4` and
`aspirec4-er`. An explicit output directory still wins. See [Migration](../Migration.md) for the full list
of breaking changes, including the TypeScript method renames.

## Next pages

- [AppHost languages](README.md)
- [Go AppHosts](Go.md) and [Python AppHosts](Python.md)
- [Configuration](../configuration/README.md)
- [Customizing Resources](../configuration/Customizing-Resources.md)
