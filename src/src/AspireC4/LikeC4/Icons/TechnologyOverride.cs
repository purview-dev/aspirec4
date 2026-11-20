namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

/// <summary>What a <see cref="TechnologyOverride"/> is matched against.</summary>
public enum TechnologyOverrideMatch
{
	/// <summary>
	/// A dependency of the resource's own project: an npm package in <c>package.json</c>, a Python package
	/// in <c>pyproject.toml</c> or <c>requirements.txt</c>, or a NuGet package, framework reference or MSBuild
	/// SDK of a .NET project. Package-manager installer resources are never matched.
	/// </summary>
	Package,

	/// <summary>
	/// The resource's type or any of its base types: a full name with its namespace
	/// (<c>Acme.Hosting.WidgetResource</c>) matches exactly, and a short name (<c>WidgetResource</c> or
	/// <c>Widget</c>) matches the class name with or without its <c>Resource</c> suffix. The resource type
	/// the dashboard reports is matched too.
	/// </summary>
	ResourceType,

	/// <summary>
	/// The resource's container image, ignoring the registry, the <c>library/</c> namespace, the tag and any
	/// digest. A pattern with a slash (<c>datalust/seq</c>) must match the whole repository path; one without
	/// (<c>seq</c>) matches its last segment, in any namespace.
	/// </summary>
	ContainerImage,
}

/// <summary>
/// An application-wide rule that names a technology, and optionally its icon, for every resource it
/// matches. Overrides are consulted before AspireC4's built-in technology catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Rules are tried by kind — <see cref="TechnologyOverrideMatch.Package"/>, then
/// <see cref="TechnologyOverrideMatch.ResourceType"/>, then <see cref="TechnologyOverrideMatch.ContainerImage"/>
/// — and within a kind in registration order; the first match wins. A technology or icon set on the
/// resource itself through <c>WithLikeC4Details</c> still wins over any rule.
/// </para>
/// <para>
/// When <see cref="Icon"/> is <see langword="null"/>, the icon is matched from <see cref="Technology"/>, as it
/// would be for a technology set on the resource.
/// </para>
/// </remarks>
/// <param name="Match">What <paramref name="Pattern"/> is matched against.</param>
/// <param name="Pattern">The package, resource type or container image to match.</param>
/// <param name="Technology">The technology label shown on matched elements, e.g. <c>Seq</c>.</param>
/// <param name="Icon">
/// The LikeC4 icon, e.g. <c>tech:grafana</c>, <c>azure:key-vaults</c> or an image alias such as
/// <c>@/icons/seq.svg</c>; <see langword="null"/> to match one from <paramref name="Technology"/>.
/// </param>
public sealed record TechnologyOverride(
	TechnologyOverrideMatch Match,
	string Pattern,
	string Technology,
	string? Icon = null
);
