namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

public sealed class LikeC4DSLHelpersTests
{
	[Test]
	public async Task ExtractSpecificationItems_WithTagDeclaration_ExtractsTags()
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			  tag my-tag
			  tag external
			}
			""";

		// Act
		var result = LikeC4DSLHelpers.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.Tags).Contains("my-tag");
		await Assert.That(result.Tags).Contains("external");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithElementDeclaration_ExtractsElementKinds()
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			  element executable
			  element service
			}
			""";

		// Act
		var result = LikeC4DSLHelpers.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.ElementKinds).Contains("container");
		await Assert.That(result.ElementKinds).Contains("executable");
		await Assert.That(result.ElementKinds).Contains("service");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithRelationshipDeclaration_ExtractsRelationshipKinds()
	{
		// Arrange
		const string dsl = """
			specification {
			  relationship async
			  relationship RESP
			  relationship tcp-ip
			}
			""";

		// Act
		var result = LikeC4DSLHelpers.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.RelationshipKinds).Contains("async");
		await Assert.That(result.RelationshipKinds).Contains("RESP");
		await Assert.That(result.RelationshipKinds).Contains("tcp-ip");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithEmptyText_ReturnsEmptyDefinitions()
	{
		// Arrange
		const string dsl = "";

		// Act
		var result = LikeC4DSLHelpers.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.Tags).IsEmpty();
		await Assert.That(result.ElementKinds).IsEmpty();
		await Assert.That(result.RelationshipKinds).IsEmpty();
	}

	[Test]
	public async Task ExtractSpecificationItems_WithFullGeneratedFile_ExtractsAllDeclarations()
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			  element executable
			  relationship RESP
			  relationship tcp-ip
			  tag aspire-run-state-finished
			  tag aspire-run-state-running
			  tag local-dev
			}

			model {
			  redis = container 'redis' {
			    #local-dev
			    link https://redis.io/ 'Redis'
			  }
			  redis -> container_other 'Connects'
			}
			""";

		// Act
		var result = LikeC4DSLHelpers.ExtractSpecificationItems(dsl);

		// Assert
		await Assert.That(result.Tags).Contains("aspire-run-state-finished");
		await Assert.That(result.Tags).Contains("aspire-run-state-running");
		await Assert.That(result.Tags).Contains("local-dev");
		await Assert.That(result.ElementKinds).Contains("container");
		await Assert.That(result.ElementKinds).Contains("executable");
		await Assert.That(result.RelationshipKinds).Contains("RESP");
		await Assert.That(result.RelationshipKinds).Contains("tcp-ip");
	}

	[Test]
	public async Task ExtractSpecificationItems_WithExtendBlockInModel_DoesNotFalselyExtract()
	{
		// Arrange — model block with #tag (hash-prefix) should not be picked up as a declaration
		const string dsl = """
			model {
			  extend azure_redis {
			    link https://redis.io/ 'Redis'
			    metadata {
			      team 'Platform'
			    }
			  }
			}
			""";

		// Act

		var result = LikeC4DSLHelpers.ExtractSpecificationItems(dsl);

		// Assert — nothing from the model block should be extracted
		await Assert.That(result.Tags).IsEmpty();
		await Assert.That(result.ElementKinds).IsEmpty();
		await Assert.That(result.RelationshipKinds).IsEmpty();
	}
}
