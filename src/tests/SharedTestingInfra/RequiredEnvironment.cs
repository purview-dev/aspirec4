namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Decides whether a missing prerequisite — Docker, or a polyglot AppHost's toolchain — skips a test or
/// fails it.
/// </summary>
/// <remarks>
/// Locally a missing tool skips, so a machine without Docker can still run the unit tests. In CI it must
/// fail: a gate that passes by skipping proves nothing, and the C# integration suites would otherwise go
/// green on a runner whose Docker had stopped.
/// </remarks>
public static class RequiredEnvironment
{
	/// <summary>Set to <c>true</c> or <c>1</c> to make a missing polyglot toolchain fail the test.</summary>
	public const string RequireAppHostsVariable = "ASPIREC4_REQUIRE_APPHOSTS";

	/// <summary>Set to <c>true</c> or <c>1</c> to make a missing container runtime fail the test.</summary>
	public const string RequireDockerVariable = "ASPIREC4_REQUIRE_DOCKER";

	/// <summary>Whether a missing polyglot AppHost toolchain must fail rather than skip.</summary>
	public static bool AppHostsRequired => IsSet(RequireAppHostsVariable);

	/// <summary>Whether a missing container runtime must fail rather than skip.</summary>
	/// <remarks>
	/// True on GitHub Actions without any configuration, because the shared build workflow takes no
	/// environment from its caller and every job that runs these suites is an <c>ubuntu-latest</c> runner
	/// with Docker. Set <see cref="RequireDockerVariable"/> to <c>false</c> to opt a job out.
	/// </remarks>
	public static bool DockerRequired =>
		Environment.GetEnvironmentVariable(RequireDockerVariable) is { } value
			? IsTrue(value)
			: IsSet(RequireAppHostsVariable) || IsTrue(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

	static bool IsSet(string variable) => IsTrue(Environment.GetEnvironmentVariable(variable));

	static bool IsTrue(string? value) =>
		value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}
