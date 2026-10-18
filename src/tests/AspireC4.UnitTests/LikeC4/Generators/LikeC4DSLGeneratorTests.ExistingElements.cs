using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4.Generators;

/// <summary>
/// Covers elements mapped onto a hand-authored LikeC4 project with <c>WithLikeC4Element</c>, which are
/// emitted as <c>extend</c> blocks rather than declarations.
/// </summary>
public sealed class LikeC4DSLGeneratorExistingElementTests
{
	// Helpers

	static AspireC4DiagramOptions CreateOptions() =>
		new()
		{
			Title = "Test Architecture",
			ViewTitle = "Test Architecture",
			FileName = "model.gen",
		};

	static LikeC4Element CreateExistingElement(
		string name = "cloud.backend.api",
		string label = "api",
		string kind = "service",
		IReadOnlyList<string>? tags = null,
		IReadOnlyList<LikeC4Link>? links = null,
		IReadOnlyList<LikeC4Metadata>? metadata = null
	) =>
		new()
		{
			Name = name,
			Label = label,
			Kind = kind,
			ExtendsExistingElement = true,
			Tags = tags ?? ["aspire-run-state-running"],
			Links = links ?? [],
			Metadata = metadata ?? [],
		};

	static LikeC4Element CreateElement(string name = "db", string label = "db", string kind = "container") =>
		new()
		{
			Name = name,
			Label = label,
			Kind = kind,
		};

	// Tests

	[Test]
	public async Task Generate_ExistingElement_EmitsAnExtendBlockRatherThanADeclaration()
	{
		// Arrange
		LikeC4Model model = new() { Elements = [CreateExistingElement()], Relationships = [] };

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("extend cloud.backend.api {");
		await Assert.That(dsl).DoesNotContain("cloud.backend.api = service");
	}

	[Test]
	public async Task Generate_ExistingElement_PreservesTheDotsInTheIdentifier()
	{
		// Sanitisation turns every non-identifier character into an underscore, which would destroy the
		// hierarchy separators in a fully-qualified reference.
		// Arrange
		LikeC4Model model = new() { Elements = [CreateExistingElement()], Relationships = [] };

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).DoesNotContain("cloud_backend_api");
	}

	[Test]
	public async Task Generate_ExistingElement_DoesNotDeclareItsKindInTheSpecification()
	{
		// The hand-authored project declares the kind; declaring it again collides with that.
		// Arrange
		LikeC4Model model = new() { Elements = [CreateExistingElement(kind: "service")], Relationships = [] };

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).DoesNotContain("element service");
	}

	[Test]
	public async Task Generate_ExistingElement_StillDeclaresItsStateTagInTheSpecification()
	{
		// The state tag is AspireC4's own, so the generated file has to declare it.
		// Arrange
		LikeC4Model model = new()
		{
			Elements = [CreateExistingElement(tags: ["aspire-run-state-running"])],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("tag aspire-run-state-running");
		await Assert.That(dsl).Contains("#aspire-run-state-running");
	}

	[Test]
	public async Task Generate_ExistingElement_WritesNoTitle()
	{
		// LikeC4's extend body has no title; the project the element comes from keeps its own.
		// Arrange
		LikeC4Model model = new() { Elements = [CreateExistingElement(label: "Backend API")], Relationships = [] };

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert — the view's own title line is unrelated, so match the element's label specifically.
		await Assert.That(dsl).DoesNotContain("title 'Backend API'");
	}

	[Test]
	public async Task Generate_ExistingElementWithNothingToApply_EmitsNoExtendBlock()
	{
		// An extend block with an empty body is not valid DSL.
		// Arrange
		LikeC4Model model = new() { Elements = [CreateExistingElement(tags: [])], Relationships = [] };

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).DoesNotContain("extend ");
	}

	[Test]
	public async Task Generate_ExistingElement_WritesTheSuppliedLinksAndMetadata()
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				CreateExistingElement(
					links: [new LikeC4Link("http://localhost:5123", "Endpoint: http")],
					metadata: [new LikeC4Metadata("aspire-name", "api")]
				),
			],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("link http://localhost:5123 'Endpoint: http'");
		await Assert.That(dsl).Contains("aspire-name 'api'");
	}

	[Test]
	public async Task Generate_RelationshipToAnExistingElement_KeepsTheQualifiedIdentifier()
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements = [CreateExistingElement(), CreateElement()],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "cloud.backend.api",
					TargetName = "db",
					Label = "Persists data",
				},
			],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("cloud.backend.api -> db 'Persists data'");
	}

	[Test]
	public async Task Generate_GroupContainingAnExistingElement_KeepsTheQualifiedIdentifier()
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements = [CreateExistingElement() with { Group = "Backend" }],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, CreateOptions());

		// Assert
		await Assert.That(dsl).Contains("include cloud.backend.api");
	}
}
