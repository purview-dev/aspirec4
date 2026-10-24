using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithGeneratedViewId_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithGeneratedViewId("custom");

		// Assert
		await Assert.That(sut.GeneratedViewId).IsEqualTo("custom");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithGeneratedViewId_AcceptsNull()
	{
		// Arrange
		var sut = CreateSut();
		sut.GeneratedViewId = "something";

		// Act
		sut.WithGeneratedViewId(null);

		// Assert
		await Assert.That(sut.GeneratedViewId).IsNull();
	}

	[Test]
	public async Task WithDefaultViewId_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithDefaultViewId("home");

		// Assert
		await Assert.That(sut.DefaultViewId).IsEqualTo("home");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithDefaultViewId_AcceptsNull()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithDefaultViewId(null);

		// Assert
		await Assert.That(sut.DefaultViewId).IsNull();
	}

	[Test]
	public async Task WithTitle_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithTitle("My Architecture");

		// Assert
		await Assert.That(sut.Title).IsEqualTo("My Architecture");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithTitle_ThrowsOnWhitespace()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithTitle("   ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithOutputDirectory_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithOutputDirectory("./out");

		// Assert
		await Assert.That(sut.OutputDirectory).IsEqualTo("./out");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithOutputDirectory_ThrowsOnEmpty()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithOutputDirectory("")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithFileName_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithFileName("custom.gen");

		// Assert
		await Assert.That(sut.FileName).IsEqualTo("custom.gen");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithHMRDisabled_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithHMRDisabled();

		// Assert
		await Assert.That(sut.DisableHMR).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithHMRDisabled_FalseReEnablesHMR()
	{
		// Arrange
		var sut = CreateSut();
		sut.DisableHMR = true;

		// Act
		sut.WithHMRDisabled(false);

		// Assert
		await Assert.That(sut.DisableHMR).IsFalse();
	}

	[Test]
	public async Task WithContainerImageTag_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithContainerImageTag("1.56");

		// Assert
		await Assert.That(sut.ContainerImageTag).IsEqualTo("1.56");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithContainerImageTag_AcceptsNull()
	{
		// Arrange
		var sut = CreateSut();
		sut.ContainerImageTag = "1.0";

		// Act
		sut.WithContainerImageTag(null);

		// Assert
		await Assert.That(sut.ContainerImageTag).IsNull();
	}

	[Test]
	public async Task WithAutoIcons_EnablesTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.AutoIconsEnabled = false;

		// Act
		var result = sut.WithAutoIcons();

		// Assert
		await Assert.That(sut.AutoIconsEnabled).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAutoIcons_FalseDisablesAutoIcons()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithAutoIcons(false);

		// Assert
		await Assert.That(sut.AutoIconsEnabled).IsFalse();
	}

	[Test]
	public async Task WithHideFromDashboard_SetsHideAndDisplayName()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithHideFromDashboard("My Diagram");

		// Assert
		await Assert.That(sut.HideFromDashboard).IsTrue();
		await Assert.That(sut.DashboardLinkDisplayName).IsEqualTo("My Diagram");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithHideFromDashboard_UsesDefaultDisplayName()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithHideFromDashboard();

		// Assert
		await Assert.That(sut.HideFromDashboard).IsTrue();
		await Assert.That(sut.DashboardLinkDisplayName).IsEqualTo("Architecture Diagram");
	}

	[Test]
	public async Task WithHideFromDashboard_ThrowsOnWhitespaceDisplayName()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithHideFromDashboard("  ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithRelationshipKindSyntax_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax.Bracket);

		// Assert
		await Assert.That(sut.RelationshipKindSyntax).IsEqualTo(LikeC4RelationshipKindSyntax.Bracket);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithFormatGeneratedFile_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.FormatGeneratedFile = false;

		// Act
		var result = sut.WithFormatGeneratedFile();

		// Assert
		await Assert.That(sut.FormatGeneratedFile).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithFormatGeneratedFile_FalseDisablesFormatting()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithFormatGeneratedFile(false);

		// Assert
		await Assert.That(sut.FormatGeneratedFile).IsFalse();
	}

	[Test]
	public async Task WithAutoIncludeAspireMetadata_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAutoIncludeAspireMetadata(AspireMetadataInclusion.None);

		// Assert
		await Assert.That(sut.AutoIncludeAspireMetadata).IsEqualTo(AspireMetadataInclusion.None);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithNormaliseMetadataBehaviour_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour.Throw);

		// Assert
		await Assert.That(sut.NormaliseMetadataBehaviour).IsEqualTo(NormaliseMetadataBehaviour.Throw);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithoutConfigFileGeneration_SetsGenerateConfigFileFalse()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithoutConfigFileGeneration();

		// Assert
		await Assert.That(sut.GenerateConfigFile).IsFalse();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAspireDashboardLinks_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.IncludeAspireDashboardLinks = false;

		// Act
		var result = sut.WithAspireDashboardLinks();

		// Assert
		await Assert.That(sut.IncludeAspireDashboardLinks).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAspireDashboardLinks_FalseDisablesLinks()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithAspireDashboardLinks(false);

		// Assert
		await Assert.That(sut.IncludeAspireDashboardLinks).IsFalse();
	}

	[Test]
	public async Task WithAspireTokenInDashboardLinks_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAspireTokenInDashboardLinks();

		// Assert
		await Assert.That(sut.IncludeAspireTokenInDashboardLinks).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAspireTokenInDashboardLinks_FalseDisablesToken()
	{
		// Arrange
		var sut = CreateSut();
		sut.IncludeAspireTokenInDashboardLinks = true;

		// Act
		sut.WithAspireTokenInDashboardLinks(false);

		// Assert
		await Assert.That(sut.IncludeAspireTokenInDashboardLinks).IsFalse();
	}

	[Test]
	public async Task WithDefaultStateStyles_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.IncludeDefaultStateStyles = false;

		// Act
		var result = sut.WithDefaultStateStyles();

		// Assert
		await Assert.That(sut.IncludeDefaultStateStyles).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithDefaultStateStyles_FalseDisablesStyles()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithDefaultStateStyles(false);

		// Assert
		await Assert.That(sut.IncludeDefaultStateStyles).IsFalse();
	}

	[Test]
	public async Task ExportImages_ByDefault_IsOnForPng()
	{
		// Act
		var sut = CreateSut();

		// Assert
		await Assert.That(sut.ExportImages).IsTrue();
		await Assert.That(sut.ImageFormats).IsEquivalentTo([LikeC4ImageFormat.Png]);
		await Assert.That(sut.ImageExportDirectory).IsNull();
	}

	[Test]
	public async Task WithImageExport_ReplacesTheDefaultFormats_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithImageExport(LikeC4ImageFormat.Jpg);

		// Assert
		await Assert.That(sut.ImageFormats).IsEquivalentTo([LikeC4ImageFormat.Jpg]);
		await Assert.That(sut.ExportImages).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithImageExport_NoFormats_TurnsExportOff()
	{
		// Naming no format is a request for no images, not a request for the default.
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithImageExport();

		// Assert
		await Assert.That(sut.ExportImages).IsFalse();
		await Assert.That(sut.ImageFormats).IsEmpty();
	}

	[Test]
	public async Task WithoutImageExport_TurnsExportOff_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithoutImageExport();

		// Assert
		await Assert.That(sut.ExportImages).IsFalse();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithImageExportDirectory_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithImageExportDirectory("../diagrams");

		// Assert
		await Assert.That(sut.ImageExportDirectory).IsEqualTo("../diagrams");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task WithImageExportDirectory_BlankPath_Throws(string? directory)
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		await Assert.That(() => sut.WithImageExportDirectory(directory!)).Throws<ArgumentException>();
	}

	static AspireC4DiagramOptions CreateSut() => new();
}
