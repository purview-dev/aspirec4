namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ApplicationModel;

/// <summary>
/// The user-facing ER diagram resource. Wraps the d2 sidecar so the dashboard shows a single row.
/// </summary>
/// <param name="name">The resource name.</param>
public sealed class AspireC4EfResource(string name) : Resource(name)
{
	/// <summary>The endpoint name used by the d2 server.</summary>
	public const string HttpEndpointName = "http";

	/// <summary>
	/// The output directory resolved at builder time. Used for the sidecar's bind mount, which has to
	/// be fixed before the container starts.
	/// </summary>
	public required string OutputDirectory { get; init; }

	/// <summary>
	/// The AppHost directory, so a relative <c>OutputDirectory</c> supplied through configuration can
	/// be resolved when the diagram is written rather than when the builder ran.
	/// </summary>
	public required string AppHostDirectory { get; init; }

	/// <summary>
	/// The inner sidecar resource actually running d2, or <see langword="null"/> when the configured
	/// renderer needs no server. Settable so <c>WithLocalD2CLI()</c> can swap the container for a local
	/// executable.
	/// </summary>
	public required IResource? InnerResource { get; set; }
}
