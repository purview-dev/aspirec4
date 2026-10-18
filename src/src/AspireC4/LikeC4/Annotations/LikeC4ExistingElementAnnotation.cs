namespace Aspire.Hosting.AspireC4.LikeC4.Annotations;

/// <summary>
/// Maps a resource onto an element that already exists in a hand-authored LikeC4 project.
/// </summary>
/// <remarks>
/// The generated model emits <c>extend &lt;id&gt; { … }</c> for the resource rather than declaring a
/// new element, so Aspire's runtime state, links and metadata land on the element the project already
/// describes instead of creating a duplicate beside it. Configure with
/// <c>WithLikeC4Element("cloud.backend.api")</c>.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class LikeC4ExistingElementAnnotation : IResourceAnnotation
{
	/// <summary>Initialises the annotation with the fully-qualified LikeC4 element identifier.</summary>
	/// <param name="elementId">
	/// The element's fully-qualified identifier, with dots separating the hierarchy
	/// (e.g. <c>cloud.backend.api</c>).
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="elementId"/> is not a usable LikeC4 identifier.
	/// </exception>
	public LikeC4ExistingElementAnnotation(string elementId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

		if (!IsValidElementId(elementId))
		{
			throw new ArgumentException(
				$"'{elementId}' is not a valid LikeC4 element identifier. Expected dot-separated segments, "
					+ "each starting with a letter or underscore (e.g. 'cloud.backend.api').",
				nameof(elementId)
			);
		}

		ElementId = elementId;
	}

	/// <summary>The fully-qualified identifier of the element in the hand-authored project.</summary>
	public string ElementId { get; }

	/// <summary>
	/// Validates the shape of an identifier without attempting to resolve it.
	/// </summary>
	/// <remarks>
	/// Deliberately permissive: AspireC4 does not read the project's <c>.c4</c> sources, so it cannot
	/// know which identifiers exist. This only rejects text that could never parse as an identifier,
	/// leaving genuinely unresolved references to LikeC4 itself, which reports them properly.
	/// </remarks>
	static bool IsValidElementId(string elementId)
	{
		foreach (var segment in elementId.Split('.'))
		{
			if (segment.Length == 0 || !(char.IsLetter(segment[0]) || segment[0] == '_'))
			{
				return false;
			}

			foreach (var character in segment)
			{
				if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
				{
					return false;
				}
			}
		}

		return true;
	}
}
