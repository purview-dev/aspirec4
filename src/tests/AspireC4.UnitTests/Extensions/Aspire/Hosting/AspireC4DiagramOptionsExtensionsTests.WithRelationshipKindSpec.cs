using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_AddsSpec_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithRelationshipKindSpec("async");

		// Assert
		await Assert.That(sut.RelationshipKindSpecs.Any(s => s.Name == "async")).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_WithTechnology_SetsSpecTechnology()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithRelationshipKindSpec("grpc", technology: "gRPC");

		// Assert
		await Assert.That(sut.RelationshipKindSpecs.Single(s => s.Name == "grpc").Technology).IsEqualTo("gRPC");
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_AddsSpec_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		LikeC4RelationshipKindSpec spec = new("async");

		// Act
		var result = sut.WithRelationshipKindSpec(spec);

		// Assert
		await Assert.That(sut.RelationshipKindSpecs).Contains(spec);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_NullSpec_Throws()
	{
		// Arrange
		LikeC4RelationshipKindSpec spec = null!;

		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithRelationshipKindSpec(spec)).Throws<ArgumentNullException>();
	}
}
