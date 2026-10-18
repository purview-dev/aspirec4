# Migration Guide

## 13.6 — generated output and dashboard names

> [!IMPORTANT]
> These are **breaking changes**. If you need to upgrade without touching anything else, one switch
> restores the previous behaviour in full — see
> [Legacy mode](#legacy-mode-keeping-the-pre-136-behaviour) below. It is a compatibility shim, not a
> supported alternative layout, and will be removed in a future major version.

> [!TIP]
> **In a hurry?** Set **legacy mode** with
> `builder.AddAspireC4(configure: options => options.WithLegacyLayout())` or
> `"AspireC4": { "UseLegacyLayout": true }` and stop reading. Everything else on this page describes
> the new layout; see [Migrating off the shim](#migrating-off-the-shim) for the steps to adopt it
> later.

### What changed

**1. Generated diagrams moved under a single root.**

| | Before 13.6 | 13.6 |
| --- | --- | --- |
| `AspireC4DiagramOptions.OutputDirectory` | `./likec4/gen/` | `./.aspirec4/likec4/` |
| `AspireC4EfDiagramOptions.OutputDirectory` | `./erd/gen/` | `./.aspirec4/erd/` |

Everything AspireC4 generates now sits beneath one `.aspirec4/` directory in the AppHost, so there is a
single entry to ignore in source control regardless of how many diagrams are enabled:

```gitignore
# Replaces **/likec4/gen/, **/erd/gen/ and the **/erd/gen/vendor/ entries.
**/.aspirec4/
```

Delete the stale `likec4/gen/` and `erd/gen/` directories after upgrading — nothing writes to them any
more, so they will sit there as untracked leftovers.

**2. The diagram resources nest under a shared dashboard parent.**

This only applies when more than one diagram is registered. With just `AddAspireC4()`, the dashboard is
unchanged: a single `aspirec4` row, exactly as before.

```text
Before 13.6                   13.6, with WithEntityFrameworkDiagram()
aspirec4                      aspirec4                 <- parent row
aspirec4-er                   ├── aspirec4-likec4      <- renamed
aspirec4-er-server            └── aspirec4-er
                                  └── aspirec4-er-server
```

The architecture diagram's resource is renamed from `aspirec4` to `aspirec4-likec4`, so **anything that
addresses it by name needs updating** — integration tests, `ResourceNotificationService` watchers,
`GetResource("aspirec4")` lookups, and anything matching on dashboard resource names. If you passed a
name to `AddAspireC4("my-diagram")`, the same applies: the parent takes `my-diagram` and the diagram
becomes `my-diagram-likec4`.

The inner server resources (`aspirec4-server`, `aspirec4-er-server`) keep their names, which are derived
from the name you supplied rather than the renamed row.

### Legacy mode (keeping the pre-13.6 behaviour)

`WithLegacyLayout()` reverts both changes together — the output paths *and* the dashboard names. This
is the switch to reach for when an upgrade must change nothing else:

```csharp
builder.AddAspireC4(configure: options => options.WithLegacyLayout());
```

or, without touching code:

```json
{
  "AspireC4": {
    "UseLegacyLayout": true
  }
}
```

The same setting works as an environment variable when you cannot edit `appsettings.json`:

```text
AspireC4__UseLegacyLayout=true
```

One switch covers the whole integration: the ER diagram follows the architecture diagram rather than
exposing a second setting that could disagree with it. With it set, `./likec4/gen/` and `./erd/gen/` are
the defaults again, and the diagrams stay flat as `aspirec4` and `aspirec4-er` with no parent row.

Notes:

- An `OutputDirectory` you set yourself always wins over either default, legacy or not.
- One switch covers both diagrams: there is no separate ER setting, so the architecture and ER
  diagrams cannot disagree about which layout is in use.
- It has no effect on `ProjectDirectory`. Pointing AspireC4 at a hand-authored LikeC4 project always
  writes generated output beneath the project being served, because it has to.
- Unlike most options, this one is read from configuration when the builder runs, not only when the
  runtime options are resolved — it fixes the paths and resource names before anything else starts, so
  an `appsettings` override has to be visible that early or the server would mount one directory while
  the model was written to another.

### Migrating off the shim

1. Remove `WithLegacyLayout()` (or the configuration entry).
2. Replace the `.gitignore` entries with `**/.aspirec4/` and delete the old directories.
3. Update anything that refers to the `aspirec4` resource by name to `aspirec4-likec4`, but only if you
   register a second diagram.

Once legacy mode is off, `./likec4/gen/` and `./erd/gen/` are no longer written. Delete them so they do
not linger as untracked leftovers.

## Source generator migration

This section covers breaking changes introduced by the migration of the source generator to the current `Purview.SourceGeneratorFramework` incremental APIs. Existing applications should review the following when upgrading:

- **Aspire AppHost dependency is explicit.** Aspire 13.5 AppHosts must reference `Aspire.Hosting.AppHost`; relying on the AppHost SDK alone produces `ASPIRE002`.
- **Only one registry class is supported per assembly.** Merge multiple `[LikeC4Registry]` classes into one class.
- **Registry declaration styles cannot be mixed per type.** For example, choose either a `Tags` nested class or `[KnownType(LikeC4RegistryType.Tag)]` fields. Mixing both now produces `ASPIREC4005`.
- **Strict settings are severity-based.** Replace older boolean-only assumptions with `suggestion`, `warning`, `error`, `all`, or `allincludingmetadata`. `true` remains accepted as an alias for error-level validation.
- **Metadata validation is opt-in at the global level.** Use `allincludingmetadata`, or apply an explicit metadata severity through `[Severity]`/`[KnownType]`. Plain `all` does not validate metadata keys.
- **Generated source files are split by type.** The generator now emits `LikeC4RegistryAttribute.g.cs`, `KnownTypeAttribute.g.cs`, `SeverityAttribute.g.cs`, `LikeC4RegistryType.g.cs`, and `LikeC4Severity.g.cs` instead of a combined `LikeC4RegistryAttributes.g.cs`. This affects generator snapshot tests and tooling that inspected hint names; normal application source code is unaffected.
- **Do not define generated registry types manually.** Remove compatibility copies of `LikeC4RegistryAttribute`, `KnownTypeAttribute`, `SeverityAttribute`, `LikeC4RegistryType`, or `LikeC4Severity` to avoid duplicate-type errors.
- **Generator packaging is automatic.** Consumers should reference only `AspireC4.Hosting`; remove direct references to `AspireC4.SourceGenerators` or `Purview.SourceGeneratorFramework` that were added solely to make the AspireC4 generator run.
- **TypeScript APIs are generated by Aspire.** TypeScript AppHosts import from `.aspire/modules/aspire.mjs`; generated files must not be copied between projects or edited manually. Run `aspire restore` after upgrading AspireC4 so the exported API matches the installed integration version.

## See also

- [Configuration](Configuration.md)
- [Generated Output](Generated-Output.md)
- [Source Generator Validation](Source-Generator.md)
- [TypeScript AppHosts](Typescript-AppHost.md)