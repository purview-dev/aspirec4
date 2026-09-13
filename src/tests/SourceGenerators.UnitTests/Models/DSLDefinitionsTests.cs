namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

public sealed class DSLDefinitionsTests
{
	[Test]
	public async Task DslDefinitions_Empty_HasAnyIsFalse()
	{
		// Arrange / Act
		var empty = DSLDefinitions.Empty;

		// Assert
		await Assert.That(empty.HasAny).IsFalse();
	}

	[Test]
	public async Task DslDefinitions_WithTags_HasAnyIsTrue()
	{
		// Arrange / Act
		DSLDefinitions defs = new(["my-tag"], [], []);

		// Assert
		await Assert.That(defs.HasAny).IsTrue();
	}
}
