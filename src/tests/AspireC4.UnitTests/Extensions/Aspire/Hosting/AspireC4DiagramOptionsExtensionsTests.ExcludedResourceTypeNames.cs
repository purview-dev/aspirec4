using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task ExcludedResourceTypeNames_ByDefault_ContainsTheAzureEnvironmentResource()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var names = sut.ExcludedResourceTypeNames;

		// Assert
		await Assert.That(names).Contains("Aspire.Hosting.Azure.AzureEnvironmentResource");
	}

	[Test]
	public async Task WithExcludedResourceType_ByName_AddsTheNameToTheSet_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithExcludedResourceType("Acme.Hosting.InternalResource");

		// Assert
		await Assert.That(sut.ExcludedResourceTypeNames).Contains("Acme.Hosting.InternalResource");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithExcludedResourceType_ByEmptyName_Throws()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		void Act() => sut.WithExcludedResourceType(" ");

		// Assert
		await Assert.That(Act).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithoutExcludedResourceType_ByName_RemovesTheDefault_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithoutExcludedResourceType(AspireC4DiagramOptions.AzureEnvironmentResourceTypeName);

		// Assert
		await Assert.That(sut.ExcludedResourceTypeNames).IsEmpty();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithoutExcludedResourceType_Generic_AlsoRemovesTheTypeName()
	{
		// Arrange
		var sut = CreateSut();
		sut.WithExcludedResourceType(typeof(ContainerResource).FullName!);

		// Act
		sut.WithoutExcludedResourceType<ContainerResource>();

		// Assert
		await Assert.That(sut.ExcludedResourceTypeNames).DoesNotContain(typeof(ContainerResource).FullName!);
	}
}
