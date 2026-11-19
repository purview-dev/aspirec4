namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Recognises a .NET project resource on either supported Aspire line, in one place.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IDotnetProgramResource"/> is implemented by the legacy <see cref="ProjectResource"/>
/// (<c>AddProject</c>) and by Aspire 17's <c>DotnetProjectResource</c> (<c>AddDotnetProject</c>), which
/// derives from <see cref="ExecutableResource"/> instead. Matching the interface draws both as projects.
/// </para>
/// <para>
/// The libraries compile against the Aspire 13.6.5 floor, where the interface exists but is marked
/// experimental (<c>ASPIREPROJECTS001</c>); Aspire 17 made it stable. Only a type test is made against
/// it, which binds by name and holds on both lines, so the diagnostic is suppressed here and nowhere else.
/// </para>
/// </remarks>
static class DotnetProjects
{
#pragma warning disable ASPIREPROJECTS001 // Experimental on the 13 line, stable on 17; see the remarks.
	public static bool IsDotnetProject(IResource resource) => resource is IDotnetProgramResource;
#pragma warning restore ASPIREPROJECTS001

	public static IEnumerable<IResource> In(DistributedApplicationModel appModel) =>
		appModel.Resources.Where(IsDotnetProject);
}
