using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Covers the discovery and filtering configuration on the ER diagram options.
/// </summary>
public sealed class AspireC4EfDiagramOptionsTests
{
	// Tests

	[Test]
	public async Task DiscoverAllProjects_ByDefault_IsOn()
	{
		// The whole point of the default: WithEntityFrameworkDiagram() alone finds every context.
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.DiscoverAllProjects).IsTrue();
	}

	[Test]
	public async Task WithProjectOptInOnly_TurnsOffAutomaticDiscovery()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithProjectOptInOnly();

		// Assert
		await Assert.That(options.DiscoverAllProjects).IsFalse();
	}

	[Test]
	public async Task WithIncludedContexts_AddsTheNames()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithIncludedContexts("SalesDbContext", "AuditDbContext");

		// Assert
		await Assert.That(options.IncludedContexts).IsEquivalentTo(["SalesDbContext", "AuditDbContext"]);
	}

	[Test]
	public async Task WithExcludedContexts_AddsTheNames()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithExcludedContexts("IdentityDbContext");

		// Assert
		await Assert.That(options.ExcludedContexts).IsEquivalentTo(["IdentityDbContext"]);
	}

	[Test]
	public async Task WithExcludedContexts_RepeatedName_IsAddedOnce()
	{
		// Act
		var options = new AspireC4EfDiagramOptions()
			.WithExcludedContexts("IdentityDbContext")
			.WithExcludedContexts("identitydbcontext");

		// Assert
		await Assert.That(options.ExcludedContexts.Count).IsEqualTo(1);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task WithIncludedContexts_BlankName_Throws(string? name)
	{
		// Act & Assert
		await Assert.That(() => new AspireC4EfDiagramOptions().WithIncludedContexts(name!)).Throws<ArgumentException>();
	}

	[Test]
	public async Task ExportImages_ByDefault_IsOn()
	{
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.ExportImages).IsTrue();
		await Assert.That(options.ImageFormats).IsEquivalentTo([ErdImageFormat.Svg, ErdImageFormat.Png]);
	}

	[Test]
	public async Task WithImageExport_ReplacesTheDefaultFormats()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Png);

		// Assert
		await Assert.That(options.ImageFormats).IsEquivalentTo([ErdImageFormat.Png]);
		await Assert.That(options.ExportImages).IsTrue();
	}

	[Test]
	public async Task WithImageExport_NoFormats_TurnsExportOff()
	{
		// Naming no format is a request for no images, not a request for the default two.
		// Act
		var options = new AspireC4EfDiagramOptions().WithImageExport();

		// Assert
		await Assert.That(options.ExportImages).IsFalse();
		await Assert.That(options.ImageFormats).IsEmpty();
	}

	[Test]
	public async Task WithoutImageExport_TurnsExportOff()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithoutImageExport();

		// Assert
		await Assert.That(options.ExportImages).IsFalse();
	}

	[Test]
	public async Task UseViewerFidelityExport_ByDefault_IsOff()
	{
		// Off because it pulls a browser image of several gigabytes; the d2 render is the default.
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.UseViewerFidelityExport).IsFalse();
		await Assert.That(options.ViewerExportImage).IsNull();
	}

	[Test]
	public async Task WithViewerFidelityExport_TurnsItOn()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithViewerFidelityExport();

		// Assert
		await Assert.That(options.UseViewerFidelityExport).IsTrue();
	}

	[Test]
	public async Task WithViewerFidelityExport_False_TurnsItOff()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithViewerFidelityExport().WithViewerFidelityExport(false);

		// Assert
		await Assert.That(options.UseViewerFidelityExport).IsFalse();
	}

	[Test]
	public async Task D2Command_ByDefault_IsUnsetSoExportUsesTheContainer()
	{
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.D2Command).IsNull();
		await Assert.That(options.ContainerRuntimeCommand).IsEqualTo("docker");
	}

	[Test]
	public async Task ProjectOptions_CarryLabelAndFilters()
	{
		// Act
		AspireC4EfProjectOptions options = new() { Label = "API" };
		options.WithExcludedContexts("IdentityDbContext");

		// Assert
		await Assert.That(options.Label).IsEqualTo("API");
		await Assert.That(options.ExcludedContexts).IsEquivalentTo(["IdentityDbContext"]);
	}

	// Styling, layout and descriptions

	[Test]
	public async Task Defaults_ForTheNewOptions_KeepTheDiagramAsItWas()
	{
		// Act
		AspireC4EfDiagramOptions options = new();

		// Assert
		await Assert.That(options.Styles).IsEmpty();
		await Assert.That(options.ShowDescriptions).IsFalse();
		await Assert.That(options.DescriptionSources).IsEqualTo(ErdDescriptionSources.All);
		await Assert.That(options.LayoutDirection).IsEqualTo(ErdLayoutDirection.Down);
		await Assert.That(options.NodeSpacing).IsEqualTo(72);
		await Assert.That(options.LayerSpacing).IsEqualTo(120);
		await Assert.That(options.ContextSpacing).IsEqualTo(88);
		await Assert.That(options.EdgeStyle).IsEqualTo(ErdEdgeStyle.Bezier);
		await Assert.That(options.EdgeAnimation).IsEqualTo(ErdEdgeAnimation.Normal);
		await Assert.That(options.AnimateTransitions).IsTrue();
		await Assert.That(options.HighlightOnHover).IsTrue();
		await Assert.That(options.ShowTooltips).IsTrue();
		await Assert.That(options.ShowLegend).IsFalse();
	}

	[Test]
	public async Task WithStyle_Callback_AddsTheConfiguredRule()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithStyle(rule =>
			rule.ForTable("Order*").ForType("decimal").WithAccent("#b45309", "#f59e0b")
		);

		// Assert
		var rule = options.Styles.Single();
		await Assert.That(rule.Table).IsEqualTo("Order*");
		await Assert.That(rule.Type).IsEqualTo("decimal");
		await Assert.That(rule.EffectiveTarget).IsEqualTo(ErdStyleTarget.Column);
		await Assert.That(rule.Style.Accent!.Dark).IsEqualTo("#f59e0b");
	}

	[Test]
	public async Task WithTableStyle_AddsATableRule()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithTableStyle("Orders", s => s.WithBold());

		// Assert
		await Assert.That(options.Styles.Single().EffectiveTarget).IsEqualTo(ErdStyleTarget.Table);
		await Assert.That(options.Styles.Single().Style.Bold).IsTrue();
	}

	[Test]
	public async Task WithColumnStyle_WithTable_NarrowsToThatTable()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithColumnStyle("Email", s => s.WithItalic(), table: "Customers");

		// Assert
		await Assert.That(options.Styles.Single().Table).IsEqualTo("Customers");
		await Assert.That(options.Styles.Single().Column).IsEqualTo("Email");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("  ")]
	public async Task WithTableStyle_BlankPattern_Throws(string? pattern)
	{
		// Act & Assert
		await Assert
			.That(() => new AspireC4EfDiagramOptions().WithTableStyle(pattern!, s => s.WithBold()))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task OnTableStyle_AddsTheCallback()
	{
		// Act
		var options = new AspireC4EfDiagramOptions()
			.OnTableStyle(_ => { })
			.OnColumnStyle(_ => { })
			.OnRelationshipStyle(_ => { });

		// Assert
		await Assert.That(options.TableStyleCallbacks.Count).IsEqualTo(1);
		await Assert.That(options.ColumnStyleCallbacks.Count).IsEqualTo(1);
		await Assert.That(options.RelationshipStyleCallbacks.Count).IsEqualTo(1);
	}

	[Test]
	public async Task WithLayout_SetsOnlyWhatIsGiven()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithLayout(ErdLayoutDirection.Right, layerSpacing: 200);

		// Assert
		await Assert.That(options.LayoutDirection).IsEqualTo(ErdLayoutDirection.Right);
		await Assert.That(options.LayerSpacing).IsEqualTo(200);
		await Assert.That(options.NodeSpacing).IsEqualTo(72);
	}

	[Test]
	public async Task WithLayout_NegativeSpacing_Throws()
	{
		// Act & Assert
		await Assert
			.That(() => new AspireC4EfDiagramOptions().WithLayout(ErdLayoutDirection.Down, nodeSpacing: -1))
			.Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task WithoutAnimation_StopsEdgesAndTransitions()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithoutAnimation();

		// Assert
		await Assert.That(options.EdgeAnimation).IsEqualTo(ErdEdgeAnimation.Off);
		await Assert.That(options.AnimateTransitions).IsFalse();
	}

	[Test]
	public async Task WithDescriptions_SetsTheSourcesAndShowsThem()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithDescriptions(ErdDescriptionSources.XmlDocumentation);

		// Assert
		await Assert.That(options.ShowDescriptions).IsTrue();
		await Assert.That(options.DescriptionSources).IsEqualTo(ErdDescriptionSources.XmlDocumentation);
	}

	[Test]
	public async Task WithDescriptions_NoSource_HidesThem()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithDescriptions(ErdDescriptionSources.None);

		// Assert
		await Assert.That(options.ShowDescriptions).IsFalse();
	}

	[Test]
	public async Task WithContextPalette_DropsEmptyColours()
	{
		// Act
		var options = new AspireC4EfDiagramOptions().WithContextPalette(new ErdColour("#0f766e"), new ErdColour());

		// Assert
		await Assert.That(options.ContextPalette.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Bind_Configuration_PopulatesStylesLayoutAndDescriptions()
	{
		// A style rule is plain data, so it can live in appsettings.json beside the rest.
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Styles:0:Table"] = "Orders",
					["Styles:0:Style:Accent:Light"] = "#dc2626",
					["Styles:0:Style:Accent:Dark"] = "#f87171",
					["Styles:0:Style:Bold"] = "true",
					["Styles:1:Type"] = "decimal",
					["Styles:1:Style:Badge"] = "MONEY",
					["ContextPalette:0:Light"] = "#0f766e",
					["LayoutDirection"] = "Right",
					["EdgeStyle"] = "SmoothStep",
					["ShowDescriptions"] = "true",
					["DescriptionSources"] = "Attributes, XmlDocumentation",
				}
			)
			.Build();
		AspireC4EfDiagramOptions options = new();

		// Act
		ConfigurationBinder.Bind(configuration, options);

		// Assert
		await Assert.That(options.Styles.Count).IsEqualTo(2);
		await Assert.That(options.Styles[0].Style.Accent!.Dark).IsEqualTo("#f87171");
		await Assert.That(options.Styles[0].Style.Bold).IsTrue();
		await Assert.That(options.Styles[1].EffectiveTarget).IsEqualTo(ErdStyleTarget.Column);
		await Assert.That(options.ContextPalette.Single().Light).IsEqualTo("#0f766e");
		await Assert.That(options.LayoutDirection).IsEqualTo(ErdLayoutDirection.Right);
		await Assert.That(options.EdgeStyle).IsEqualTo(ErdEdgeStyle.SmoothStep);
		await Assert.That(options.ShowDescriptions).IsTrue();
		await Assert
			.That(options.DescriptionSources)
			.IsEqualTo(ErdDescriptionSources.Attributes | ErdDescriptionSources.XmlDocumentation);
	}

	[Test]
	public async Task Bind_ConfigurationColourThatIsNotCss_NeverReachesTheStyle()
	{
		// A value from configuration is written into the diagram's own syntax, so it is checked too. The
		// binder swallows a list element's failure, so the whole rule is dropped rather than failing
		// startup — and nothing it held reaches the output.
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Styles:0:Table"] = "Orders",
					["Styles:0:Style:Accent:Light"] = "red;} body { display: none",
				}
			)
			.Build();
		AspireC4EfDiagramOptions options = new();

		// Act
		ConfigurationBinder.Bind(configuration, options);

		// Assert
		await Assert.That(options.Styles).IsEmpty();
	}
}
