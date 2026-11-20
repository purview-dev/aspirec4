# ER Diagram Styling, Layout and Descriptions

The Entity Framework Core ER diagram can be styled by table, column, type and relationship. You can
also change how it is laid out and how it moves, and show a description for each table and column.
This page owns that behaviour; [Entity Framework Core ER Diagrams](../Entity-Framework-Diagrams.md)
covers enabling the diagram itself.

Everything here is set on `AspireC4EfDiagramOptions`, in the `configure` callback of
`WithEntityFrameworkDiagram()` or in the `AspireC4:EntityFramework` configuration section:

```csharp
builder.AddAspireC4()
       .WithEntityFrameworkDiagram(configure: options => options
           .WithTableStyle("Order*", style => style.WithAccent("#dc2626", "#f87171").WithBadge("SALES"))
           .WithTypeStyle("decimal", style => style.WithAccent("#16a34a"))
           .WithColumnStyle("Email", style => style.WithBadge("PII").WithTooltip("Personal data"))
           .WithLayout(ErdLayoutDirection.Right, layerSpacing: 160)
           .WithEdgeStyle(ErdEdgeStyle.SmoothStep)
           .WithDescriptions()
           .WithLegend());
```

The interactive viewer applies all of it. Mermaid, d2 and DBML apply what their formats can express,
listed in [What each renderer applies](#what-each-renderer-applies).

### From TypeScript, Go and Python

The same methods are on the options object a polyglot AppHost's `withEntityFrameworkDiagram` callback
receives, in each language's spelling. The style callbacks take an `ErdElementStyle` or `ErdStyleRule`
handle, and optional arguments become an options object, an options struct or keyword arguments:

```typescript
await c4.withEntityFrameworkDiagram({
	configure: async (opts) => {
		await opts
			.withTableStyle("Order*", async (s) => {
				await s.withAccent("#dc2626", { dark: "#f87171" }).withBadge("SALES");
			})
			.withTypeStyle("decimal", async (s) => {
				await s.withAccent("#16a34a");
			})
			.withContextPalette(["#2563eb", "#16a34a", "#d97706"])
			.withLayout(ErdLayoutDirection.Right, { layerSpacing: 160 })
			.withEdgeStyle(ErdEdgeStyle.SmoothStep)
			.withDescriptions()
			.withLegend();
	},
});
```

```go
c4.WithEntityFrameworkDiagram(&aspire.WithEntityFrameworkDiagramOptions{
	Configure: func(opts aspire.AspireC4EfDiagramOptions) {
		opts.
			WithTableStyle("Order*", func(s aspire.ErdElementStyle) {
				s.WithAccent("#dc2626", &aspire.WithAccentOptions{Dark: ptr("#f87171")}).WithBadge("SALES")
			}).
			WithContextPalette([]string{"#2563eb", "#16a34a", "#d97706"}).
			WithLayout(aspire.ErdLayoutDirectionRight, &aspire.WithLayoutOptions{LayerSpacing: ptr(160)}).
			WithDescriptions()
	},
})
```

```python
def er_diagram(opts: AspireC4EfDiagramOptions) -> None:
    opts.with_table_style("Order*", order_style).with_layout("Right", layer_spacing=160).with_descriptions()


def order_style(s: ErdElementStyle) -> None:
    s.with_accent("#dc2626", dark="#f87171").with_badge("SALES")
```

Three things are C#-only, because a binding cannot express them:

- the [style callbacks](#style-callbacks) `OnTableStyle`, `OnColumnStyle` and `OnRelationshipStyle`, which
  run against model types the bindings do not see;
- an `ErdStyleRule` built up front and passed to `WithStyle(ErdStyleRule)`; use the callback form,
  `withStyle(rule => …)`;
- [context colours](#context-colours) with a separate dark-theme value. `withContextPalette` takes
  strings, each used in both themes; it is `WithContextPaletteColours` in C#.

Rules and light-and-dark context colours can still be set from any language through
[configuration](#styles-from-configuration), which every AppHost reads; the callbacks cannot, since they
are code.

> [!TIP]
> The sample AppHost applies one working example of every option on this page to its own tables, in
> [`SampleErdStyling.cs`](https://github.com/purview-dev/aspirec4/blob/main/src/src/TestAppHost/SampleErdStyling.cs).
> Run `dotnet run --project src/src/TestAppHost` and open the ER diagram to see the result.

## Styling

### Style rules

A rule matches elements by pattern and gives each match a style. Each pattern is optional, and a
pattern you leave unset matches anything. Patterns are case-insensitive: `*` matches any run of
characters and `?` matches one.

| Pattern | Matches |
| --- | --- |
| `Context` | the `DbContext`'s short or full type name |
| `Table` | the table name, or the entity's short or full CLR type name |
| `Column` | the column name |
| `Type` | the column's store type with or without its size (`decimal` and `decimal(18,2)` both match `decimal(18,2)`), its domain type (`OrderId`), its CLR type, or its kind of domain type (`enum`, `union`, `ValueObject`) |

The shortcuts cover the common cases:

```csharp
options.WithTableStyle("Audit*", s => s.WithLineStyle(ErdLineStyle.Dashed));
options.WithColumnStyle("*Id", s => s.WithForeground("#64748b"));
options.WithColumnStyle("Email", s => s.WithBadge("PII"), table: "Customers");
options.WithTypeStyle("enum", s => s.WithItalic());
```

`WithStyle` takes any combination of patterns:

```csharp
// The decimal columns of every order table
options.WithStyle(rule => rule.ForTable("Order*").ForType("decimal").WithAccent("#b45309", "#f59e0b"));

// Every table that has a TenantId column, rather than the column itself
options.WithStyle(rule => rule.ForColumn("TenantId").ForTables().WithBadge("TENANT"));

// Every relationship with Customers at either end
options.WithStyle(rule => rule.ForTable("Customers").ForRelationships().WithLineStyle(ErdLineStyle.Dotted));
```

What a rule styles depends on its target:

| Target | Set by | Styles |
| --- | --- | --- |
| `Auto` (the default) | `WithTableStyle`, `WithColumnStyle`, `WithTypeStyle` | columns when the rule names a column or a type, otherwise tables |
| `Table` | `ForTables()` | tables; a column or type pattern then selects the tables that have a matching column |
| `Column` | `Target = ErdStyleTarget.Column` | columns |
| `Relationship` | `ForRelationships()` | relationships with a matching table at either end and a matching foreign-key column |

Rules apply in the order you add them. When several rules match one element, a later rule overrides
only the properties it sets, so a broad rule followed by a narrow one works as you would expect:

```csharp
options
    .WithTableStyle("*", s => s.WithBold())                 // every table bold
    .WithTableStyle("Orders", s => s.WithAccent("#dc2626")); // Orders also red, and still bold
```

### What a style sets

| Property | Fluent method | Table | Column | Relationship |
| --- | --- | --- | --- | --- |
| `Accent` | `WithAccent(light, dark?)` | header band and border | bar beside the row | line colour |
| `Background` | `WithBackground(light, dark?)` | card body | row | label |
| `Foreground` | `WithForeground(light, dark?)` | header text | column name | label text |
| `Bold` / `Italic` | `WithBold()` / `WithItalic()` | header text | column name | label text |
| `LineStyle` | `WithLineStyle(Solid \| Dashed \| Dotted)` | border | not used | line |
| `Badge` | `WithBadge("PII")` | shown in the header | shown beside the key badges | not used |
| `Tooltip` | `WithTooltip("…")` | added to the header's tooltip | added to the row's tooltip | not used |

A table's accent replaces its context colour on the card. The relationships that start from it, where
it is the principal, take the accent too, unless a relationship rule sets their colour. The context container keeps the context's colour.

### Colours for light and dark

Every colour has a light value and a dark value. Give one value to use it in both themes, or both to
have the viewer switch as its theme changes. Whichever value you leave out falls back to the other:

```csharp
style.WithAccent("#2563eb");             // both themes
style.WithAccent("#2563eb", "#60a5fa");  // light, dark
new ErdColour(null, "#60a5fa");          // dark given, used for light too
```

The interactive viewer accepts any CSS colour: hex (`#2563eb`, `#fff`, `#2563eb80`), a named colour,
or `rgb()`, `hsl()`, `oklch()` and the other colour functions. Mermaid, d2 and DBML have no theme
switch and accept only hex, so they draw in the **light** value when it is hex. A non-hex colour is
not written to those formats, so prefer hex for a style that should reach every renderer.

> [!NOTE]
> A colour that is not a CSS colour throws `ArgumentException`. Values that could escape the syntax
> they are written into, such as quotes, semicolons, braces, line breaks or `url()`, are rejected,
> because a colour is embedded in CSS and in three diagram languages.

### Style callbacks

A rule matches by name. A callback can style an element from anything in the model. Callbacks run
after the rules for every table, column and relationship. Each one gets the element and the style the
rules produced, and changes that style in place:

```csharp
options
    // Wide tables stand out, so they are worth a second look
    .OnTableStyle(ctx =>
    {
        if (ctx.Table.Columns.Count > 15)
        {
            ctx.Style.WithBadge($"{ctx.Table.Columns.Count} cols").WithAccent("#b45309");
        }
    })
    // Every foreign key, wherever it is
    .OnColumnStyle(ctx =>
    {
        if (ctx.Column.IsForeignKey)
        {
            ctx.Style.WithForeground("#2563eb");
        }
    })
    // Optional relationships are drawn dashed
    .OnRelationshipStyle(ctx =>
    {
        if (!ctx.ForeignKey.IsRequired)
        {
            ctx.Style.WithLineStyle(ErdLineStyle.Dashed);
        }
    });
```

| Callback | Context | Gives you |
| --- | --- | --- |
| `OnTableStyle` | `ErdTableStyleContext` | `Model`, `DbContext`, `Table`, `Style` |
| `OnColumnStyle` | `ErdColumnStyleContext` | `Model`, `DbContext`, `Table`, `Column`, `Style` |
| `OnRelationshipStyle` | `ErdRelationshipStyleContext` | `Model`, `DbContext`, `Dependent`, `Principal`, `ForeignKey`, `Style` |

Callbacks run every time the diagram is regenerated: on startup, and whenever a contributing project
restarts with a changed model. A style computed from the model therefore follows the model as it
changes, with no restart of the AppHost. A callback can run more than once per regeneration, because
image export renders separately, so keep callbacks free of side effects. An exception from a callback
is rethrown as an `InvalidOperationException` that names the element it failed on.

Callbacks run in the AppHost, not in the browser. The viewer receives only the result.

### Styles from configuration

Rules are plain data, so they can live in `appsettings.json` with the rest of the options. Callbacks
can only be set in code.

```json
{
  "AspireC4": {
    "EntityFramework": {
      "Styles": [
        { "Table": "Orders", "Style": { "Accent": { "Light": "#dc2626", "Dark": "#f87171" }, "Bold": true } },
        { "Type": "decimal", "Style": { "Badge": "MONEY" } }
      ],
      "ContextPalette": [ { "Light": "#0f766e" }, { "Light": "#7c3aed" } ],
      "LayoutDirection": "Right",
      "ShowDescriptions": true
    }
  }
}
```

> [!WARNING]
> The configuration binder ignores a list entry it fails to bind. A rule in configuration whose
> colour is not a CSS colour is therefore dropped as a whole, without an error. Nothing it holds
> reaches the diagram, but neither does the rest of that rule.

### Context colours

Each `DbContext` is drawn in a colour from a palette of six. `WithContextPalette` replaces the palette,
and the colours are cycled in order:

```csharp
options.WithContextPalette(new ErdColour("#0f766e", "#2dd4bf"), new ErdColour("#7c3aed", "#a78bfa"));

// One colour per context, used in both themes; the form TypeScript, Go and Python call as withContextPalette.
options.WithContextPaletteColours("#0f766e", "#7c3aed");
```

`ErdColour.IsValid(value)` reports whether a string is a colour these accept, so a value from elsewhere
can be checked before it is used.

Mermaid uses the palette for its per-context fills, deriving each fill from the colour the way the
built-in pairs are derived.

## Layout

| Property | Fluent method | Default | Applies to |
| --- | --- | --- | --- |
| `LayoutDirection` | `WithLayout(direction, …)` | `Down` | the viewer, d2 and Mermaid. `Down`, `Right`, `Up` or `Left` |
| `NodeSpacing` | `WithLayout(…, nodeSpacing:)` | `72` | pixels between neighbouring tables |
| `LayerSpacing` | `WithLayout(…, layerSpacing:)` | `120` | pixels between related tables, along the direction. This gap carries the relationship labels |
| `ContextSpacing` | `WithLayout(…, contextSpacing:)` | `88` | pixels between context containers |
| `EdgeStyle` | `WithEdgeStyle(style)` | `Bezier` | the viewer. `Bezier`, `SmoothStep`, `Step` or `Straight` |

The spacing settings apply to the interactive viewer. d2 and Mermaid lay themselves out and take only
the direction. Negative spacing throws `ArgumentOutOfRangeException`.

`Right` suits a wide screen and a long chain of relationships. In a sideways layout a table's
relationship to itself hangs below its card rather than beside it, because both sides of the card
carry other relationships. The space below the card is reserved, so the arc's label clears the next
table down.

## Animation and interaction

| Property | Fluent method | Default | What it does |
| --- | --- | --- | --- |
| `EdgeAnimation` | `WithAnimation(edges, …)` | `Normal` | how fast relationship lines flow toward their many end: `Off`, `Slow`, `Normal` or `Fast` |
| `AnimateTransitions` | `WithAnimation(…, transitions:)` | `true` | tables glide, rather than jump, when the viewer re-arranges them |
| `HighlightOnHover` | set the property | `true` | hovering a table picks out its relationships and the tables they reach, and fades the rest |
| — | `WithoutAnimation()` | — | turns off flowing lines and gliding tables together |

A browser that asks for reduced motion stops both animations. The reader can override that in the
viewer's **Motion** setting. A search outranks the hover highlight: while a search is active, hovering
changes nothing.

## Viewer defaults

These set where the viewer starts. The reader can still change each one in the viewer's settings.

| Property | Fluent method | Default |
| --- | --- | --- |
| `Theme` | `WithViewerDefaults(theme, …)` | `Auto` (follow the operating system), `Light` or `Dark` |
| `ViewerBackground` | `WithViewerDefaults(…, background:)` | `Lines`, `Dots`, `Cross` or `None` |
| `TableDisplay` | `WithViewerDefaults(…, tables:)` | `Columns`, or `Names` for headers only |
| `ShowLegend` | `WithLegend()` | `false`. A panel explaining the context colours, the key badges and every badge your styles add |
| `ShowTooltips` | set the property | `true`. Hovering a column or a table header shows its CLR type, store type, length, nullability, keys, comment, description and any style tooltip |

The viewer remembers a reader's choices in the browser. Each choice is stored with the default it was
made against. If you later change that default in the AppHost, the new default applies again, so a
choice made under the old default never hides a new configuration. A choice made against an unchanged
default survives reloads as before.

## Descriptions

Tables and columns can show a description. This is **off by default**; `WithDescriptions()` turns it
on.

```csharp
options.WithDescriptions();                                         // every source
options.WithDescriptions(ErdDescriptionSources.XmlDocumentation);  // only XML documentation
```

A description comes from the first source that has one, in this order:

| Order | Source (`ErdDescriptionSources`) | Read from |
| --- | --- | --- |
| 1 | `Attributes` | `[Description("…")]`, then `[Display(Description = "…")]`, then `[DisplayName("…")]`, on the entity type or property |
| 2 | `XmlDocumentation` | the `<summary>` of the entity type or property. A positional record property uses the record's `<param>` for it |
| 3 | `Comments` | the database comment, from `[Comment("…")]` or `HasComment("…")` |

An attribute is written for display, so it comes first. A comment is written for the database, so it
is the fallback. References in a summary keep their short name (`<see cref="Order"/>` reads `Order`),
and a summary wrapped over several lines reads as one.

```csharp
/// <summary>An order a customer has placed.</summary>
public sealed class Order
{
    /// <summary>Including tax, in the order's currency.</summary>
    public decimal Total { get; set; }

    [Display(Description = "Where the order is in its lifecycle")]
    public OrderStatus Status { get; set; }
}
```

Each way of [reading the model](../Entity-Framework-Diagrams.md#how-the-model-is-read) records all
three sources. They read XML documentation differently:

| Source | XML documentation |
| --- | --- |
| Analyzer (`AspireC4.EntityFrameworkCore`) | read from source, whether or not the project generates a documentation file |
| Metadata reader (the default) | read from the documentation file beside the assembly, so the project needs `<GenerateDocumentationFile>true</GenerateDocumentationFile>` |
| Build-time dump | the same documentation file |

Attributes and comments need nothing extra. Entity-level `ToTable(t => t.HasComment(…))` is visible
only to the build-time dump, and `[Comment]` on the entity class to all three.

What each renderer shows:

| Renderer | Table description | Column description |
| --- | --- | --- |
| Interactive | a line under the table name | a column on each card, capped in width, with the full text as its tooltip |
| Mermaid | not shown: an entity has no note | added to the attribute's comment |
| DBML | leads the table's `Note` | added to the column's `note` |
| d2 | the table's `tooltip` | not shown: a `sql_table` row has no room |

In the viewer, the **Descriptions** setting switches them on and off. It appears only when the model
has descriptions. Descriptions are always sent to the viewer, and `ShowDescriptions` sets whether they
start shown.

## What each renderer applies

| | Interactive | Mermaid | d2 | DBML |
| --- | --- | --- | --- | --- |
| Table accent | ✓ | stroke, with a washed fill | header band | `headercolor` |
| Table background, foreground | ✓ | ✓ | background as row fill, foreground as text | — |
| Bold, italic, line style | ✓ | ✓ | ✓ | — |
| Column styles | ✓ | — | — | — |
| Relationship styles | ✓ | — | line colour, dash, bold, italic | — |
| Badges, tooltips | ✓ | — | table tooltip | — |
| Dark-theme colours | ✓ | — (light) | — (light) | — (light) |
| Layout direction | ✓ | `direction` | `direction` | — |
| Spacing, edge style, animation, viewer defaults | ✓ | — | — | — |
| Descriptions | ✓ | column comment | table tooltip | notes |

An unstyled diagram is unchanged in every format. The text formats are written exactly as they were
before styling existed. Exported images re-render when only a style changes, because the export stamp
includes the styling.

## Options reference

| Property | Default | Fluent method |
| --- | --- | --- |
| `Styles` | `[]` | `WithStyle`, `WithTableStyle`, `WithColumnStyle`, `WithTypeStyle` |
| `TableStyleCallbacks` / `ColumnStyleCallbacks` / `RelationshipStyleCallbacks` | `[]` | `OnTableStyle`, `OnColumnStyle`, `OnRelationshipStyle` |
| `ContextPalette` | `[]` (built-in six) | `WithContextPalette` |
| `LayoutDirection` | `Down` | `WithLayout` |
| `NodeSpacing` / `LayerSpacing` / `ContextSpacing` | `72` / `120` / `88` | `WithLayout` |
| `EdgeStyle` | `Bezier` | `WithEdgeStyle` |
| `EdgeAnimation` | `Normal` | `WithAnimation`, `WithoutAnimation` |
| `AnimateTransitions` | `true` | `WithAnimation`, `WithoutAnimation` |
| `HighlightOnHover` | `true` | — |
| `Theme` / `ViewerBackground` / `TableDisplay` | `Auto` / `Lines` / `Columns` | `WithViewerDefaults` |
| `ShowLegend` | `false` | `WithLegend` |
| `ShowTooltips` | `true` | — |
| `ShowDescriptions` | `false` | `WithDescriptions` |
| `DescriptionSources` | `All` | `WithDescriptions(sources)` |
