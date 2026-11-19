using System.ComponentModel;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IResourceBuilder{T}"/> (where T : <see cref="IResourceWithEnvironment"/>) to
/// add annotations that customize how resources and their relationships
/// are represented in the generated LikeC4 diagrams. These methods allow you to specify details such as labels, technologies, descriptions,
/// summaries, and icons for resources, as well as details for relationships between resources.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4ResourceBuilderEnvExtensions
{
	/// <summary>
	/// Adds a reference to another resource with a connection string, and configures it to be a LikeC4 relationship,
	/// using a fluent callback to customise the relationship.
	/// </summary>
	/// <param name="builder">The resource builder for the source resource.</param>
	/// <param name="source">The target resource builder with a connection string.</param>
	/// <param name="configure">An action that configures the relationship appearance.</param>
	/// <param name="connectionName">The connection name passed to Aspire's <c>WithReference</c>.</param>
	/// <param name="optional">Whether the reference is optional.</param>
	/// <param name="skipAspireReference">When <see langword="true"/>, skips calling Aspire's <c>WithReference</c>.</param>
	/// <remarks>
	/// <para>
	/// The same as <see cref="WithLikeC4ConnectionReference{T}"/>, which is the name every AppHost language
	/// shares: a TypeScript, Go or Python binding has no overloads, so this overload cannot be exported
	/// under <c>WithLikeC4Reference</c> beside the one that takes any resource.
	/// </para>
	/// <para>
	/// Prefer <see cref="WithLikeC4ConnectionReference{T}"/>. C# only picks this overload when one of its
	/// connection arguments (<paramref name="connectionName"/>, <paramref name="optional"/>,
	/// <paramref name="skipAspireReference"/>) is passed: a call with just a target and a callback binds to
	/// the relationship-only <see cref="AspireC4ResourceBuilderExtensions.WithLikeC4Reference{T, TRef}"/>,
	/// which needs no omitted optional parameters, and so never calls Aspire's <c>WithReference</c>.
	/// </para>
	/// </remarks>
	[AspireExportIgnore(Reason = "Exported as withLikeC4ConnectionReference.")]
	public static IResourceBuilder<T> WithLikeC4Reference<T>(
		this IResourceBuilder<T> builder,
		IResourceBuilder<IResourceWithConnectionString> source,
		Action<LikeC4RelationshipDetailsAnnotation>? configure = null,
		string? connectionName = null,
		bool optional = false,
		bool skipAspireReference = false
	)
		where T : IResourceWithEnvironment =>
		builder.WithLikeC4ConnectionReference(source, configure, connectionName, optional, skipAspireReference);

	/// <summary>
	/// Adds a reference to a resource with a connection string, through Aspire's <c>WithReference</c>, and
	/// draws it as a LikeC4 relationship, using a fluent callback to customise the relationship.
	/// </summary>
	/// <remarks>
	/// The name is the same in every AppHost language (<c>withLikeC4ConnectionReference</c>,
	/// <c>WithLikeC4ConnectionReference</c>, <c>with_like_c4_connection_reference</c>). Use
	/// <see cref="AspireC4ResourceBuilderExtensions.WithLikeC4Reference{T, TRef}"/> for a relationship
	/// without a connection string.
	/// </remarks>
	/// <param name="builder">The resource builder for the source resource.</param>
	/// <param name="source">The target resource builder with a connection string.</param>
	/// <param name="configure">An action that configures the relationship appearance.</param>
	/// <param name="connectionName">The connection name passed to Aspire's <c>WithReference</c>.</param>
	/// <param name="optional">Whether the reference is optional.</param>
	/// <param name="skipAspireReference">When <see langword="true"/>, skips calling Aspire's <c>WithReference</c>.</param>
	[AspireExport(RunSyncOnBackgroundThread = true)]
	public static IResourceBuilder<T> WithLikeC4ConnectionReference<T>(
		this IResourceBuilder<T> builder,
		IResourceBuilder<IResourceWithConnectionString> source,
		Action<LikeC4RelationshipDetailsAnnotation>? configure = null,
		string? connectionName = null,
		bool optional = false,
		bool skipAspireReference = false
	)
		where T : IResourceWithEnvironment
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(source);

		if (!skipAspireReference)
			builder.WithReference(source, connectionName, optional);

		LikeC4RelationshipDetailsAnnotation annotation = new(source.Resource.Name);
		configure?.Invoke(annotation);
		builder.Resource.Annotations.Add(annotation);

		return builder;
	}
}
