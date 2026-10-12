namespace Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// The dashboard row that AspireC4's diagram resources nest under when more than one diagram is
/// registered.
/// </summary>
/// <remarks>
/// It runs no process and serves nothing — it exists purely so the architecture diagram and the ER
/// diagram appear together rather than as unrelated rows. It is not created at all when only one
/// diagram is visible.
/// </remarks>
/// <param name="name">The resource name, which is the name <c>AddAspireC4()</c> was called with.</param>
public sealed class AspireC4DiagramGroupResource(string name) : Resource(name);
