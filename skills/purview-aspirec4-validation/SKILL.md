---
name: purview-aspirec4-validation
description: >-
  Use when an AspireC4 build reports an ASPIREC4xxx diagnostic, or when setting up the
  [LikeC4Registry] class that makes tags, kinds, groups and metadata keys refactor-safe - the nested
  class names, [KnownType] fields, the three severity levels, the AspireC4Strict MSBuild property,
  and validating hand-authored .c4 specification files.
metadata:
  product: AspireC4
  repository: purview-dev/aspirec4
---

# Source-generator validation

AspireC4 ships an incremental Roslyn generator, built on `Purview.SourceGeneratorFramework`, that
validates the **constant** values passed to `.WithTag()`, `.WithKind()`, `.WithLikeC4Group()` and
`.WithMetadata()`.

The registry attributes and enums are injected by `AspireC4.Hosting` itself — there is no separate
generator package to add. TypeScript AppHosts are not validated this way; they use the generated
fluent API.

## The registry class

**One `[LikeC4Registry]` class per AppHost assembly.** Its accessibility and nesting do not matter.
Values are `const string` fields in conventionally named nested classes:

```csharp
using Aspire.Hosting.AspireC4;

[LikeC4Registry]
internal static class ArchitectureRegistry
{
    public static class Tags
    {
        public const string External = "external";
        public const string LocalDevelopment = "local-dev";
    }

    public static class ElementKinds
    {
        public const string Service = "service";
    }

    public static class RelationshipKinds
    {
        public const string Async = "async";
    }

    public static class Groups
    {
        public const string Platform = "Platform";
    }

    public static class MetadataKeys
    {
        public const string AzureSku = "Azure_SKU";
    }
}
```

| Registry type | Accepted class names |
| --- | --- |
| Tag | `Tag`, `Tags` |
| Element kind | `ElementKind`, `ElementKinds`, `Element`, `Elements` |
| Relationship kind | `RelationshipKind`, `RelationshipKinds`, `Relationship`, `Relationships` |
| Group | `Group`, `Groups` |
| Metadata key | `MetadataKey`, `MetadataKeys` |

A nested class with any other name is `ASPIREC4007` — it is not silently ignored, because a typo'd
container would mean every value inside it was undeclared.

Then use the constants at the call sites, which is the whole point:

```csharp
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details => details
        .WithTag(ArchitectureRegistry.Tags.External)
        .WithKind(ArchitectureRegistry.ElementKinds.Service)
        .WithMetadata(ArchitectureRegistry.MetadataKeys.AzureSku, "Standard_LRS"))
    .WithLikeC4Group(ArchitectureRegistry.Groups.Platform);
```

## A flat registry

```csharp
[LikeC4Registry]
internal static class ArchitectureRegistry
{
    [KnownType(LikeC4RegistryType.Tag)]
    public const string External = "external";

    [KnownType(LikeC4RegistryType.Group, Strict = LikeC4Severity.Warning)]
    public const string Platform = "Platform";
}
```

**Do not declare one registry type both ways** — a named nested class *and* `[KnownType]` fields for
the same type is `ASPIREC4005`.

## Severity, from broadest to most specific

Without an explicit setting, a registry class enables **suggestion**-level validation. Three levels,
each overriding the one above:

1. the `AspireC4Strict` MSBuild property;
2. `[LikeC4Registry(Strict = …)]` on the registry;
3. `[Severity(…)]` on a nested class, or `KnownType.Strict` on an individual field.

```csharp
[LikeC4Registry(Strict = LikeC4Severity.Warning)]
internal static class ArchitectureRegistry
{
    [Severity(LikeC4Severity.Error)]
    public static class Tags
    {
        public const string External = "external";
    }

    [KnownType(LikeC4RegistryType.Group, Strict = LikeC4Severity.Off)]
    public const string UnvalidatedGroup = "Temporary";
}
```

`LikeC4Severity`: `Inherit`, `Off`, `Suggestion`, `Warning`, `Error`.

## `AspireC4Strict`

In the AppHost project, or `Directory.Build.props`:

```xml
<PropertyGroup>
  <AspireC4Strict>warning</AspireC4Strict>
</PropertyGroup>
```

| Value | Behaviour |
| --- | --- |
| `off`, or unset/unknown | DSL-file strict validation is disabled |
| `suggestion` | undeclared DSL values are suggestions |
| `warning` | undeclared DSL values are warnings |
| `error`, `true`, `yes`, `all` | undeclared DSL values are errors |
| `allincludingmetadata` | error-level, **including metadata keys** |

Metadata-key comparison is **case-insensitive and normalises punctuation and whitespace to
underscores**, so `Azure SKU`, `azure sku` and `Azure_SKU` are the same key.

To silence every AspireC4 generator diagnostic while keeping the injected registry types:

```xml
<PropertyGroup>
  <DisableAspireC4SourceGenerator>true</DisableAspireC4SourceGenerator>
</PropertyGroup>
```

## Validating hand-authored `.c4` files

Add them as compiler additional files, and set a strictness:

```xml
<ItemGroup>
  <AdditionalFiles Include="likec4/**/*.c4" />
  <AdditionalFiles Include="likec4/**/*.likec4" />
</ItemGroup>

<PropertyGroup>
  <AspireC4Strict>warning</AspireC4Strict>
</PropertyGroup>
```

Tags, element kinds and relationship kinds declared in `specification` blocks are **merged** with the
registry class's definitions, so a value declared in either place is valid in both.

## Diagnostics

| ID | Meaning |
| --- | --- |
| `ASPIREC4001` | a tag passed to `.WithTag()` is undeclared |
| `ASPIREC4002` | an element or relationship kind passed to `.WithKind()` is undeclared |
| `ASPIREC4003` | more than one class in the assembly has `[LikeC4Registry]` |
| `ASPIREC4004` | a group passed to `.WithLikeC4Group()` is undeclared |
| `ASPIREC4005` | a registry type uses both a nested class and `[KnownType]` fields |
| `ASPIREC4006` | a metadata key passed to `.WithMetadata()` is undeclared |
| `ASPIREC4007` | a nested class uses a name that is not a recognised registry type |

## Injected types — do not declare them

The generator emits `LikeC4RegistryAttribute.g.cs`, `KnownTypeAttribute.g.cs`,
`SeverityAttribute.g.cs`, `LikeC4RegistryType.g.cs` and `LikeC4Severity.g.cs`. Defining any of them
by hand collides with the generated copy.

## Related skills

`purview-aspirec4-resources` for the call sites these constants feed, and
`purview-aspirec4-configuration` for the project-wide options.
