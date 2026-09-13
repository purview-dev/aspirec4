namespace Aspire.Hosting.AspireC4.TestAppHost;

/// <summary>
/// Registry of known LikeC4 values for the AspireC4 test app host.
/// These constants are the source of truth — they replace the old auto-generated
/// <c>KnownLikeC4Registry</c> class.
/// </summary>
[LikeC4Registry]
static class AppLikeC4Registry
{
	/// <summary>
	/// Known tag values used with <c>.WithTag()</c>.
	/// </summary>
	internal static class Tags
	{
		public const string LocalDev = "local-dev";
	}

	/// <summary>
	/// Known element kinds used with <c>.WithKind()</c>.
	/// </summary>
	internal static class ElementKinds;

	/// <summary>
	/// Known relationship kinds used with <c>.WithKind()</c>.
	/// </summary>
	internal static class RelationshipKinds
	{
		public const string Resp = "RESP";

		public const string TcpIp = "tcp-ip";
	}

	/// <summary>
	/// Known group names used with <c>.WithLikeC4Group()</c>.
	/// </summary>
	internal static class Groups
	{
		public const string LocalDevSyncGroup = "Local Dev/ Sync Group";
	}

	/// <summary>
	/// Known metadata keys used with <c>.WithMetadata()</c>.
	/// </summary>
	internal static class MetadataKeys
	{
		public const string AzureSku = "Azure_SKU";

		public const string UseCase = "Use_Case";
	}
}
