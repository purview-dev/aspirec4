using Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Styles, lays out and describes the sample ER diagram — one working example of each option on the
/// ER Diagram Styling page, against the sample's own tables.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="SampleDataWiring"/>, which stays small to keep the AppHost under the
/// CA1506 class-coupling limit. The integration suite asserts that these reach the generated model,
/// so a change here that stops styling something shows up as a failing test.
/// </para>
/// <para>
/// <see cref="WithSharedSampleStyling"/> is the part every sample AppHost applies, in its own language;
/// <c>SampleAppHostContract</c> asserts it for all five. Change it, the TypeScript, Go and Python samples,
/// and the contract together.
/// </para>
/// </remarks>
static class SampleErdStyling
{
	/// <summary>The styling every sample AppHost applies, whatever its language.</summary>
	public static AspireC4EfDiagramOptions WithSharedSampleStyling(this AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options
			.WithTableStyle("Order*", style => style.WithAccent("#dc2626", "#f87171").WithBadge("SALES"))
			.WithTypeStyle("decimal", style => style.WithAccent("#16a34a"))
			.WithContextPaletteColours("#2563eb", "#16a34a", "#d97706", "#9333ea", "#e11d48", "#0d9488")
			.WithLayout(ErdLayoutDirection.Right, layerSpacing: 160)
			.WithEdgeStyle(ErdEdgeStyle.SmoothStep)
			.WithAnimation(ErdEdgeAnimation.Slow)
			.WithLegend()
			.WithDescriptions();
	}

	/// <summary>Applies the sample's styling, layout and description options.</summary>
	/// <remarks>
	/// The shared styling plus the forms only C# can use: the per-regeneration callbacks and colours
	/// with a separate dark-theme value. Rules apply in order and a later match overrides only what it
	/// sets, so the broad C#-only rules come first, the shared ones after them, and the light-and-dark
	/// palette last, replacing the shared single-colour one.
	/// </remarks>
	public static AspireC4EfDiagramOptions WithSampleStyling(this AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options
			// ── Rules ───────────────────────────────────────────────────────────────────────
			// This broad rule comes first, so the shared Order* rule below replaces its badge on Orders
			// while keeping the rest. Every table that has a matching column, rather than the column itself.
			.WithStyle(rule => rule.ForColumn("CustomerId").ForTables().WithBadge("CUSTOMER"))
			// A whole context, by name: rules naming only a context style its tables.
			.WithStyle(rule =>
				rule.ForContext("AuditDbContext")
					.WithLineStyle(ErdLineStyle.Dashed)
					.WithBackground("#fafaf9", "#1c1917")
			)
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
			// ── Shared with every sample: a table pattern with a colour for each theme and a badge, a
			// type pattern (decimal matches decimal(18,2) and decimal(18,4) alike), layout, animation,
			// the viewer and descriptions ([Description]/[Display]/[DisplayName], XML summaries, comments).
			.WithSharedSampleStyling()
			// ── Colours per DbContext, light and dark ───────────────────────────────────────
			.WithContextPalette(
				new ErdColour("#2563eb", "#60a5fa"),
				new ErdColour("#16a34a", "#4ade80"),
				new ErdColour("#d97706", "#fbbf24"),
				new ErdColour("#9333ea", "#c084fc"),
				new ErdColour("#e11d48", "#fb7185"),
				new ErdColour("#0d9488", "#2dd4bf")
			);
	}
}
