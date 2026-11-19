using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4.Lifecycle;

public sealed partial class AspireC4LifecycleHookTests
{
	[Test]
	public async Task SetupContainerBindMount_WithTheDefaultOutputDirectory_ResolvesItAgainstTheAppHostDirectory()
	{
		// Arrange
		// A polyglot AppHost runs AspireC4 in the Aspire CLI's AppHost server, whose working directory is not
		// the AppHost's; the default output must still land beside the AppHost, as the ER diagram's does.
		var appHostDirectory = P("project", "apphost");
		ContainerWorkspaceOptions workspaceOptions = new() { AppHostDirectory = appHostDirectory };
		LikeC4ServerResource serverResource = new("test");
		using var sut = CreateLifecycleHookSut(workspaceOptions: workspaceOptions);

		// Act
		sut.SetupContainerBindMount(null!, serverResource);

		// Assert
		var mount = serverResource.Annotations.OfType<ContainerMountAnnotation>().Single();
		await Assert.That(mount.Source).IsEqualTo(Path.Combine(appHostDirectory, ".aspirec4", "likec4"));
	}

	[Test]
	public async Task ResolveBaseDirectory_WhenTheAppHostDirectoryIsUnset_FallsBackToTheCurrentDirectory()
	{
		// Arrange
		ContainerWorkspaceOptions sut = new();

		// Act
		var directory = sut.ResolveBaseDirectory();

		// Assert
		await Assert.That(directory).IsEqualTo(Environment.CurrentDirectory);
	}

	[Test]
	public async Task AddAspireC4_RecordsTheAppHostDirectoryForTheLifecycleHook()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();

		// Act
		appBuilder.AddAspireC4();
		using var services = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions = services.GetRequiredService<IOptions<ContainerWorkspaceOptions>>().Value;

		// Assert
		await Assert.That(workspaceOptions.AppHostDirectory).IsEqualTo(appBuilder.AppHostDirectory);
	}
}
