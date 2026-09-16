# TypeScript AppHosts

AspireC4 supports both C# and TypeScript Aspire AppHosts. In a TypeScript AppHost, the Aspire integration exports the same diagram configuration, resource metadata, grouping, and relationship features through camel-cased asynchronous APIs. The C# registry source generator is not applied to TypeScript; TypeScript applications configure tags, kinds, groups, and metadata through the generated fluent API.

## Generated modules

The Aspire CLI generates the TypeScript API surface under `.aspire/modules/`. Import `createBuilder` from the generated Aspire module, then add AspireC4 to the builder:

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
    .configureServer(async (resource) => {
        resource.withLikeC4Details({
            configure: async (options) => {
                options.withLabel("Architecture diagram");
            },
        });
    });

const api = await builder.addNodeApp("api", "../api", "index.ts");

await api.withLikeC4Details({
    configure: async (options) => {
        options
            .withLabel("API")
            .withTechnology("Node.js")
            .withTag("backend");
    },
});

const app = await builder.build();
await app.run();
```

## aspire.config.json

Declare the integration and its Aspire dependencies in `aspire.config.json`:

```json
{
  "appHost": {
    "path": "apphost.mts",
    "language": "typescript/nodejs"
  },
  "sdk": {
    "version": "13.5.3"
  },
  "packages": {
    "Aspire.Hosting.JavaScript": "13.5.3",
    "AspireC4.Hosting": "13.5.3"
  }
}
```

Use the AspireC4 package version appropriate for the application. The repository sample points `AspireC4.Hosting` at `../../src/src/AspireC4/AspireC4.csproj` so it exercises the local source instead of a published package.

## Run the TypeScript sample

The sample at `samples/typescript-app-host` demonstrates:

- AspireC4 configuration from `apphost.mts`.
- Azure Redis and PostgreSQL resources running as local containers.
- Redis Commander and PgWeb dashboard resources.
- A TypeScript Node.js service with Redis and PostgreSQL references.
- LikeC4 labels, descriptions, links, icons, metadata, tags, groups, and relationships.
- Additional LikeC4 DSL and image folders from the repository `assets` directory.

Prerequisites are Docker, the Aspire CLI, and Bun. From the repository root:

```bash
cd samples/typescript-app-host
bun install
aspire restore
aspire start
```

`aspire restore` restores the integrations declared in `aspire.config.json` and regenerates `.aspire/modules/`. Once `aspire start` completes, open the Aspire dashboard URL printed by the CLI and select the LikeC4 resource or its architecture-diagram link. The sample also exposes the `node-app` `/health`, `/ping/redis`, and `/ping/postgres` endpoints through Aspire-assigned URLs.

For an interactive foreground session, the sample's Bun script is equivalent to `aspire run`:

```bash
bun run dev
```

Stop a background session with:

```bash
aspire stop
```

## Generated modules notes

Do not edit files under `.aspire/modules/`; Aspire owns and regenerates them. If the folder is missing or stale after a pull, clean, or branch switch, run:

```bash
aspire restore
```

When adding another Aspire integration, use `aspire add <package>` so Aspire updates `aspire.config.json` and regenerates the TypeScript API. Inspect `.aspire/modules/aspire.mts` to see the APIs currently available to `apphost.mts`.

## Next pages

- [Configuration](Configuration.md)
- [Customizing Resources](Customizing-Resources.md)