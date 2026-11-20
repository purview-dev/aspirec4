---
name: purview-aspirec4-enrichment
description: >-
  Use when migrating an existing Aspire AppHost - written inline or composed with
  Purview.Aspire.ResourceKit kits and groups - onto a curated AspireC4 diagram, or when enriching
  one that already runs. Covers auditing the generated model.gen.c4, adding descriptions, summaries,
  technology, links and metadata to resources, turning ResourceKit groups into LikeC4 groups, tagging
  resources and styling them by tag or kind, and removing infrastructure that is hard to hide, such as
  Aspire 17's __dotnet-project-build, the *-rebuilder executables, ResourceKit group nodes and their
  'Parent' arrows.
metadata:
  product: AspireC4
  repository: purview-dev/aspirec4
---

# Migrating and enriching an AppHost's diagram

`AddAspireC4()` draws whatever is in the Aspire app model: every resource and every relationship.
An AppHost that was written before AspireC4 was added draws accurately but not usefully. Labels
are resource names, there is no prose, the build plumbing is on show, and dashboard-only structure
appears as boxes and arrows. This skill is the order of work for turning that into an architecture
diagram. `purview-aspirec4-resources` is the reference for each method used here.

## 1. Audit the generated model first

Run the AppHost once, then read `./.aspirec4/likec4/model.gen.c4` beside it (or look at
`./.aspirec4/images/architecture-index.png`). Sort every element into one of three buckets before
changing code:

| Bucket | Typical examples | Action |
| --- | --- | --- |
| Architecture | projects, databases, queues, storage, external APIs | enrich (step 2) |
| Dashboard structure | ResourceKit group nodes (`aspire-type 'Group'`), arrows labelled `'Parent'` | convert to LikeC4 groups (step 3) |
| Infrastructure or dev tooling | `__dotnet-project-build`, `<project>-rebuilder`, `<app>-installer`, DbGate, storage explorers | exclude (step 5) |

The generated element id is the resource name with every character other than a letter, digit or `_`
turned into `_`, so `minder-db` is `minder_db`. Those ids are what hand-written `.c4` files refer to.

## 2. Enrich each resource

Put **all** of a resource's details in **one** `WithLikeC4Details` call:

```csharp
builder.AddProject<Projects.Api>("api")
    .WithLikeC4Details(details => details
        .WithLabel("Minder API")
        .WithTechnology("ASP.NET Core minimal API")
        .WithSummary("Answers version questions from stored data")
        .WithDescription("""
            Read-only HTTP API over the support database.

            Unauthenticated until R-006, so it is reachable only inside the local host.
            """)
        .WithKind("service")
        .WithTag("backend")
        .WithLink("https://dev.azure.com/org/project/_wiki/wikis/api", "Runbook")
        .WithMetadata("owner", "InfoSec Engineering"));
```

- `WithLikeC4Details` **replaces** any earlier details annotation on the resource. Two calls, for
  example one in a shared helper and one in the AppHost, keep only the second. To add to details
  that are already there, amend the annotation instead:
  `resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault()?.WithTag("pii")`.
- The description is Markdown. The summary is one line, used in tooltips and the map view.
- Leave `WithTechnology` and `WithIcon` unset where inference is already right: `.NET` and
  `tech:dotnet` for projects, the image name for containers.
- Do not hand-write the source details. Every local project already gets `project-path` and its
  runtime version (`dotnet-target-framework`, `node-version`, …), and every container gets its full
  `container-image` reference. Set `WithMetadata("project-path", …)` only to override a detected value.
- Describe the arrows too, using `WithLikeC4Reference(target, r => r.WithLabel(...).WithTechnology(...))`.
  It annotates the relationship `WithReference` already created; it does not add a second arrow.
- Prefer registry constants to string literals for kinds, tags, groups and metadata keys, so a typo
  is a build diagnostic. See `purview-aspirec4-validation`.

### In a ResourceKit kit

A kit's resource is an ordinary `IResourceBuilder<T>`, so enrichment goes on the builder
`BuildResource` returns, or on `ResourceBuilder` in `ConfigureResource`. Choose **one** of the two for
each kit, because the details annotation replaces itself:

```csharp
[ResourceDefinition<DotnetProjectResource>(MinderResources.API, ProjectPath = AppHostProjects.API.Path)]
[InGroup<ServicesGroup>]
sealed partial class APIKit
{
    protected override void ConfigureResource()
    {
        ResourceBuilder
            .WithReference(HostKit.SqlServer.Database)
            .WithLikeC4Details(details => details
                .WithLabel("Minder API")
                .WithSummary("Answers version questions from stored data")
                .WithTag(ArchitectureRegistry.Tags.Backend))
            .WithLikeC4Reference(HostKit.SqlServer.Database, r => r.WithLabel("Reads support data"));
    }
}
```

