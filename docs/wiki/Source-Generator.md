# Source Generator Validation

AspireC4 includes an incremental Roslyn source generator (built on `Purview.SourceGeneratorFramework`) that validates constant values passed to:

- `.WithTag()`
- `.WithKind()`
- `.WithLikeC4Group()`
- `.WithMetadata()`

The generator injects the registry attributes and enums automatically — the `AspireC4.Hosting` package references it, so no separate generator package is needed. TypeScript AppHosts are not validated this way; they use the generated fluent API.

## Registry class

Add one `[LikeC4Registry]` class to the AppHost assembly. Its accessibility and nesting do not matter. Values are declared as `const string` fields in conventionally named nested classes:

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

Supported nested-class names are:

| Registry type | Accepted class names |
| --- | --- |
| Tag | `Tag`, `Tags` |
| Element kind | `ElementKind`, `ElementKinds`, `Element`, `Elements` |
| Relationship kind | `RelationshipKind`, `RelationshipKinds`, `Relationship`, `Relationships` |
| Group | `Group`, `Groups` |
| Metadata key | `MetadataKey`, `MetadataKeys` |

Use the constants at call sites to make refactoring safe:

```csharp
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details =>
        details
            .WithTag(ArchitectureRegistry.Tags.External)
            .WithKind(ArchitectureRegistry.ElementKinds.Service)
            .WithMetadata(ArchitectureRegistry.MetadataKeys.AzureSku, "Standard_LRS")
    )
    .WithLikeC4Group(ArchitectureRegistry.Groups.Platform);
```

## Individual registry fields

For a flat registry, annotate each constant with `[KnownType]`:

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

Do not declare the same registry type using both a named nested class and `[KnownType]` fields. Doing so produces `ASPIREC4005`.

## Validation severity

Without an explicit strict setting, a registry class enables suggestion-level validation. Severity can be configured at three levels, from broadest to most specific:

1. The `AspireC4Strict` MSBuild property.
2. `[LikeC4Registry(Strict = ...)]` for the registry.
3. `[Severity(...)]` on a named nested class, or `KnownType.Strict` on an individual field.

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

`LikeC4Severity` supports `Inherit`, `Off`, `Suggestion`, `Warning`, and `Error`.

## The `AspireC4Strict` MSBuild property

The project-wide setting can be placed in the AppHost project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <AspireC4Strict>warning</AspireC4Strict>
</PropertyGroup>
```

Accepted values:

| Value | Behavior |
| --- | --- |
| `off` or an unset/unknown value | Disables DSL-file strict validation |
| `suggestion` | Reports undeclared DSL values as suggestions |
| `warning` | Reports undeclared DSL values as warnings |
| `error`, `true`, `yes`, or `all` | Reports undeclared DSL values as errors |
| `allincludingmetadata` | Error-level validation including metadata keys |

Metadata-key comparison is case-insensitive and normalizes punctuation and whitespace to underscores. For example, `Azure SKU`, `azure sku`, and `Azure_SKU` identify the same key.

To disable all AspireC4 source-generator diagnostics while retaining the injected registry types:

```xml
<PropertyGroup>
  <DisableAspireC4SourceGenerator>true</DisableAspireC4SourceGenerator>
</PropertyGroup>
```

## Validate LikeC4 files

Add `.c4` or `.likec4` specification files as compiler additional files, then set `AspireC4Strict`:

```xml
<ItemGroup>
  <AdditionalFiles Include="likec4/**/*.c4" />
  <AdditionalFiles Include="likec4/**/*.likec4" />
</ItemGroup>

<PropertyGroup>
  <AspireC4Strict>warning</AspireC4Strict>
</PropertyGroup>
```

Tags, element kinds, and relationship kinds in `specification` blocks are merged with registry-class definitions.

## Diagnostics

| ID | Meaning |
| --- | --- |
| `ASPIREC4001` | A tag passed to `.WithTag()` is undeclared |
| `ASPIREC4002` | An element or relationship kind passed to `.WithKind()` is undeclared |
| `ASPIREC4003` | More than one class in the assembly has `[LikeC4Registry]` |
| `ASPIREC4004` | A group passed to `.WithLikeC4Group()` is undeclared |
| `ASPIREC4005` | A registry type uses both a nested class and `[KnownType]` fields |
| `ASPIREC4006` | A metadata key passed to `.WithMetadata()` is undeclared |
| `ASPIREC4007` | A nested class in the registry uses a name that is not a recognized registry type |

## Injected types

The generator emits `LikeC4RegistryAttribute.g.cs`, `KnownTypeAttribute.g.cs`, `SeverityAttribute.g.cs`, `LikeC4RegistryType.g.cs`, and `LikeC4Severity.g.cs`. Do not define these types manually in your own code.

## Next pages

- [Migration Guide](Migration.md)
- [Customizing Resources](Customizing-Resources.md)