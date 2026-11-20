using System.Text;
using System.Xml;
using System.Xml.Linq;

// One source, compiled into each of the three discovery paths: the AppHost library (metadata reader),
// the analyzer (netstandard2.0) and the build-time dump tool. They have to agree on what a summary
// reads as, and none of them references another, so the file is linked rather than shared through an
// assembly. Each copy takes its own project's namespace, so a project that sees two of them through
// InternalsVisibleTo never has two types of the same full name.
#if ASPIREC4_ANALYZERS
namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;

#elif ASPIREC4_MODELDUMP
namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ModelDump;

#else
namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

#endif

/// <summary>
/// Turns XML documentation into the single line of plain text an ER diagram shows as a description.
/// </summary>
/// <remarks>
/// Only <c>&lt;summary&gt;</c> is read — and, for a positional record property, the record's own
/// <c>&lt;param&gt;</c> — because a diagram cell has room for one sentence, not for remarks. References
/// keep their short name (<c>&lt;see cref="T:Shop.Order"/&gt;</c> reads <c>Order</c>), and whitespace is
/// collapsed, so a summary wrapped over several comment lines reads as one.
/// </remarks>
static class XmlDocumentationText
{
	/// <summary>
	/// The plain-text summary from a member's documentation: a whole <c>&lt;member&gt;</c> element, or the
	/// bare content of a comment (<c>&lt;summary&gt;…&lt;/summary&gt;</c>).
	/// </summary>
	/// <returns><see langword="null"/> when there is no summary, or it is empty or malformed.</returns>
	public static string? Summary(string? xml) => Element(xml, "summary", null);

	/// <summary>The plain text of the <c>&lt;param name="…"&gt;</c> element in a member's documentation.</summary>
	public static string? Param(string? xml, string name) => Element(xml, "param", name);

	/// <summary>
	/// The documentation XML held by a run of <c>///</c> comment lines, with the comment markers removed.
	/// </summary>
	/// <remarks>
	/// For a compilation that does not parse documentation comments, which Roslyn reports as ordinary
	/// comments: the text is still there, it is just not structured.
	/// </remarks>
	public static string? FromCommentLines(IEnumerable<string> lines)
	{
		StringBuilder sb = new();

		foreach (var raw in lines)
		{
			var line = raw.TrimStart();

			if (!line.StartsWith("///", StringComparison.Ordinal))
			{
				continue;
			}

			sb.AppendLine(line.Substring(3));
		}

		return sb.Length == 0 ? null : sb.ToString();
	}

	/// <summary>The plain text of one documentation element, references and markup resolved.</summary>
	public static string? Text(XElement element)
	{
		StringBuilder sb = new();

		AppendText(sb, element);

		return Normalise(sb.ToString());
	}

	static string? Element(string? xml, string name, string? parameter)
	{
		if (xml is null || string.IsNullOrWhiteSpace(xml))
		{
			return null;
		}

		XElement root;

		try
		{
			// A comment's bare content has no single root, so it is wrapped in one.
			var trimmed = xml.TrimStart();

			root = XElement.Parse(
				trimmed.StartsWith("<member", StringComparison.Ordinal) ? trimmed : $"<doc>{xml}</doc>",
				LoadOptions.PreserveWhitespace
			);
		}
		catch (XmlException)
		{
			return null;
		}

		foreach (var element in root.Elements(name))
		{
			if (
				parameter is null
				|| string.Equals((string?)element.Attribute("name"), parameter, StringComparison.Ordinal)
			)
			{
				return Text(element);
			}
		}

		return null;
	}

	static void AppendText(StringBuilder sb, XElement element)
	{
		foreach (var node in element.Nodes())
		{
			switch (node)
			{
				case XText text:
					sb.Append(text.Value);
					break;

				case XElement child:
					AppendElement(sb, child);
					break;

				default:
					break;
			}
		}
	}

	static void AppendElement(StringBuilder sb, XElement element)
	{
		switch (element.Name.LocalName)
		{
			// A reference reads as the name it points at, without the member-kind prefix or namespace.
			case "see":
			case "seealso":
				if (element.Attribute("langword") is { } langword)
				{
					sb.Append(langword.Value);
				}
				else if (!element.IsEmpty)
				{
					AppendText(sb, element);
				}
				else if ((element.Attribute("cref") ?? element.Attribute("href")) is { } target)
				{
					sb.Append(ShortName(target.Value));
				}

				break;

			case "paramref":
			case "typeparamref":
				sb.Append((string?)element.Attribute("name"));
				break;

			// Structure the diagram cannot show becomes a break between words.
			case "para":
			case "br":
			case "list":
			case "item":
				sb.Append(' ');
				AppendText(sb, element);
				sb.Append(' ');
				break;

			default:
				AppendText(sb, element);
				break;
		}
	}