A kit whose `BuildResource` is generated from `ProjectPath` has no `BuildResource` of its own, so its
enrichment always goes in `ConfigureResource`. The kit's XML summary is a good first draft of
`WithSummary`. AspireC4 cannot read it, so copy it across.

## 3. Turn ResourceKit groups into LikeC4 groups

ResourceKit adds one `ResourceKitGroupResource` per group in run mode. Each group's resources point
at it with a `"Parent"` relationship annotation, which is what the dashboard nests by. AspireC4 draws
both literally: a `system` box for each group, and an arrow labelled `'Parent'` from every member.

Exclude the group nodes. Because the arrows lead to an excluded resource, they disappear with them:

```csharp
builder.AddAspireC4(options => options
    .WithExcludedResourceType<Purview.Aspire.ResourceKit.ResourceKitGroupResource>());
```

Then carry the membership across as LikeC4 groups. Each resource can belong to one LikeC4 group.
`WithLikeC4Group` replaces any earlier group, and AspireC4 does not nest groups, so a resource goes
into its nearest ResourceKit group. Group by hand on each kit with
`ResourceBuilder.WithLikeC4Group("Services")`, or bridge them all once, after `AddAspireResourceKit()`
has returned. ResourceKit adds the group nodes at the end of its Configure phase, so they do not
exist earlier:

```csharp
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Purview.Aspire.ResourceKit;

static class LikeC4GroupBridge
{
    /// <summary>Puts each resource into a LikeC4 group named after its nearest ResourceKit group.</summary>
    public static IDistributedApplicationBuilder WithLikeC4GroupsFromResourceKit(
        this IDistributedApplicationBuilder builder,
        Func<string, string>? label = null)
    {
        foreach (var resource in builder.Resources)
        {
            // A child resource (a database under its server) is drawn nested in its parent; an
            // explicit WithLikeC4Group always wins.
            if (resource is ResourceKitGroupResource or IResourceWithParent
                || resource.Annotations.OfType<LikeC4GroupAnnotation>().Any())
                continue;

            var group = resource.Annotations.OfType<ResourceRelationshipAnnotation>()
                .Where(a => a.Type == "Parent")
                .Select(a => a.Resource)
                .OfType<ResourceKitGroupResource>()
                .FirstOrDefault();

            if (group is not null)
                resource.Annotations.Add(new LikeC4GroupAnnotation(label?.Invoke(group.Name) ?? group.Name));
        }

        return builder;
    }
}
```

```csharp
builder.AddAspireResourceKit();
builder.WithLikeC4GroupsFromResourceKit(name => name switch
{
    "services" => "Services",
    "commands" => "Commands",
    "persistence" => "Persistence",
    _ => name,
});
```

- A group node is named after the group, or `<name>-group` when a resource already has that name, so
  match both forms in `label`.
- A nested ResourceKit group (`[ResourceGroup<MinderGroup>]`) becomes a flat LikeC4 group. To draw
  the outer level, write a view in `likec4-extensions/` (step 4).
- Setting `ShowInDashboard = false` on a ResourceKit group also removes its node from the diagram,
  but it removes the dashboard nesting too. Excluding the type keeps both.
- `'Parent'` arrows between two visible resources remain, for example `queues-explorer -> queues`
  and `docs-installer -> docs`. An integration that hangs a tool off a resource creates them. Exclude
  the tool (step 5), or relabel the arrow with `WithLikeC4Reference(target, r => r.WithLabel(...))`.

## 4. Tag, then style

Tags are declared for you. The generated `specification` block lists every tag a resource or
relationship carries, so **do not redeclare them** in a hand-written file. For the same reason, a
style rule can only name a tag that some resource currently carries. A rule for a tag that is not in
use names an undeclared tag, and LikeC4 rejects it.

There are three ways to style:

| Need | Use |
| --- | --- |
| Every element of a kind looks the same, in every view | `WithElementKindSpec(new LikeC4ElementKindSpec("service").WithStyle(new(Shape: "rectangle", Color: "indigo", Icon: null, Border: null, Opacity: null)))` with `WithKind("service")` on the resources |
| Style the generated view by tag | a view in `likec4-extensions/` that **extends** the generated one, then `WithDefaultViewId` so the dashboard opens it |
| Recolour by run state | `WithStateTag(state, tag)` and `WithDefaultStateStyles(false)`. See `purview-aspirec4-configuration` |

```likec4
// likec4-extensions/views.c4 - styles the generated `index` view by tag
views {
  view styled extends index {
    title 'Architecture'
    style element.tag = #backend {
      color indigo
    }
    style element.tag = #external {
      color amber
      border dashed
    }
  }
}
```

```csharp
builder.AddAspireC4(options => options.WithDefaultViewId("styled"));
```

