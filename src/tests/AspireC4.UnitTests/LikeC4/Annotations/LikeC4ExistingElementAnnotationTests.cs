namespace Aspire.Hosting.AspireC4.LikeC4.Annotations;

public sealed class LikeC4ExistingElementAnnotationTests
{
	[Test]
	public async Task Constructor_WithAQualifiedIdentifier_KeepsItVerbatim()
	{
		// Arrange
		const string elementId = "cloud.backend.api";

		// Act
		LikeC4ExistingElementAnnotation annotation = new(elementId);

		// Assert
		await Assert.That(annotation.ElementId).IsEqualTo(elementId);
	}

	[Test]
	[Arguments("api")]
	[Arguments("cloud.backend.api")]
	[Arguments("_internal.api-gateway")]
	public async Task Constructor_WithAValidIdentifier_DoesNotThrow(string elementId)
	{
		// Arrange & Act & Assert
		await Assert.That(() => new LikeC4ExistingElementAnnotation(elementId)).ThrowsNothing();
	}

	[Test]
	[Arguments("cloud..api")]
	[Arguments("1cloud.api")]
	[Arguments("cloud.api ")]
	[Arguments("cloud api")]
	[Arguments(".api")]
	public async Task Constructor_WithAnUnusableIdentifier_Throws(string elementId)
	{
		// Arrange & Act & Assert
		await Assert.That(() => new LikeC4ExistingElementAnnotation(elementId)).Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_WithNoIdentifier_Throws()
	{
		// Arrange & Act & Assert
		await Assert.That(() => new LikeC4ExistingElementAnnotation("  ")).Throws<ArgumentException>();
	}
}
