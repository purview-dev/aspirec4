namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>Represents a single element (node) in the LikeC4 architecture model.</summary>
public sealed record LikeC4Element
{
	/// <summary>The resource name — used as the identifier in the DSL.</summary>
	public required string Name { get; init; }

	/// <summary>Human-readable display label. Defaults to <see cref="Name"/> when not specified.</summary>
	public required string Label { get; init; }

	/// <summary>LikeC4 element kind, e.g. "component", "container". See <see cref="LikeC4ElementKind"/>.</summary>
	public required string Kind { get; init; }

	/// <summary>Optional technology description shown on the element node.</summary>
	public string? Technology { get; init; }

	/// <summary>Optional longer description shown in element details.</summary>
	public string? Description { get; init; }

	/// <summary>Optional summary shown in element details.</summary>
	public string? Summary { get; init; }

	/// <summary>Optional LikeC4 icon token or image reference shown on the element.</summary>
	public string? Icon { get; init; }

	/// <summary>Name of the parent resource when this element is nested (via <see cref="IResourceWithParent"/>).</summary>
	public string? ParentName { get; init; }

	/// <summary>Current Aspire runtime state of the resource (from <see cref="KnownResourceStates"/>). Controls the diagram colour via an auto-derived <c>aspire-run-state-*</c> tag. <see langword="null"/> means no state information is available.</summary>
	public string? State { get; init; }

	/// <summary>Tags applied to this element (e.g. <c>"external"</c>, <c>"deprecated"</c>). Declared in the specification block as <c>tag NAME</c>.</summary>
	public IReadOnlyList<string> Tags { get; init; } = [];

	/// <summary>Links attached to this element — emitted as <c>link URL ['title']</c> in the element body.</summary>
	public IReadOnlyList<LikeC4Link> Links { get; init; } = [];

	/// <summary>Metadata key-value pairs emitted as a <c>metadata { key "value" }</c> block in the element body.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata { get; init; } = [];

	/// <summary>Optional view-level group label. Elements sharing the same label are emitted inside a <c>group 'label' { include ... }</c> block in the generated view.</summary>
	public string? Group { get; init; }

	/// <summary>
	/// Whether <see cref="Name"/> refers to an element declared in a hand-authored LikeC4 project
	/// rather than one this model declares.
	/// </summary>
	/// <remarks>
	/// Set by <c>WithLikeC4Element</c>. Such elements are emitted as <c>extend &lt;id&gt; { … }</c> and
	/// their kind is not declared in the specification block, because the project already declares it.
	/// <para>
	/// LikeC4's <c>extend</c> body accepts tags, links, metadata and nested declarations only, so
	/// <see cref="Technology"/>, <see cref="Description"/>, <see cref="Summary"/> and <see cref="Icon"/>
	/// are always <see langword="null"/> here and <see cref="Label"/> is never emitted — the project
	/// those come from remains their only source.
	/// </para>
	/// </remarks>
	public bool ExtendsExistingElement { get; init; }
}
