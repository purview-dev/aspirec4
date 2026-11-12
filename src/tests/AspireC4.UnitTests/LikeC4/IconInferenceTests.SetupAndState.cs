using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4.LikeC4;

sealed partial class IconInferenceTests
{
	/// <summary>
	/// Encapsulates a single icon inference test case. The display name is derived automatically
	/// from the resources and expected icon — no manual label is required.
	/// </summary>
	/// <param name="CreateResources">
	/// Factory that returns the visible resource and an optional hidden original.
	/// The hidden original simulates an Azure resource that has been replaced by a local
	/// container via <c>RunAsContainer()</c>; it is hidden from the Aspire dashboard but its
	/// type name is still used by the icon matcher.
	/// </param>
	/// <param name="ExpectedIcon">The LikeC4 icon string that must be inferred.</param>
	public sealed record IconTestScenario(
		Func<(IResource Visible, IResource? HiddenOriginal)> CreateResources,
		string ExpectedIcon
	)
	{
		public override string ToString()
		{
			var (visible, hidden) = CreateResources();

			var visibleLabel = visible
				.Annotations.OfType<ResourceSnapshotAnnotation>()
				.Select(a => a.InitialSnapshot.ResourceType)
				.FirstOrDefault(t => t is not null)
				?? visible.Name;

			var hiddenSuffix = hidden is null ? "" : $" [hidden: {hidden.GetType().Name}]";

			return $"{visibleLabel}{hiddenSuffix} → {ExpectedIcon}";
		}
	}

	static GenericTestResource CreateSnapshotResource(string name, string resourceType)
	{
		GenericTestResource resource = new(name);
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = resourceType, Properties = [] })
		);
		return resource;
	}

	static ContainerResource CreateContainerResource(string name) => new(name);

	static T AddHiddenSnapshot<T>(T resource, string resourceType)
		where T : Resource
	{
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = resourceType,
					IsHidden = true,
					Properties = [],
				}
			)
		);
		return resource;
	}

	// Named to match real Aspire Azure resource class names so the icon matcher receives
	// the correct type tokens via the hidden-original lookup without having to reference the actual package.

	sealed class GenericTestResource(string name) : Resource(name);

	sealed class AzureManagedRedisResource(string name) : Resource(name);

	sealed class AzureRedisCacheResource(string name) : Resource(name);

	sealed class AzureSqlServerResource(string name) : Resource(name);

	sealed class AzurePostgresFlexibleServerResource(string name) : Resource(name);
}
