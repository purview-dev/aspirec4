using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Icons;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithPackageTechnology_AddsAPackageRule_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithPackageTechnology(" MassTransit ", "MassTransit", "tech:rabbitmq");

		// Assert
		await Assert
			.That(sut.TechnologyOverrides)
			.Contains(
				new TechnologyOverride(TechnologyOverrideMatch.Package, "MassTransit", "MassTransit", "tech:rabbitmq")
			);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithResourceTypeTechnology_ByName_AddsATypeRule()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithResourceTypeTechnology("Acme.Hosting.WidgetResource", "Widget");

		// Assert
		await Assert
			.That(sut.TechnologyOverrides)
			.Contains(
				new TechnologyOverride(TechnologyOverrideMatch.ResourceType, "Acme.Hosting.WidgetResource", "Widget")
			);
	}

	[Test]
	public async Task WithResourceTypeTechnology_Generic_UsesTheTypesFullName()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithResourceTypeTechnology<ContainerResource>("Container", "tech:docker");

		// Assert
		await Assert
			.That(sut.TechnologyOverrides.Single().Pattern)
			.IsEqualTo("Aspire.Hosting.ApplicationModel.ContainerResource");
	}

	[Test]
	public async Task WithContainerImageTechnology_AddsAnImageRule_InRegistrationOrder()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithContainerImageTechnology("acme/one", "One").WithContainerImageTechnology("acme/two", "Two");

		// Assert
		await Assert.That(sut.TechnologyOverrides.Select(o => o.Technology)).IsEquivalentTo(["One", "Two"]);
		await Assert.That(sut.TechnologyOverrides.All(o => o.Match == TechnologyOverrideMatch.ContainerImage)).IsTrue();
	}

	[Test]
	[Arguments("", "Widget")]
	[Arguments("acme/widget", " ")]
	public async Task WithContainerImageTechnology_BlankPatternOrTechnology_Throws(string image, string technology)
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithContainerImageTechnology(image, technology)).Throws<ArgumentException>();
	}
}
