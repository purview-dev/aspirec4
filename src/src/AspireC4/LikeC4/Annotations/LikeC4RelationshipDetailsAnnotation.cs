using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4.Annotations;

/// <summary>
/// Annotation that customises how a relationship appears as an edge in the generated LikeC4 diagram.
/// Keyed by <see cref="TargetName"/>; attach multiple instances to a source resource — one per
/// target — to override different relationships independently.
/// Configure using <c>WithLikeC4Reference(target, a =&gt; a.WithKind("async").WithLabel("calls"))</c>.
/// </summary>
[AspireExport(ExposeProperties = true, ExposeMethods = true)]
public sealed class LikeC4RelationshipDetailsAnnotation : IResourceAnnotation
{
	readonly List<string> _tags = [];
	readonly List<LikeC4Link> _links = [];
	readonly List<LikeC4Metadata> _metadata = [];

	/// <param name="targetName">
	/// The <see cref="IResource.Name"/> of the relationship target. Used to match this annotation to the
	/// correct <see cref="ResourceRelationshipAnnotation"/> when building the diagram model.
	/// </param>
	public LikeC4RelationshipDetailsAnnotation(string targetName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
		TargetName = targetName;
	}

	/// <summary>The <see cref="IResource.Name"/> of the relationship target resource.</summary>
	public string TargetName { get; }

	/// <summary>Short label shown on the relationship arrow.</summary>
	public string? Label { get; private set; }

	/// <summary>Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public string? Technology { get; private set; }

	/// <summary>Longer description of the relationship.</summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Optional LikeC4 relationship kind identifier (e.g. "async", "sync"). When set, the kind is
	/// declared in the <c>specification</c> block and the typed <c>-[kind]-&gt;</c> syntax is used.
	/// </summary>
	public string? Kind { get; private set; }

	/// <summary>Optional ID of a LikeC4 view to navigate to when the relationship is clicked.</summary>
	public string? NavigateTo { get; private set; }

	/// <summary>Tags applied to this relationship in the diagram.</summary>
	public IReadOnlyList<string> Tags => _tags;

	/// <summary>Links attached to this relationship in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links => _links;

	/// <summary>Metadata key-value pairs for this relationship.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata => _metadata;

	/// <summary>Sets the short label shown on the relationship arrow.</summary>
	public LikeC4RelationshipDetailsAnnotation WithLabel(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
		return this;
	}

	/// <summary>Sets the technology or protocol (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public LikeC4RelationshipDetailsAnnotation WithTechnology(string? technology)
	{
		Technology = technology;
		return this;
	}

	/// <summary>Sets the longer description of the relationship.</summary>
	public LikeC4RelationshipDetailsAnnotation WithDescription(string? description)
	{
		Description = description;
		return this;
	}

	/// <summary>
	/// Sets the LikeC4 relationship kind (e.g. "async", "sync"). The kind is declared in the
	/// <c>specification</c> block and the typed <c>-[kind]-&gt;</c> syntax is used in the model.
	/// </summary>
	public LikeC4RelationshipDetailsAnnotation WithKind(string? kind)
	{
		Kind = kind;
		return this;
	}

	/// <summary>
	/// Adds a tag. A leading <c>#</c> is accepted and stripped automatically, so <c>"#external"</c>
	/// and <c>"external"</c> refer to the same tag.
	/// </summary>
	public LikeC4RelationshipDetailsAnnotation WithTag(string tag)
	{
		_tags.Add(Helpers.NormaliseTag(tag));
		return this;
	}

	/// <summary>Adds a hyperlink to this relationship.</summary>
	/// <param name="url">The URL, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	[AspireExport(id: "withLinkRelationship", MethodName = "withLink")]
	public LikeC4RelationshipDetailsAnnotation WithLink(string url, string? title = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		_links.Add(new LikeC4Link(url, title));
		return this;
	}

	/// <inheritdoc cref="WithLink(string, string?)"/>
	[AspireExport(id: "withLinkUriRelationship", MethodName = "withLinkUri")]
	public LikeC4RelationshipDetailsAnnotation WithLink(Uri uri, string? title = null) =>
		WithLink(uri?.ToString()!, title);

	/// <summary>Adds a metadata key-value pair to this relationship.</summary>
	[AspireExport(id: "withMetadataRelationship", MethodName = "withMetadata")]
	public LikeC4RelationshipDetailsAnnotation WithMetadata(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		_metadata.Add(new LikeC4Metadata(key, value));

		return this;
	}

	/// <summary>Adds multiple metadata key-value pairs to this relationship.</summary>
	[AspireExportIgnore(Reason = "Not supported in the exported API.")]
	public LikeC4RelationshipDetailsAnnotation WithMetadata(params (string key, string value)[] metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}

	/// <summary>Sets the ID of a LikeC4 dynamic view to navigate to when the relationship is clicked.</summary>
	/// <param name="viewId">The identifier of the target dynamic view.</param>
	public LikeC4RelationshipDetailsAnnotation WithNavigateTo(string viewId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		NavigateTo = viewId;
		return this;
	}
}
