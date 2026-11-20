using Aspire.Hosting.AspireC4.EntityFrameworkCore.Dbml;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Mermaid;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// What the text emitters and the interactive output make of styles, layout and descriptions. An
/// unconfigured diagram is covered by the exact-body tests, which prove it is unchanged.
/// </summary>
public sealed partial class ErdEmitterTests
{
	// Helpers

	/// <summary>The blog model, with a description on Posts and on its Title column.</summary>
	static EfModel CreateDescribedModel()
	{
		var model = CreateModel();
		var posts = model.Contexts[0].Tables.Single(t => t.Name == "Posts");

		var described = posts with
		{
			Description = "Articles on a blog",
			Columns = [.. posts.Columns.Select(c => c.Name == "Title" ? c with { Description = "The headline" } : c)],
		};

		return model with
		{
			Contexts = [model.Contexts[0] with { Tables = [model.Contexts[0].Tables[0], described] }],
		};
	}

	/// <summary>The blog model split across two contexts, so the Mermaid output groups and colours them.</summary>
	static EfModel CreateTwoContextModel()
	{
		var model = CreateModel();

		return model with
		{
			Contexts =
			[
				model.Contexts[0] with
				{
					Tables = [model.Contexts[0].Tables[0]],
				},
				new EfContext
				{
					Name = "PostDbContext",
					TypeName = "Sample.PostDbContext",
					Tables = [model.Contexts[0].Tables[1]],
				},
			],
		};
	}

	// Mermaid

	[Test]
	public async Task Generate_MermaidSidewaysLayout_EmitsTheDirection()
	{
		// Arrange
		var options = CreateOptions().WithLayout(ErdLayoutDirection.Right);

		// Act
		var body = Body(MermaidGenerator.Generate(CreateModel(), options));

		// Assert
		await Assert.That(body).StartsWith("erDiagram\n\tdirection LR\n");
	}

	[Test]
	public async Task Generate_MermaidStyledTable_EmitsAStyleStatement()
	{
		// Arrange
		var options = CreateOptions()
			.WithTableStyle("Posts", s =>
				s.WithAccent("#dc2626", "#f87171")
					.WithBackground("#fef2f2")
					.WithForeground("#7f1d1d")
					.WithLineStyle(ErdLineStyle.Dashed)
					.WithBold()
			);

		// Act
		var body = Body(MermaidGenerator.Generate(CreateModel(), options));

		// Assert — the light colours, because Mermaid has no theme switch of its own here.
		await Assert
			.That(body)
			.EndsWith("\tstyle Posts fill:#fef2f2,stroke:#dc2626,color:#7f1d1d,stroke-dasharray:6 4,font-weight:bold");
	}

	[Test]
	public async Task Generate_MermaidAccentOnly_WashesItForTheFill()
	{
		// Arrange
		var options = CreateOptions().WithTableStyle("Blogs", s => s.WithAccent("#000000"));

		// Act
		var body = Body(MermaidGenerator.Generate(CreateModel(), options));

		// Assert
		await Assert.That(body).Contains("\tstyle Blogs fill:#d9d9d9,stroke:#000000");
	}

	[Test]
	public async Task Generate_MermaidNonHexColour_IsLeftOut()
	{
		// A colour function's commas would break the comma-separated style statement.
		// Arrange
		var options = CreateOptions().WithTableStyle("Posts", s => s.WithAccent("rgb(220, 38, 38)"));

		// Act
		var body = Body(MermaidGenerator.Generate(CreateModel(), options));

		// Assert
		await Assert.That(body).DoesNotContain("style Posts");
	}

	[Test]
	public async Task Generate_MermaidContextPalette_ColoursEachContext()
	{
		// Arrange
		var options = CreateOptions().WithContextPalette(new ErdColour("#0f766e"), new ErdColour("#7c3aed"));

		// Act
		var body = Body(MermaidGenerator.Generate(CreateTwoContextModel(), options));

		// Assert
		await Assert.That(body).Contains("classDef BlogDbContext fill:#dbeae9,stroke:#0f766e,color:#1f2937");
		await Assert.That(body).Contains("classDef PostDbContext fill:#ebe1fc,stroke:#7c3aed,color:#1f2937");
	}

