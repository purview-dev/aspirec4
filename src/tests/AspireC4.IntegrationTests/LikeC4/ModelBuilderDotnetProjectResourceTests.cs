using Aspire.Hosting.AspireC4.LikeC4.Models;
using Aspire.Hosting.Dotnet;

namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// <c>DotnetProjectResource</c> is what Aspire 17's <c>AddDotnetProject</c> creates. It derives from
/// <c>ExecutableResource</c> rather than <c>ProjectResource</c>, so these guard that it is still drawn
/// as a .NET project instead of falling through to the executable mapping.
/// </summary>
/// <remarks>
/// They live here rather than in AspireC4.UnitTests because the type only exists on the Aspire 17
/// line: the unit tests resolve the libraries' Aspire 13 floor, and this project runs on 17.
/// </remarks>
public sealed class ModelBuilderDotnetProjectResourceTests
{
	[Test]
	public async Task Build_DotnetProjectResource_MapsToComponentKind()
	{
		// Arrange
		var resource = CreateDotnetProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Component);
		await Assert.That(model.Elements[0].Name).IsEqualTo("api");
	}

	[Test]
	public async Task Build_DotnetProjectResource_InfersDotnetTechnologyAndIcon()
	{
		// Arrange
		var resource = CreateDotnetProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Technology).IsEqualTo(".NET");
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	static DotnetProjectResource CreateDotnetProjectResource(string name, string workingDirectory = ".")
	{
#pragma warning disable ASPIREDOTNETPROJECT001 // Experimental in Aspire 17.
		DotnetProjectResource resource = new(name, workingDirectory);
#pragma warning restore ASPIREDOTNETPROJECT001
		return resource;
	}
}
