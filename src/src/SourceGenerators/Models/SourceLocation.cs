using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

/// <summary>
/// A value-equatable, compilation-independent source position used to report diagnostics at the
/// source-output stage. Roslyn <see cref="Location"/> instances are never safe to retain in
/// incremental pipeline models, so the span information is extracted here and rebuilt on demand.
/// </summary>
readonly struct SourceLocation(string filePath, TextSpan sourceSpan, LinePositionSpan lineSpan)
	: IEquatable<SourceLocation>
{
	public static readonly SourceLocation None = new(string.Empty, default, default);

	public string FilePath { get; } = filePath;

	public TextSpan SourceSpan { get; } = sourceSpan;

	public LinePositionSpan LineSpan { get; } = lineSpan;

	public bool IsEmpty => string.IsNullOrEmpty(FilePath);

	public static SourceLocation FromLocation(Location? location)
	{
		if (location is null || !location.IsInSource)
			return None;

		var lineSpan = location.GetLineSpan();
		return new(lineSpan.Path, location.SourceSpan, lineSpan.Span);
	}

	public Location ToLocation() => IsEmpty ? Location.None : Location.Create(FilePath, SourceSpan, LineSpan);

	public bool Equals(SourceLocation other) =>
		FilePath == other.FilePath && SourceSpan == other.SourceSpan && LineSpan == other.LineSpan;

	public override bool Equals(object? obj) => obj is SourceLocation other && Equals(other);

	public override int GetHashCode()
	{
		unchecked
		{
			var h = FilePath?.GetHashCode() ?? 0;
			h = (h * 397) ^ SourceSpan.GetHashCode();
			h = (h * 397) ^ LineSpan.GetHashCode();
			return h;
		}
	}
}