	[Test]
	public async Task Generate_MermaidDescriptionsShown_AddsThemToTheColumnNote()
	{
		// Arrange
		var options = CreateOptions().WithDescriptions();

		// Act
		var body = Body(MermaidGenerator.Generate(CreateDescribedModel(), options));

		// Assert
		await Assert.That(body).Contains("varchar(200) Title \"The headline\"");
	}

	[Test]
	public async Task Generate_MermaidDescriptionsOff_LeavesTheColumnNoteAlone()
	{
		// Off by default: a description is opt-in, so the output is what it always was.
		// Act
		var body = Body(MermaidGenerator.Generate(CreateDescribedModel(), CreateOptions()));

		// Assert
		await Assert.That(body).DoesNotContain("The headline");
	}

	[Test]
	public async Task Generate_MermaidDescriptionSameAsComment_IsNotRepeated()
	{
		// Arrange
		var model = CreateModel();
		var posts = model.Contexts[0].Tables[1];
		var commented = model with
		{
			Contexts =
			[
				model.Contexts[0] with
				{
					Tables =
					[
						model.Contexts[0].Tables[0],
						posts with
						{
							Columns =
							[
								.. posts.Columns.Select(c => c.Name == "Title" ? c with { Comment = "Headline" } : c),
							],
						},
					],
				},
			],
		};

		// Act
		var body = Body(MermaidGenerator.Generate(commented, CreateOptions().WithDescriptions()));

		// Assert
		await Assert.That(body).Contains("Title \"Headline\"");
	}

	// DBML

	[Test]
	public async Task Generate_DbmlStyledTable_EmitsItsHeaderColour()
	{
		// Arrange
		var options = CreateOptions(ErdRenderer.Dbml).WithTableStyle("Posts", s => s.WithAccent("#dc2626"));

		// Act
		var body = Body(DbmlGenerator.Generate(CreateModel(), options));

		// Assert
		await Assert.That(body).Contains("Table Posts [headercolor: #dc2626] {");
		await Assert.That(body).Contains("Table Blogs {");
	}

	[Test]
	public async Task Generate_DbmlDescriptionsShown_LeadTheTableNoteAndJoinTheColumnNote()
	{
		// Arrange
		var options = CreateOptions(ErdRenderer.Dbml).WithDescriptions();

		// Act
		var body = Body(DbmlGenerator.Generate(CreateDescribedModel(), options));

		// Assert
		await Assert.That(body).Contains("Note: \"Articles on a blog — BlogDbContext (Sample.Post)\"");
		await Assert.That(body).Contains("Title varchar(200) [not null, note: \"The headline\"]");
	}

	// Interactive

	[Test]
	public async Task Create_Interactive_WritesThePresentationBesideTheContexts()
	{
		// Arrange
		var options = CreateOptions(ErdRenderer.Interactive).WithTableStyle("Posts", s => s.WithBadge("PII"));

		// Act
		var json = ErdOutput.Create(CreateModel(), options)[0].Content;

		// Assert — still a model to anything that reads one, with the presentation beside it.
		var parsed = EfModelLoader.Parse(json);
		await Assert.That(parsed.Contexts.Count).IsEqualTo(1);
		await Assert.That(json).Contains("\"presentation\"");
		await Assert.That(json).Contains("\"badge\": \"PII\"");
	}

	[Test]
	public async Task Create_InteractiveConfiguredTheme_PaintsItBeforeTheScriptLoads()
	{
		// Arrange
		var options = CreateOptions(ErdRenderer.Interactive).WithViewerDefaults(ErdViewerTheme.Dark);

		// Act
		var html = ErdOutput.Create(CreateModel(), options).Single(f => f.FileName == "index.html").Content;

		// Assert
		await Assert.That(html).DoesNotContain("__DEFAULT_THEME__");
		await Assert.That(html).Contains("[\"light\", \"dark\"].includes(\"dark\")");
	}

	[Test]
	public async Task Create_ImageStamp_ChangesWithTheStyle()
	{
		// The stamp decides whether images are re-rendered, so a style-only change must reach it.
		// Arrange
		var plain = CreateOptions(ErdRenderer.Interactive);
		var styled = CreateOptions(ErdRenderer.Interactive).WithTableStyle("Posts", s => s.WithBold());

		// Act
		var before = ErdOutput.Serialise(CreateModel(), plain);
		var after = ErdOutput.Serialise(CreateModel(), styled);

		// Assert
		await Assert.That(after).IsNotEqualTo(before);
	}
}