The generated view cannot be edited, because it is rewritten on every change. Extending it keeps its
groups and run-state styles and adds yours. Every view is exported as an image, so `styled` gets its
own `architecture-styled.png`. Validate a hand-written file with
`likec4 validate --json --no-layout --file <file> <project-dir>` (the `likec4-dsl` skill covers the
DSL). For a resource that a hand-authored LikeC4 project already describes, map it with
`WithLikeC4Element("cloud.backend.api")` instead of enriching it twice. See
`purview-aspirec4-resources`.

## 5. Remove what is hard to hide

The usual tools come first: `ExcludeFromLikeC4()` where you add a resource, and
`WithExcludedResourceType(...)` for a whole type. These cases need a little more:

| Resource | Why it shows | Remove it with |
| --- | --- | --- |
| `__dotnet-project-build` (Aspire 17) | Aspire adds it to build every project before start, as a plain `ExecutableResource` with no type of its own. Excluding `ExecutableResource` would hide your real executables too | by name, below |
| `<project>-rebuilder` (Aspire 17) | Aspire's internal `ProjectRebuilderResource`, nested inside each project | `WithExcludedResourceType("Aspire.Hosting.ApplicationModel.ProjectRebuilderResource")` |
| `<app>-installer` | `Aspire.Hosting.JavaScript.JavaScriptInstallerResource`, added by the JavaScript integrations | `WithExcludedResourceType<JavaScriptInstallerResource>()` |
| ResourceKit group nodes | see step 3 | `WithExcludedResourceType<ResourceKitGroupResource>()` |
| A tool an integration adds (DbGate, pgweb, storage explorers) | it is a resource of its own | `ExcludeFromLikeC4()` in the integration's callback: `sql.WithDbGate(g => g.ExcludeFromLikeC4())` |

AspireC4 skips a resource that is hidden in its **initial** snapshot. Aspire hides the project build
in the dashboard but not in that snapshot, so the build is still drawn. Exclude it by name, after the
last project has been added, which is just before `builder.Build()`:

```csharp
static IDistributedApplicationBuilder ExcludeAspireBuildFromLikeC4(this IDistributedApplicationBuilder builder)
{
    // Absent when nothing needs building, or when an IDE builds the projects.
    var build = builder.Resources.FirstOrDefault(r => r.Name == "__dotnet-project-build");
    if (build is not null)
        builder.CreateResourceBuilder(build).ExcludeFromLikeC4();

    return builder;
}
```

```csharp
builder.AddAspireResourceKit();        // or every AddProject / AddDotnetProject call
builder.ExcludeAspireBuildFromLikeC4();
builder.Build().Run();
```

`ExcludeFromLikeC4()` and type exclusion only remove the resource from the diagram. The dashboard,
the manifest and the resource itself are unchanged.

> [!WARNING]
> `__dotnet-project-build` and `ProjectRebuilderResource` are Aspire internals, so a rename in an
> Aspire release quietly brings them back. After an Aspire upgrade, check `model.gen.c4` again.

## Where the configuration goes in a ResourceKit AppHost

When AspireC4 is itself a kit (`[ResourceDefinition<AspireC4Resource>(...)]`, building with
`builder.AddAspireC4().WithEntityFrameworkDiagram()`), the options callback goes inside that kit's
`BuildResource`:

```csharp
protected override IResourceBuilder<AspireC4Resource> BuildResource(IDistributedApplicationBuilder builder) =>
    builder
        .AddAspireC4(options => options
            .WithTitle("Minder")
            .WithExcludedResourceType<ResourceKitGroupResource>()
            .WithExcludedResourceType<JavaScriptInstallerResource>()
            .WithExcludedResourceType("Aspire.Hosting.ApplicationModel.ProjectRebuilderResource")
            .WithDefaultViewId("styled"))
        .WithEntityFrameworkDiagram();
```

The group bridge and the build exclusion operate on the whole app model, so they belong in
`Program.cs` after `AddAspireResourceKit()`, not in a kit.

## Checklist

1. Audit `model.gen.c4` and sort every element into a bucket.
2. One `WithLikeC4Details` per resource: label, summary, description, and tags for styling.
3. Exclude `ResourceKitGroupResource` and bridge or hand-assign LikeC4 groups.
4. Exclude the build, the rebuilders, the installers and the dev tools.
5. Style by kind (`WithElementKindSpec`) or by tag (an `extends index` view plus `WithDefaultViewId`).
6. Run again, then reread `model.gen.c4` and the exported image.

## Related skills

`purview-aspirec4-resources` for every node and relationship method, `purview-aspirec4-configuration`
for exclusion, kind specs, extensions and view options, `purview-aspirec4-validation` for registry
constants, and `purview-aspirec4-quickstart` to get the diagram running at all.
