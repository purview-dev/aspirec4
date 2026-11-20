using System.Text.RegularExpressions;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>Covers the AppHost identity passed to the d2 export container.</summary>
public sealed partial class HostUserTests
{
	[GeneratedRegex(@"^\d+:\d+$")]
	private static partial Regex UidGidPattern();

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
}
