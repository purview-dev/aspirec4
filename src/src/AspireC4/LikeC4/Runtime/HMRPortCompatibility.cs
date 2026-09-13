namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

static class HMRPortCompatibility
{
	// The minimum version of LikeC4 that supports configurable HMR ports is v1.57.
	static Version ConfigurableHmrPortMinimumVersion => new(1, 57, 0);

	public static HMRPortMode Resolve(string? loadedVersionTag) =>
		Resolve(loadedVersionTag, ConfigurableHmrPortMinimumVersion);

	public static HMRPortMode Resolve(string? loadedVersionTag, Version? configurableHmrPortMinimumVersion)
	{
		return configurableHmrPortMinimumVersion is null ? HMRPortMode.FixedPort
			: TryParseVersion(loadedVersionTag, out var loadedVersion)
			&& loadedVersion >= configurableHmrPortMinimumVersion
				? HMRPortMode.Configurable
			: HMRPortMode.FixedPort;
	}

	public static bool TryParseVersion(string? loadedVersionTag, out Version loadedVersion)
	{
		loadedVersion = default!;

		if (string.IsNullOrWhiteSpace(loadedVersionTag))
		{
			return false;
		}

		var normalized = loadedVersionTag.Trim();
		if (normalized.StartsWith('v'))
		{
			normalized = normalized[1..];
		}

		var suffixIndex = normalized.IndexOfAny(['-', '+']);
		if (suffixIndex >= 0)
		{
			normalized = normalized[..suffixIndex];
		}

		if (!Version.TryParse(normalized, out var parsedVersion) || parsedVersion is null)
		{
			return false;
		}

		loadedVersion = parsedVersion;
		return true;
	}
}
