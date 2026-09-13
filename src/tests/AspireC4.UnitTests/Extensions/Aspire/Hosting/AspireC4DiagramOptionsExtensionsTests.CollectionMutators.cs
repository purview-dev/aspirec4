using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Icons;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithElementKindSpec_AddsToCollection_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		LikeC4ElementKindSpec spec = new("queue");

		// Act
		var result = sut.WithElementKindSpec(spec);

		// Assert
		await Assert.That(sut.ElementKindSpecs).Contains(spec);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithElementKindSpec_Null_Throws()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithElementKindSpec(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task WithElementKindSpec_MultipleCallsAccumulateSpecs()
	{
		// Arrange
		var sut = CreateSut();
		LikeC4ElementKindSpec s1 = new("queue");
		LikeC4ElementKindSpec s2 = new("topic");

		// Act
		sut.WithElementKindSpec(s1).WithElementKindSpec(s2);

		// Assert
		await Assert.That(sut.ElementKindSpecs.Count).IsEqualTo(2);
	}

	[Test]
	public async Task WithStateTag_UpdatesMapEntry_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithStateTag(KnownResourceStates.Running, "my-running");

		// Assert
		await Assert.That(sut.StateTagMap[KnownResourceStates.Running]).IsEqualTo("my-running");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithStateTag_NullTagSuppressesTagForState()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithStateTag(KnownResourceStates.Running, null);

		// Assert
		await Assert.That(sut.StateTagMap[KnownResourceStates.Running]).IsNull();
	}

	[Test]
	public async Task WithIconResolver_AddsToCollection_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		static string? Resolver(IconResolverContext _) => "tech:dotnet";

		// Act
		var result = sut.WithIconResolver(Resolver);

		// Assert
		await Assert.That(sut.IconResolvers).Contains(Resolver);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithIconResolver_Null_Throws()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithIconResolver(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task WithIconResolver_MultipleCallsAccumulateResolvers()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithIconResolver(_ => "tech:dotnet").WithIconResolver(_ => null);

		// Assert
		await Assert.That(sut.IconResolvers.Count).IsEqualTo(2);
	}
}
