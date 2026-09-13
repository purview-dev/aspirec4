using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.AspireC4.Lifecycle;

/// <summary>
/// Unit tests for <see cref="AspireC4LifecycleHook"/> internal helpers.
/// </summary>
public sealed partial class AspireC4LifecycleHookTests
{
	[Test]
	public async Task ResolveAspireBrowserToken_TokenDisabled_WithConfiguredToken_ReturnsNull()
	{
		// Arrange
		// Default behaviour: IncludeAspireTokenInDashboardLinks = false → never expose the token.
		var config = CreateConfig("super-secret");
		var options = CreateOptions(includeToken: false);

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task ResolveAspireBrowserToken_TokenEnabled_WithConfiguredToken_ReturnsToken()
	{
		// Arrange
		// When opt-in is set and a token exists in configuration, the token is returned
		// so that dashboard links are built as /login?t=<token>&returnUrl=<path>.
		var config = CreateConfig("super-secret");
		var options = CreateOptions(includeToken: true);

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsEqualTo("super-secret");
	}

	[Test]
	public async Task ResolveAspireBrowserToken_TokenEnabled_WithNoConfiguredToken_ReturnsNull()
	{
		// Arrange
		// Opt-in is set but no BrowserToken key exists in configuration (e.g. auth disabled).
		var config = CreateConfig(browserToken: null);
		var options = CreateOptions(includeToken: true);

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task ResolveAspireBrowserToken_DefaultOptions_TokenIsDisabledByDefault()
	{
		// Arrange
		// Safety default: IncludeAspireTokenInDashboardLinks must be false out of the box
		// so that tokens are never accidentally embedded in generated diagrams.
		var config = CreateConfig("should-not-appear");
		AspireC4DiagramOptions options = new();

		// Act
		var result = AspireC4LifecycleHook.ResolveAspireBrowserToken(config, options);

		// Assert
		await Assert.That(result).IsNull();
	}

	static IConfigurationMock CreateConfig(string? browserToken)
	{
		var config = IConfiguration.Mock();
		config.Item("AppHost:BrowserToken").Returns(browserToken);

		return config;
	}

	static AspireC4DiagramOptions CreateOptions(bool includeToken) =>
		new() { IncludeAspireTokenInDashboardLinks = includeToken };
}