	/// <summary><c>T:Shop.Orders.Order</c> reads <c>Order</c>; <c>M:Shop.Order.Pay(System.Int32)</c> reads <c>Pay</c>.</summary>
	static string ShortName(string reference)
	{
		var name = reference;
		var colon = Find(name, ':', fromEnd: false);

		if (colon is >= 0 and < 2)
		{
			name = name.Substring(colon + 1);
		}

		var parenthesis = Find(name, '(', fromEnd: false);

		if (parenthesis >= 0)
		{
			name = name.Substring(0, parenthesis);
		}

		var dot = Find(name, '.', fromEnd: true);

		return dot >= 0 && dot < name.Length - 1 ? name.Substring(dot + 1) : name;
	}

	/// <summary>
	/// The first or last index of a character, or -1. Hand-rolled because the overload each target's
	/// analyzers insist on does not exist on the other: <c>netstandard2.0</c> has no comparison overload
	/// for a <see langword="char"/>, and .NET rejects the single-character <see langword="string"/> form.
	/// </summary>
	static int Find(string value, char c, bool fromEnd)
	{
		if (fromEnd)
		{
			for (var i = value.Length - 1; i >= 0; i--)
			{
				if (value[i] == c)
				{
					return i;
				}
			}

			return -1;
		}

		for (var i = 0; i < value.Length; i++)
		{
			if (value[i] == c)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>Collapses every run of whitespace to one space, so a wrapped comment reads as one line.</summary>
	static string? Normalise(string text)
	{
		StringBuilder sb = new(text.Length);
		var space = false;

		foreach (var c in text)
		{
			if (char.IsWhiteSpace(c))
			{
				space = sb.Length > 0;

				continue;
			}

			if (space)
			{
				sb.Append(' ');
				space = false;
			}

			sb.Append(c);
		}

		return sb.Length == 0 ? null : sb.ToString();
	}

#if !ASPIREC4_ANALYZERS
	/// <summary>
	/// The documentation file beside each assembly, keyed by path, so a regeneration does not parse the
	/// same file again. A rebuilt file has a new timestamp and is read afresh.
	/// </summary>
	static readonly System.Collections.Concurrent.ConcurrentDictionary<
		string,
		(DateTime Written, Dictionary<string, XElement> Members)
	> Files = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The summary of a type, read from the documentation file beside its assembly.</summary>
	public static string? TypeSummary(Type type)
	{
		var members = MembersFor(type);

		return members is not null && members.TryGetValue("T:" + DocumentationId(type), out var member)
			? Summary(member.ToString(SaveOptions.DisableFormatting))
			: null;
	}

	/// <summary>
	/// The summary of a property, falling back to the declaring record's <c>&lt;param&gt;</c> for a
	/// positional record property, whose documentation is written on the record rather than on it.
	/// </summary>
	public static string? PropertySummary(Type declaringType, string propertyName)
	{
		var members = MembersFor(declaringType);

		if (members is null)
		{
			return null;
		}

		var id = DocumentationId(declaringType);

		if (
			members.TryGetValue($"P:{id}.{propertyName}", out var property)
			&& Summary(property.ToString(SaveOptions.DisableFormatting)) is { } summary
		)
		{
			return summary;
		}

		return members.TryGetValue("T:" + id, out var type)
			? Param(type.ToString(SaveOptions.DisableFormatting), propertyName)
			: null;
	}

	/// <summary>A type's documentation id: its full name, with nested types joined by a dot.</summary>
	static string DocumentationId(Type type)
	{
		var definition = type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;

		return (definition.FullName ?? definition.Name).Replace('+', '.');
	}

	static Dictionary<string, XElement>? MembersFor(Type type)
	{
		string location;

		try
		{
			location = type.Assembly.Location;
		}
		catch (NotSupportedException)
		{
			return null;
		}

		if (string.IsNullOrEmpty(location))
		{
			return null;
		}

		var path = Path.ChangeExtension(location, ".xml");

		if (!File.Exists(path))
		{
			return null;
		}

		DateTime written;

		try
		{
			written = File.GetLastWriteTimeUtc(path);
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}

		if (Files.TryGetValue(path, out var cached) && cached.Written == written)
		{
			return cached.Members;
		}

		var members = Load(path);

		Files[path] = (written, members);

		return members;
	}

	/// <summary>Reads every <c>&lt;member&gt;</c> from a compiler-generated documentation file.</summary>
	static Dictionary<string, XElement> Load(string path)
	{
		Dictionary<string, XElement> members = new(StringComparer.Ordinal);

		try
		{
			var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);

			foreach (var member in document.Descendants("member"))
			{
				if ((string?)member.Attribute("name") is { Length: > 0 } name)
				{
					members[name] = member;
				}
			}
		}
		catch (XmlException)
		{
			// A malformed or half-written file contributes nothing; it is re-read once it changes.
		}
		catch (IOException)
		{
			// Being rewritten by a concurrent build.
		}
		catch (UnauthorizedAccessException)
		{
			// Unreadable; descriptions from attributes still apply.
		}

		return members;
	}
#endif
}
