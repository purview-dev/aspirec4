namespace Aspire.Hosting.AspireC4;

public static class TestHelpers
{
	public static IDistributedApplicationBuilder CreateAppBuilder(string[]? args = null) =>
		DistributedApplication.CreateBuilder(args ?? []);
}
