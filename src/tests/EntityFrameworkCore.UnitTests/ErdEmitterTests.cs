using Aspire.Hosting.AspireC4.EntityFrameworkCore.Dbml;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Mermaid;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Exact-output tests for the text emitters.
/// </summary>
/// <remarks>
/// These assert on whole emitted bodies rather than fragments, so an ordering change is a test
/// failure rather than something that silently churns the generated files.
/// </remarks>
public sealed class ErdEmitterTests
{
	// Helpers

	static AspireC4EfDiagramOptions CreateOptions(
		ErdRenderer renderer = ErdRenderer.Mermaid,
		bool useSubgraphs = true,
		string? title = null
	) =>
		new()
		{
			Renderer = renderer,
			UseSubgraphs = useSubgraphs,
			Title = title,
		};

	static EfColumn CreateColumn(
		string name,
		string storeType = "int",
		bool isNullable = false,
		bool isPrimaryKey = false,
		bool isForeignKey = false,
		bool isUnique = false,
		int? maxLength = null
	) =>
		new()
		{
			Name = name,
			StoreType = storeType,
			IsNullable = isNullable,
			IsPrimaryKey = isPrimaryKey,
			IsForeignKey = isForeignKey,
			IsUnique = isUnique,
			MaxLength = maxLength,
		};

