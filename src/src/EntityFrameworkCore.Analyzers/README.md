# AspireC4.EntityFrameworkCore

[![NuGet version](https://img.shields.io/nuget/v/AspireC4.EntityFrameworkCore.svg)](https://www.nuget.org/packages/AspireC4.EntityFrameworkCore)

A Roslyn source generator that records a project's Entity Framework Core model, **including fluent
`OnModelCreating` configuration**, so the AspireC4 ER diagram can draw it at full fidelity.

This package is **optional**. It is a companion to
[`AspireC4.Hosting.EntityFrameworkCore`](https://www.nuget.org/packages/AspireC4.Hosting.EntityFrameworkCore),
which goes in the AppHost and does the drawing. Start there if you have not set up the ER diagram yet.

## Why use it

Without this package, the AppHost reads your contexts from compiled metadata. That picks up conventions
and data annotations, but fluent configuration lives in a method body, and method bodies are not in
the metadata. Adding this package to the project that declares the `DbContext` captures that
configuration.

| Read from metadata only | With this package |
| --- | --- |
| `DbSet<T>`, navigations, `[Key]`, `[Table]`, `[Column]`, `[Required]`, `[MaxLength]`, `[Index]`, … | All of that, **plus** `HasDefaultSchema`, `ToTable`, `HasKey`, `Ignore`, `HasIndex(...).IsUnique()`, `HasColumnName`, `HasColumnType`, `HasMaxLength`, `IsRequired`, `HasComment` |

Both read value objects the same way — Purview's `[Scalar<T>]`, `[Scalar(typeof(T))]` and `[Scalar("Name")]`,
`IScalarValueObject<,>`, a bare `[ValueObject]`, `[ValueObject(EFMapping = Json)]`, EF's `[ComplexType]` and
`[Owned]` — along with value-object collections and C# unions. The attributes matter here: a Purview
generator writes the `Value` member into a generated file this analyzer cannot see, so a strongly-typed id
is recognised by its `[Scalar]` attribute. See
[Value objects, enums and unions](https://github.com/purview-dev/aspirec4/blob/main/docs/wiki/Entity-Framework-Diagrams.md#value-objects-enums-and-unions).
| Fidelity: `Approximate` | Fidelity: `Full` |

## Requirements

- **`netstandard2.0` analyzer, no runtime surface.** The package ships under `analyzers/dotnet/cs/`, so it
  loads into the compiler for whatever framework your project targets and adds nothing to your build
  output. The generated `[assembly: AspireC4EfModel]` attribute is compiled into your own assembly, which
  the AppHost then reads.
- **The .NET 11 SDK** to build the project. A source generator runs inside the compiler, and this one
  is built against Roslyn 5.9 — the project's target framework has no bearing on it. An older SDK
  reports `CS9057` and skips the generator, so the ER diagram falls back to the approximate metadata
  read. See the
  [requirements page](https://github.com/purview-dev/aspirec4/blob/main/docs/wiki/Requirements.md#the-compiler-floor-net-11-sdk).
- The project still has to be registered in the AppHost with `AddProject<Projects.X>(...)`, and the AppHost
  still has to call `WithEntityFrameworkDiagram()`.

## Installation

Add it to the project that **declares** your `DbContext`. That can be the service project or a class
library it references. Do not add it to the AppHost.

```bash
dotnet add package AspireC4.EntityFrameworkCore
```

That is the whole opt-in. There is no code to write and nothing to configure. The package is a
development dependency (`PrivateAssets="all"`), so it does not flow to the projects that reference
yours:

```xml
<PackageReference Include="AspireC4.EntityFrameworkCore" PrivateAssets="all" />
```

## How it works

At compile time the generator analyses every `DbContext` in the project and emits the model as a single
assembly attribute, `[assembly: AspireC4EfModel("<json>")]`. The AppHost reads that attribute straight
from the built assembly's metadata. None of your code is loaded or run.

- **No runtime dependency.** The package adds no assembly to your output, only the attribute in your own
  assembly.
- **No files written.** The generator only adds source, so builds stay deterministic and incremental.

## Diagnostics

All are `Info`. They point out where the diagram may be incomplete.

| ID | Meaning |
| --- | --- |
| `ASPIREC4008` | A `DbContext` was found but no entity types could be discovered |
| `ASPIREC4009` | An entity type has no discoverable primary key |
| `ASPIREC4010` | Configuration could not be interpreted statically, e.g. `ApplyConfigurationsFromAssembly` |

Configuration applied indirectly, such as `IEntityTypeConfiguration<T>` classes loaded with
`ApplyConfigurationsFromAssembly`, cannot be seen by a source generator. If you rely on it, use
[`AspireC4.EntityFrameworkCore.ModelDump`](https://www.nuget.org/packages/AspireC4.EntityFrameworkCore.ModelDump),
which builds the real model at build time.

## More information

- [Full documentation](https://purview.dev/docs/aspirec4/)
- [Repository and samples](https://github.com/purview-dev/aspirec4)
