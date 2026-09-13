namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

/// <summary>
/// A resolved constant string value from a call-site argument, with its source location.
/// </summary>
readonly struct CallSiteInfo(string value, SourceLocation location) : IEquatable<CallSiteInfo>
{
	public string Value { get; } = value;

	/// <summary>
	/// The location of the argument expression, for diagnostic reporting.
	/// </summary>
	public SourceLocation Location { get; } = location;

	public bool Equals(CallSiteInfo other) => Value == other.Value && Location.Equals(other.Location);

	public override bool Equals(object? obj) => obj is CallSiteInfo c && Equals(c);

	public override int GetHashCode()
	{
		unchecked
		{
			var h = Value?.GetHashCode() ?? 0;
			h = (h * 397) ^ Location.GetHashCode();
			return h;
		}
	}
}
