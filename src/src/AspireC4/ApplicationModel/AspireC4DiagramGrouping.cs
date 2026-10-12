namespace Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// Owns the shared dashboard parent for AspireC4's diagram resources.
/// </summary>
/// <remarks>
/// Grouping is created lazily, by the first diagram registered after the architecture diagram,
/// because <c>AddAspireC4()</c> cannot know whether a second diagram is coming. An AppHost that only
/// ever adds the architecture diagram therefore keeps a single flat row — a parent with one child is
/// just an extra click.
/// <para>
/// The decision is made when the builder runs, so turning
/// <see cref="AspireC4DiagramOptions.HideFromDashboard"/> on through configuration rather than the
/// <c>configure</c> callback leaves the parent in place with one visible child.
/// </para>
/// </remarks>
static class AspireC4DiagramGrouping
{
	/// <summary>Suffix appended to the architecture diagram's row once it is nested.</summary>
	internal const string LikeC4ResourceSuffix = "-likec4";

	/// <summary>The resource type reported for the group row in the dashboard.</summary>
	internal const string GroupResourceType = "AspireC4";

	/// <summary>
	/// Returns the parent row the AspireC4 diagrams nest under, creating it on first call.
	/// </summary>
	/// <param name="builder">The architecture diagram's resource builder.</param>
	/// <returns>
	/// The group, or <see langword="null"/> when there is no second row to pair with — because the
	/// architecture diagram is hidden from the dashboard, or because
	/// <see cref="AspireC4DiagramOptions.UseLegacyLayout"/> asked for the flat pre-13.6 names.
	/// </returns>
	internal static AspireC4DiagramGroupResource? EnsureGroup(IResourceBuilder<AspireC4Resource> builder)
	{
		var applicationBuilder = builder.ApplicationBuilder;
		var existing = applicationBuilder.Resources.OfType<AspireC4DiagramGroupResource>().FirstOrDefault();

		if (existing is not null)
		{
			return existing;
		}

		var likeC4 = builder.Resource;

		// Grouping renames the architecture diagram's row, which is exactly what the legacy layout
		// exists to avoid: an AppHost opting into it keeps the flat aspirec4 / aspirec4-er pair.
		if (likeC4.UsesLegacyLayout || likeC4.HiddenFromDashboard)
		{
			return null;
		}

		// The group takes over the name AddAspireC4() was called with, so the grouped tree reads as
		// aspirec4 → aspirec4-likec4 + aspirec4-er. Aspire's resource collection keys by name when a
		// resource is added, so the architecture diagram has to be re-registered for the rename to free
		// up the old name — otherwise adding the group is rejected as a duplicate.
		applicationBuilder.Resources.Remove(likeC4);
		likeC4.RenameTo(likeC4.BaseName + LikeC4ResourceSuffix);
		applicationBuilder.AddResource(likeC4);

		AspireC4DiagramGroupResource group = new(likeC4.BaseName);

		applicationBuilder
			.AddResource(group)
			.ExcludeFromLikeC4()
			.ExcludeFromManifest()
			// Nothing runs behind this row, so it has to publish its own state or the dashboard leaves
			// it Unknown forever — the same reason the ER diagram resource seeds one.
			.WithInitialState(
				new CustomResourceSnapshot
				{
					ResourceType = GroupResourceType,
					IsHidden = false,
					State = KnownResourceStates.Running,
					Properties = [],
				}
			);

		builder.WithParentRelationship(group);

		return group;
	}
}
