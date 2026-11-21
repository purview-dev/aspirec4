using System.Text.RegularExpressions;

namespace Aspire.Hosting.AspireC4;

/// <summary>Covers the AppHost identity export containers hand their images to.</summary>
public sealed partial class HostUserTests
{
	[GeneratedRegex(@"^\d+:\d+$")]
	private static partial Regex UidGidPattern();

	static IReadOnlyList<string> CreateCommand() => ["/usr/local/bin/likec4", "export", "png", "-o", "/out"];

	[Test]
	public async Task ContainerUser_OnThisHost_IsUidColonGidOrNullOnWindows()
	{
		// Arrange
		var isWindows = OperatingSystem.IsWindows();

		// Act
		var user = HostUser.ContainerUser;

		// Assert
		if (isWindows)
		{
			await Assert.That(user).IsNull();
		}
		else
		{
			await Assert.That(user).IsNotNull();
			await Assert.That(UidGidPattern().IsMatch(user!)).IsTrue();
		}
	}

	[Test]
	[Arguments(null)]
	[Arguments(HostUser.Root)]
	public async Task HandBackOwnership_WithNoUserOrRoot_LeavesTheCommandAlone(string? containerUser)
	{
		// Root already owns what root writes, and Windows has no uid to hand back to.
		// Arrange
		var command = CreateCommand();

		// Act
		var (before, after) = HostUser.HandBackOwnership(containerUser, "/out", command);

		// Assert
		await Assert.That(before).IsEmpty();
		await Assert.That(after).IsEquivalentTo(command);
	}

	[Test]
	public async Task HandBackOwnership_WithAUser_RunsTheCommandThroughAShellThatChownsRootsFiles()
	{
		// Arrange
		var command = CreateCommand();

		// Act
		var (before, after) = HostUser.HandBackOwnership("1000:1000", "/out", command);

		// Assert
		await Assert.That(before).IsEquivalentTo(["--entrypoint", "sh"]);
		await Assert.That(after[0]).IsEqualTo("-c");
		await Assert
			.That(after[1])
			.IsEqualTo("\"$@\"; status=$?; find '/out' -user 0 -exec chown 1000:1000 {} +; exit $status");
		await Assert.That(after[2]).IsEqualTo("sh");
		await Assert.That(after.Skip(3)).IsEquivalentTo(command);
	}
}