	/// <summary>Two tables, one mandatory one-to-many relationship, in a single context.</summary>
	static EfModel CreateModel(bool isUnique = false, bool isRequired = true, string? schema = null) =>
		new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "BlogDbContext",
					TypeName = "Sample.BlogDbContext",
					DefaultSchema = schema,
					Tables =
					[
						new EfTable
						{
							Name = "Blogs",
							ClrTypeName = "Sample.Blog",
							Columns = [CreateColumn("Id", isPrimaryKey: true)],
						},
						new EfTable
						{
							Name = "Posts",
							ClrTypeName = "Sample.Post",
							Columns =
							[
								CreateColumn("Id", isPrimaryKey: true),
								CreateColumn("Title", "varchar", maxLength: 200),
								CreateColumn("Summary", "varchar", isNullable: true),
								CreateColumn("BlogId", isForeignKey: true, isUnique: isUnique, isNullable: !isRequired),
							],
							ForeignKeys =
							[
								new EfForeignKey
								{
									Columns = ["BlogId"],
									PrincipalTable = "Blogs",
									PrincipalColumns = ["Id"],
									NavigationName = "Blog",
									IsUnique = isUnique,
									IsRequired = isRequired,
								},
							],
						},
					],
				},
			],
		};

	/// <summary>Drops the timestamped header so the body can be compared exactly.</summary>
	static string Body(string content)
	{
		var lines = content.Split('\n').Select(l => l.TrimEnd('\r'));

		return string.Join(
				"\n",
				lines.SkipWhile(l =>
					l.StartsWith("%%", StringComparison.Ordinal)
					|| l.StartsWith("//", StringComparison.Ordinal)
					|| l.Length == 0
				)
			)
			.TrimEnd('\n');
	}

	// Mermaid

	[Test]
	public async Task Generate_SingleContext_EmitsExactMermaidBody()
	{
		// Arrange
		var options = CreateOptions();

		// Act
		var body = Body(MermaidGenerator.Generate(CreateModel(), options));

		// Assert — single context, so no subgraph wrapper and no class assignments.
		await Assert
			.That(body)
			.IsEqualTo(
				"""
				erDiagram
					Blogs["Blog"] {
						int Id PK
					}
					Posts["Post"] {
						int Id PK
						varchar(200) Title
						varchar? Summary
						int BlogId FK
					}
					Blogs ||--|{ Posts : "← Blog"
				""".ReplaceLineEndings("\n")
			);
	}

	[Test]
	[Arguments(true, true, "||--||")]
	[Arguments(true, false, "||--o|")]
	[Arguments(false, true, "||--|{")]
	[Arguments(false, false, "||--o{")]
	public async Task Generate_Cardinality_MapsFromUniquenessAndRequiredness(
		bool isUnique,
		bool isRequired,
		string expected
	)
	{
		// Arrange
		var model = CreateModel(isUnique, isRequired);

		// Act
		var body = Body(MermaidGenerator.Generate(model, CreateOptions()));

		// Assert
		await Assert.That(body).Contains($"Blogs {expected} Posts");
	}

	[Test]
	public async Task Generate_TypeContainingComma_RewritesTheSeparator()
	{
		// Mermaid's attribute-type grammar permits parentheses but not commas, so decimal(18,2) would
		// fail to parse.
		// Arrange
		EfModel model = new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "C",
					TypeName = "N.C",
					Tables =
					[
						new EfTable
						{
							Name = "T",
							ClrTypeName = "N.T",
							Columns = [CreateColumn("Total", "decimal(18,2)")],
						},
					],
				},
			],
		};

		// Act
		var body = Body(MermaidGenerator.Generate(model, CreateOptions()));

		// Assert
		await Assert.That(body).Contains("decimal(18_2) Total");
		await Assert.That(body).DoesNotContain("decimal(18,2)");
	}

	[Test]
	public async Task Generate_ColumnComment_IsEmittedAsAMermaidAttributeComment()
	{
		// Arrange
		EfModel model = new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "C",
					TypeName = "N.C",
					Tables =
					[
						new EfTable
						{
							Name = "T",
							ClrTypeName = "N.T",
							Columns = [CreateColumn("Status") with { Comment = "Order lifecycle state" }],
						},
					],
				},
			],
		};

		// Act
		var body = Body(MermaidGenerator.Generate(model, CreateOptions()));

		// Assert
		await Assert.That(body).Contains("int Status \"Order lifecycle state\"");
	}

	[Test]
	public async Task Generate_ColumnComment_IsEmittedAsADbmlNote()
	{
		// Arrange
		EfModel model = new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "C",
					TypeName = "N.C",
					Tables =
					[
						new EfTable
						{
							Name = "T",
							ClrTypeName = "N.T",
							Columns = [CreateColumn("Status") with { Comment = "Order lifecycle state" }],
						},
					],
				},
			],
		};

		// Act
		var body = Body(DbmlGenerator.Generate(model, CreateOptions(ErdRenderer.Dbml)));

		// Assert
		await Assert.That(body).Contains("""Status int [not null, note: "Order lifecycle state"]""");
	}

	[Test]
	public async Task Generate_MultipleContexts_PinsTheLabelColourAlongsideEachContextFill()
	{
		// A classDef that sets a fill but not a colour leaves the label to the active Mermaid theme,
		// which under the dark theme is near-white and illegible on these pale fills.
		// Arrange
		EfModel model = new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "FirstDbContext",
					TypeName = "N.FirstDbContext",
					Tables =
					[
						new EfTable
						{
							Name = "A",
							ClrTypeName = "N.A",
							Columns = [CreateColumn("Id")],
						},
					],
				},
				new EfContext
				{
					Name = "SecondDbContext",
					TypeName = "N.SecondDbContext",
					Tables =
					[
						new EfTable
						{
							Name = "B",
							ClrTypeName = "N.B",
							Columns = [CreateColumn("Id")],
						},
					],
				},
			],
		};

		// Act
		var body = Body(MermaidGenerator.Generate(model, CreateOptions()));

		// Assert
		await Assert.That(body).Contains("classDef FirstDbContext fill:#dbeafe,stroke:#3b82f6,color:#1f2937");
		await Assert.That(body).Contains("classDef SecondDbContext fill:#dcfce7,stroke:#22c55e,color:#1f2937");
	}

	[Test]
	public async Task Generate_WithSubgraphsDisabled_EmitsFlatOutput()
	{
		// Arrange
		var options = CreateOptions(useSubgraphs: false);

		// Act
		var body = Body(MermaidGenerator.Generate(CreateModel(), options));

		// Assert
		await Assert.That(body).DoesNotContain("subgraph");
	}

	// DBML

	[Test]
	public async Task Generate_SingleContext_EmitsExactDbmlBody()
	{
		// Arrange
		var options = CreateOptions(ErdRenderer.Dbml);

		// Act
		var body = Body(DbmlGenerator.Generate(CreateModel(), options));

		// Assert
		await Assert
			.That(body)
			.IsEqualTo(
				"""
				Table Blogs {
					Id int [pk]

					Note: "BlogDbContext (Sample.Blog)"
				}

				Table Posts {
					Id int [pk]
					Title varchar(200) [not null]
					Summary varchar
					BlogId int [not null]

					Note: "BlogDbContext (Sample.Post)"
				}

				Ref: Posts.BlogId > Blogs.Id
				""".ReplaceLineEndings("\n")
			);
	}

	[Test]
	public async Task Generate_UniqueForeignKey_EmitsOneToOneReference()
	{
		// Arrange
		var model = CreateModel(isUnique: true);

		// Act
		var body = Body(DbmlGenerator.Generate(model, CreateOptions(ErdRenderer.Dbml)));

		// Assert — '-' is one-to-one in DBML, '>' is many-to-one.
		await Assert.That(body).Contains("Ref: Posts.BlogId - Blogs.Id");
	}

	[Test]
	public async Task Generate_WithSchema_QualifiesTableNames()
	{
		// Arrange
		var model = CreateModel(schema: "blogging");

		// Act
		var body = Body(DbmlGenerator.Generate(model, CreateOptions(ErdRenderer.Dbml)));

		// Assert
		await Assert.That(body).Contains("Table blogging.Blogs {");
		await Assert.That(body).Contains("Ref: blogging.Posts.BlogId > blogging.Blogs.Id");
	}

	// Renderer selection

	[Test]
	public async Task Create_DefaultRenderer_ProducesJsonAndViewerAssets()
	{
		// Arrange
		AspireC4EfDiagramOptions options = new() { FileName = "schema.gen" };

		// Act
		var files = ErdOutput.Create(CreateModel(), options);

		// Assert — the default is Interactive, which needs its data plus the viewer.
		await Assert.That(options.Renderer).IsEqualTo(ErdRenderer.Interactive);
		await Assert.That(files.Select(f => f.FileName)).IsEquivalentTo(["schema.gen.json", "index.html", "app.js"]);
	}

	[Test]
	[Arguments(ErdRenderer.Dbml, "schema.gen.dbml")]
	[Arguments(ErdRenderer.D2, "schema.gen.d2")]
	public async Task Create_InterchangeRenderer_ProducesASingleFile(ErdRenderer renderer, string expected)
	{
		// DBML is for interchange and D2 is served by its own sidecar, so neither emits a viewer.
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = renderer, FileName = "schema.gen" };

		// Act
		var files = ErdOutput.Create(CreateModel(), options);

		// Assert
		await Assert.That(files.Count).IsEqualTo(1);
		await Assert.That(files[0].FileName).IsEqualTo(expected);
	}

	[Test]
	public async Task Create_MermaidRenderer_ProducesTheDiagramAndItsPreview()
	{
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = ErdRenderer.Mermaid, FileName = "schema.gen" };

		// Act
		var files = ErdOutput.Create(CreateModel(), options);

		// Assert
		await Assert.That(files[0].FileName).IsEqualTo("schema.gen.mmd");
		await Assert
			.That(files.Select(f => f.FileName))
			.IsEquivalentTo(["schema.gen.mmd", "index.html", "mermaid-app.js"]);
	}

	[Test]
	public async Task Create_MermaidPreview_PointsAtTheGeneratedDiagram()
	{
		// The placeholder must be substituted, or the preview loads nothing.
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = ErdRenderer.Mermaid, FileName = "schema.gen" };

		// Act
		var script = ErdOutput.Create(CreateModel(), options).Single(f => f.FileName == "mermaid-app.js");

		// Assert
		await Assert.That(script.Content).Contains("schema.gen.mmd");
		await Assert.That(script.Content).DoesNotContain("__MODEL__");
	}

	[Test]
	public async Task Create_RunTwice_ProducesIdenticalOutputForEveryRenderer()
	{
		// Arrange
		var model = CreateModel();

		foreach (var renderer in Enum.GetValues<ErdRenderer>())
		{
			AspireC4EfDiagramOptions options = new() { Renderer = renderer };

			// Act — the header carries a timestamp, so bodies are compared.
			var first = ErdOutput.Create(model, options).Select(f => Body(f.Content)).ToArray();
			var second = ErdOutput.Create(model, options).Select(f => Body(f.Content)).ToArray();

			// Assert
			await Assert.That(second).IsEquivalentTo(first);
		}
	}
}
