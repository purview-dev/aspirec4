namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>Where a table's or column's description may come from.</summary>
/// <remarks>
/// When several are present the first in this order wins: an attribute, then XML documentation, then
/// the database comment. An attribute is written for display, so it is preferred; a comment is written
/// for the database, so it is the fallback.
/// </remarks>
[Flags]
public enum ErdDescriptionSources
{
	/// <summary>No source; nothing is described.</summary>
	None = 0,

	/// <summary>
	/// <c>[Description]</c>, <c>[Display(Description = …)]</c> or <c>[DisplayName]</c> on the entity type
	/// or property.
	/// </summary>
	Attributes = 1,

	/// <summary>
	/// The XML documentation <c>&lt;summary&gt;</c>. The analyzer reads it from source; the metadata
	/// reader and the build-time dump need the project to set <c>GenerateDocumentationFile</c>.
	/// </summary>
	XmlDocumentation = 2,

	/// <summary>The database comment, from <c>[Comment]</c> or <c>HasComment</c>.</summary>
	Comments = 4,

	/// <summary>Every source. The default.</summary>
	All = Attributes | XmlDocumentation | Comments,
}
