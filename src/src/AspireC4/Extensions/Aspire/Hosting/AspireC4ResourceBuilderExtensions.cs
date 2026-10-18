using System.ComponentModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IResourceBuilder{T}"/> to add annotations that customize how resources and their relationships
/// are represented in the generated LikeC4 diagrams. These methods allow you to specify details such as labels, technologies, descriptions,
/// summaries, and icons for resources, as well as details for relationships between resources.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4ResourceBuilderExtensions
{
	/// <summary>
	/// Customises how a resource appears in the generated LikeC4 diagram using fluent options.
	/// </summary>
	/// <param name="builder">The resource builder for the resource being customised.</param>
	/// <param name="configure">Optional action that configures the LikeC4 node details annotation using fluent methods.</param>
	[AspireExport(RunSyncOnBackgroundThread = true)]
	public static IResourceBuilder<T> WithLikeC4Details<T>(
		this IResourceBuilder<T> builder,
		Action<LikeC4NodeDetailsAnnotation>? configure = null
	)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		LikeC4NodeDetailsAnnotation annotation = new(builder.Resource.Name);
		configure?.Invoke(annotation);

		return builder.WithAnnotation(annotation, ResourceAnnotationMutationBehavior.Replace);
	}

	/// <summary>
	/// Customises how the relationship from this resource to <paramref name="target"/> appears in the
	/// generated LikeC4 diagram using fluent configuration.
	/// </summary>
	/// <param name="builder">The resource builder for the source resource.</param>
	/// <param name="target">The target resource builder that the relationship points to.</param>
	/// <param name="configure">Optional action that configures the relationship appearance.</param>
	[AspireExport(RunSyncOnBackgroundThread = true)]
	public static IResourceBuilder<T> WithLikeC4Reference<T, TRef>(
		this IResourceBuilder<T> builder,
		IResourceBuilder<TRef> target,
		Action<LikeC4RelationshipDetailsAnnotation>? configure = null
	)
		where T : IResource
		where TRef : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(target);

		LikeC4RelationshipDetailsAnnotation annotation = new(target.Resource.Name);
		configure?.Invoke(annotation);

		builder.Resource.Annotations.Add(annotation);

		return builder;
	}

	/// <summary>
	/// Maps this resource onto an element that already exists in a hand-authored LikeC4 project.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Instead of declaring a new element, the generated model emits <c>extend &lt;elementId&gt;</c> for
	/// this resource, applying its Aspire runtime state tag, endpoint links and metadata to the element
	/// the project already describes. Tags, links and metadata from <see cref="WithLikeC4Details"/> are
	/// applied too, as are relationships from <see cref="WithLikeC4Reference"/>, and any Aspire children
	/// of this resource are nested inside the extended element.
	/// </para>
	/// <para>
	/// <b>The title, technology, summary, description and icon are not applied.</b> LikeC4's
	/// <c>extend</c> body accepts tags, links, metadata and nested declarations only — the others are
	/// parse errors there — so the project that declares the element remains their only source, and
	/// anything set for them through <see cref="WithLikeC4Details"/> is ignored for this resource.
	/// </para>
	/// <para>
	/// Pair with <see cref="AspireC4DiagramOptionsExtensions.WithLikeC4ProjectFolder"/> so the project
	/// declaring <paramref name="elementId"/> is the one being served. AspireC4 does not read the
	/// project's <c>.c4</c> sources, so an identifier that does not exist there is reported by LikeC4
	/// rather than at build time.
	/// </para>
	/// </remarks>
	/// <param name="builder">The resource builder for the resource being mapped.</param>
	/// <param name="elementId">
	/// The element's fully-qualified identifier, with dots separating the hierarchy
	/// (e.g. <c>cloud.backend.api</c>).
	/// </param>
	[AspireExport]
	public static IResourceBuilder<T> WithLikeC4Element<T>(this IResourceBuilder<T> builder, string elementId)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithAnnotation(
			new LikeC4ExistingElementAnnotation(elementId),
			ResourceAnnotationMutationBehavior.Replace
		);
	}

	/// <summary>
	/// Assigns this resource to a named group in the generated LikeC4 diagram.
	/// Resources sharing the same <paramref name="groupName"/> are emitted inside a
	/// <c>group 'label' { include ... }</c> block in the generated view.
	/// </summary>
	/// <param name="builder">The resource builder for the resource being assigned to a group.</param>
	/// <param name="groupName">The name of the group to assign this resource to.</param>
	[AspireExport]
	public static IResourceBuilder<T> WithLikeC4Group<T>(this IResourceBuilder<T> builder, string groupName)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

		return builder.WithAnnotation(new LikeC4GroupAnnotation(groupName), ResourceAnnotationMutationBehavior.Replace);
	}

	/// <summary>
	/// Excludes a resource from the generated LikeC4 diagram.
	/// </summary>
	/// <param name="builder">The resource builder for the resource being excluded.</param>
	[AspireExport]
	public static IResourceBuilder<T> ExcludeFromLikeC4<T>(this IResourceBuilder<T> builder)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);
	}
}
