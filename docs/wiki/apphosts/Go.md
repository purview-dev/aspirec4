# Go AppHosts

Use AspireC4 from an Aspire 17 Go AppHost (`apphost.go`): the Aspire CLI generates a Go binding for
AspireC4 from `aspire.config.json`, so a Go AppHost gets the same live LikeC4 architecture diagram,
resource details, relationships and optional Entity Framework Core ER diagram as C#. How the languages
compare is in [AppHost languages](README.md).

## aspire.config.json

Declare AspireC4, and the integrations it draws, in `packages`:

```json
{
  "appHost": {
    "path": "apphost.go",
    "language": "go"
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
The repository sample points it at `../../src/src/AspireC4/AspireC4.csproj` instead, so it exercises the
local source. `aspire restore` writes the binding to `.aspire/modules/`, which the sample's `go.mod`
maps to the `apphost/modules/aspire` package with a `replace` directive.

## apphost.go

```go
builder, err := aspire.CreateBuilder()
if err != nil {
	log.Fatal(aspire.FormatError(err))
}

builder.
	AddAspireC4(aspire.AddAspireC4Configure(func(opts aspire.AspireC4DiagramOptions) {
		opts.WithTitle("My distributed application").WithViewTitle("Architecture")
	})).
	WithServerLikeC4Details(&aspire.WithServerLikeC4DetailsOptions{
		Configure: func(opts aspire.LikeC4NodeDetailsAnnotation) {
			opts.WithLabel("Architecture diagram")
		},
	})

cache := builder.AddRedis("cache")

builder.AddNodeApp("api", "../api", "index.ts").
	WithLikeC4Details(&aspire.WithLikeC4DetailsOptions{
		Configure: func(opts aspire.LikeC4NodeDetailsAnnotation) {
			opts.WithLabel("API").WithTechnology("Node.js").WithTag("backend")
		},
	}).
	// Draws the relationship and passes the connection string through Aspire's WithReference.
	WithLikeC4ConnectionReference(cache, &aspire.WithLikeC4ConnectionReferenceOptions{
		Configure: func(opts aspire.LikeC4RelationshipDetailsAnnotation) {
			opts.WithLabel("Caches sessions").WithKind("RESP")
		},
	})

app, err := builder.Build()
if err != nil {
	log.Fatal(aspire.FormatError(err))
}
if err := app.Run(); err != nil {
	log.Fatal(aspire.FormatError(err))
}
```

Options are passed either as a single-field value, such as `aspire.AddAspireC4Configure(...)`, or as a
pointer to the method's options struct, such as `&aspire.WithLikeC4DetailsOptions{...}`; optional
fields take pointers (`Title: &title`). Errors are carried along the builder chain and reported by
`Build()`.

> [!IMPORTANT]
> Label the LikeC4 server node with `WithServerLikeC4Details`, not `ConfigureServer`. The Go binding
> cannot decode `ConfigureServer`'s `IResource` callback argument, so an AppHost that calls it fails at
> startup.

## Entity Framework Core diagrams

Declare `AspireC4.Hosting.EntityFrameworkCore`, and `Aspire.Hosting.Dotnet` to add the .NET project
that owns the `DbContext`, then run `aspire restore`:

```go
title, label := "Order schema", "Orders"

c4 := builder.AddAspireC4()
c4.WithEntityFrameworkDiagram(&aspire.WithEntityFrameworkDiagramOptions{
	Configure: func(opts aspire.AspireC4EfDiagramOptions) {
		opts.SetTitle(&title)
	},
})

builder.
	AddDotnetProject("orders", "../Orders.Api/Orders.Api.csproj").
	WithEntityFrameworkModel(&aspire.WithEntityFrameworkModelOptions{Label: &label})
```

The ER diagram is built purely from .NET Entity Framework Core code, so it only ever draws .NET
projects, never a Go service. See [Entity Framework Core ER Diagrams](../Entity-Framework-Diagrams.md).

## Run the Go sample

The sample at `samples/go-app-host` builds the same graph as every other sample AppHost (see
[The samples share one contract](README.md#the-samples-share-one-contract)). Prerequisites are Docker,
the Aspire CLI, Go, and npm for the sample Node.js service.

```shell
just go-restore   # aspire restore: generates .aspire/modules
just go-run       # aspire run
just go-lint      # gofmt and go vet
```

Generated output lands in `.aspirec4/` beside `apphost.go`, as beside a C# AppHost project; see
[AppHost languages](README.md#what-differs-between-them).

## Next pages

- [AppHost languages](README.md)
- [TypeScript AppHosts](TypeScript.md) and [Python AppHosts](Python.md)
- [Customizing Resources](../configuration/Customizing-Resources.md)
