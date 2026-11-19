using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4.Annotations;

/// <summary>
/// Annotation that customises how a resource appears as a node in the generated LikeC4 diagram.
/// Configure using <c>WithLikeC4Details(a =&gt; a.WithIcon(...).WithTag(...))</c>.
/// </summary>
[AspireExport(ExposeProperties = true, ExposeMethods = true)]
public sealed class LikeC4NodeDetailsAnnotation : IResourceAnnotation
{
	readonly List<string> _tags = [];
	readonly List<LikeC4Link> _links = [];
	readonly List<LikeC4Metadata> _metadata = [];

	/// <summary>
	/// Initialises a new <see cref="LikeC4NodeDetailsAnnotation"/> with the given display label.
	/// </summary>
	/// <param name="label">The display label for this element. Must not be null or whitespace.</param>
	public LikeC4NodeDetailsAnnotation(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
	}

	/// <summary>The display label shown on the element node in the diagram.</summary>
	public string Label { get; private set; }

	/// <summary>Optional technology string (e.g. <c>".NET"</c>, <c>"Redis"</c>) displayed beneath the label.</summary>
	public string? Technology { get; private set; }

	/// <summary>Optional longer description of this element, rendered in the diagram's detail panel.</summary>
	public string? Description { get; private set; }

	/// <summary>Optional one-line summary shown in tooltips or the diagram map view.</summary>
	public string? Summary { get; private set; }

	/// <summary>
	/// Optional icon identifier (e.g. <c>"tech:dotnet"</c>, <c>"azure:storage"</c>).
	/// When <see langword="null"/>, the icon is inferred automatically if auto-icons are enabled.
	/// </summary>
	public string? Icon { get; private set; }

	/// <summary>
	/// Per-element override for automatic icon inference.
	/// <see langword="null"/> inherits the project-level <see cref="AspireC4DiagramOptions.AutoIconsEnabled"/> setting.
	/// </summary>
	public bool? AutoIconEnabled { get; private set; }

	/// <summary>Optional element kind override. When set, overrides the inferred element kind in the diagram.</summary>
	public string? Kind { get; private set; }

	/// <summary>Tags applied to this element in the diagram.</summary>
	public IReadOnlyList<string> Tags => _tags;

	/// <summary>Links attached to this element in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links => _links;

	/// <summary>Metadata key-value pairs for this element.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata => _metadata;

	/// <summary>Sets the display label for this element.</summary>
	/// <param name="label">The new label. Must not be null or whitespace.</param>
	/// <returns>The same <see cref="LikeC4NodeDetailsAnnotation"/> for further configuration.</returns>
	public LikeC4NodeDetailsAnnotation WithLabel(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
		return this;
	}

	/// <summary>Sets the technology string displayed beneath the element label.</summary>
	/// <param name="technology">The technology name, or <see langword="null"/> to clear it.</param>
	/// <returns>The same <see cref="LikeC4NodeDetailsAnnotation"/> for further configuration.</returns>
	public LikeC4NodeDetailsAnnotation WithTechnology(string? technology)
	{
		Technology = technology;
		return this;
	}

	/// <summary>Sets the description rendered in the diagram's detail panel.</summary>
	/// <param name="description">The description text, or <see langword="null"/> to clear it.</param>
	/// <returns>The same <see cref="LikeC4NodeDetailsAnnotation"/> for further configuration.</returns>
	public LikeC4NodeDetailsAnnotation WithDescription(string? description)
	{
		Description = description;
		return this;
	}

	/// <summary>Sets the one-line summary shown in tooltips or the diagram map view.</summary>
	/// <param name="summary">The summary text, or <see langword="null"/> to clear it.</param>
	/// <returns>The same <see cref="LikeC4NodeDetailsAnnotation"/> for further configuration.</returns>
	public LikeC4NodeDetailsAnnotation WithSummary(string? summary)
	{
		Summary = summary;
		return this;
	}

	/// <summary>
	/// Sets the icon for this element.
	/// Use a namespaced identifier such as <c>"tech:dotnet"</c> or <c>"azure:storage"</c>.
	/// Pass <see langword="null"/> to revert to automatic inference.
	/// </summary>
	/// <param name="icon">The icon identifier, or <see langword="null"/> to clear it.</param>
	/// <returns>The same <see cref="LikeC4NodeDetailsAnnotation"/> for further configuration.</returns>
	public LikeC4NodeDetailsAnnotation WithIcon(string? icon)
	{
		if (icon is not null)
			ArgumentException.ThrowIfNullOrWhiteSpace(icon);
		Icon = icon;
		return this;
	}

	/// <summary>
	/// Controls automatic icon inference for this element.
	/// <see langword="null"/> inherits the project-level <see cref="AspireC4DiagramOptions.AutoIconsEnabled"/> setting.
	/// </summary>
	public LikeC4NodeDetailsAnnotation WithAutoIcon(bool? enabled = null)
	{
		AutoIconEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets an element kind override, replacing the auto-inferred kind for this resource.
	/// Must be a valid LikeC4 identifier (letters, digits, hyphens, underscores; cannot start with a digit).
	/// </summary>
	public LikeC4NodeDetailsAnnotation WithKind(string? kind)
	{
		Kind = kind;
		return this;
	}

	/// <summary>
	/// Adds a tag. A leading <c>#</c> is accepted and stripped automatically, so <c>"#external"</c>
	/// and <c>"external"</c> refer to the same tag.
	/// </summary>
	public LikeC4NodeDetailsAnnotation WithTag(string tag)
	{
		_tags.Add(Helpers.NormaliseTag(tag));
		return this;
	}

	/// <summary>Adds a hyperlink to this element.</summary>
	/// <param name="url">The URL, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	[AspireExport(id: "withLinkNode", MethodName = "withLink")]
	public LikeC4NodeDetailsAnnotation WithLink(string url, string? title = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		_links.Add(new LikeC4Link(url, title));
		return this;
	}

	/// <inheritdoc cref="WithLink(string, string?)"/>
	[AspireExport(id: "withLinkUriNode", MethodName = "withLinkUri")]
	public LikeC4NodeDetailsAnnotation WithLink(Uri uri, string? title = null) => WithLink(uri?.ToString()!, title);

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	[AspireExport(id: "withMetadataNode", MethodName = "withMetadata")]
	public LikeC4NodeDetailsAnnotation WithMetadata(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		_metadata.Add(new(key, value));

		return this;
	}

	/// <summary>Adds multiple metadata key-value pairs to this element.</summary>
	[AspireExportIgnore(Reason = "Not supported in the exported API.")]
	public LikeC4NodeDetailsAnnotation WithMetadata(params (string key, string value)[] metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}
}
