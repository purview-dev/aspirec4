using Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Styles, lays out and describes the sample ER diagram — one working example of each option on the
/// ER Diagram Styling page, against the sample's own tables.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="SampleDataWiring"/>, which stays small to keep the AppHost under the
/// CA1506 class-coupling limit. The integration suite asserts that these reach the generated model,
/// so a change here that stops styling something shows up as a failing test.
/// </remarks>
static class SampleErdStyling
{
	/// <summary>Applies the sample's styling, layout and description options.</summary>
	public static AspireC4EfDiagramOptions WithSampleStyling(this AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options
			// ── Rules ───────────────────────────────────────────────────────────────────────
			// Rules apply in order, and a later match overrides only what it sets — so this broad rule
			// comes first, and the Order* rule below replaces its badge on Orders while keeping the rest.
			// Every table that has a matching column, rather than the column itself.
			.WithStyle(rule => rule.ForColumn("CustomerId").ForTables().WithBadge("CUSTOMER"))
			// A table pattern, with a colour for each theme and a badge.
			.WithTableStyle("Order*", style => style.WithAccent("#dc2626", "#f87171").WithBadge("SALES"))
			// A whole context, by name: rules naming only a context style its tables.
			.WithStyle(rule =>
				rule.ForContext("AuditDbContext")
					.WithLineStyle(ErdLineStyle.Dashed)
					.WithBackground("#fafaf9", "#1c1917")
			)
			// A type pattern: decimal matches decimal(18,2) and decimal(18,4) alike.
			.WithTypeStyle("decimal", style => style.WithAccent("#16a34a"))
			// A kind of domain type.
			.WithTypeStyle("enum", style => style.WithItalic())
			// One column of one table — the fluent rename, so the column name rather than the property.
			.WithColumnStyle(
				"email_address",
				style => style.WithBadge("PII").WithTooltip("Personal data: handle under the retention policy"),
				table: "Customers"
			)
			// A combination: the decimal columns of the order tables only.
			.WithStyle(rule => rule.ForTable("Order*").ForType("decimal").WithBold())
			// Every relationship with Customers at either end.
			.WithStyle(rule =>
				rule.ForTable("Customers")
					.ForRelationships()
					.WithAccent("#2563eb", "#60a5fa")
					.WithLineStyle(ErdLineStyle.Dotted)
			)
			// ── Callbacks: computed from the model on every regeneration ────────────────────
			// Wide tables stand out, so they are worth a second look.
			.OnTableStyle(ctx =>
			{
				if (ctx.Table.Columns.Count > 5)
				{
					ctx.Style.WithTooltip($"{ctx.Table.Columns.Count} columns — wider than most");
				}
			})
			// Every foreign key, wherever it is.
			.OnColumnStyle(ctx =>
			{
				if (ctx.Column.IsForeignKey)
				{
					ctx.Style.WithForeground("#2563eb", "#93c5fd");
				}
			})
			// Optional relationships are drawn dashed, unless a rule already chose a line style.
			.OnRelationshipStyle(ctx =>
			{
				if (!ctx.ForeignKey.IsRequired && ctx.Style.LineStyle is null)
				{
					ctx.Style.WithLineStyle(ErdLineStyle.Dashed);
				}
			})
			// ── Colours per DbContext, light and dark ───────────────────────────────────────
			.WithContextPalette(
				new ErdColour("#2563eb", "#60a5fa"),
				new ErdColour("#16a34a", "#4ade80"),
				new ErdColour("#d97706", "#fbbf24"),
				new ErdColour("#9333ea", "#c084fc"),
				new ErdColour("#e11d48", "#fb7185"),
				new ErdColour("#0d9488", "#2dd4bf")
			)
			// ── Layout, animation and the viewer ────────────────────────────────────────────
			.WithLayout(ErdLayoutDirection.Right, layerSpacing: 160)
			.WithEdgeStyle(ErdEdgeStyle.SmoothStep)
			.WithAnimation(ErdEdgeAnimation.Slow)
			.WithLegend()
			// ── Descriptions: [Description]/[Display]/[DisplayName], XML summaries, comments ──
			.WithDescriptions();
	}
}
