using System.Globalization;
using System.Text;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;

/// <summary>
/// A minimal JSON writer. The generator targets <c>netstandard2.0</c> and deliberately takes no
/// serialisation dependency, so the descriptor is written by hand.
/// </summary>
sealed class JsonWriter
{
	readonly StringBuilder _sb = new();
	bool _needsComma;

	/// <summary>Writes the opening brace of an object.</summary>
	public JsonWriter StartObject()
	{
		Separate();
		_sb.Append('{');
		_needsComma = false;

		return this;
	}

	/// <summary>Writes the closing brace of an object.</summary>
	public JsonWriter EndObject()
	{
		_sb.Append('}');
		_needsComma = true;

		return this;
	}

	/// <summary>Writes a named array and its contents.</summary>
	public JsonWriter Array<T>(string name, IEnumerable<T> values, Action<JsonWriter, T> write)
	{
		Separate();
		WriteName(name);
		_sb.Append('[');
		_needsComma = false;

		foreach (var value in values)
		{
			write(this, value);
		}

		_sb.Append(']');
		_needsComma = true;

		return this;
	}

	/// <summary>Writes a string property, skipping it entirely when the value is <see langword="null"/>.</summary>
	public JsonWriter Text(string name, string? value)
	{
		if (value is null)
		{
			return this;
		}

		Separate();
		WriteName(name);
		WriteEscaped(value);
		_needsComma = true;

		return this;
	}

	/// <summary>Writes a boolean property, skipping it when it carries the default value.</summary>
	public JsonWriter Bool(string name, bool value, bool skipWhenFalse = true)
	{
		if (!value && skipWhenFalse)
		{
			return this;
		}

		Separate();
		WriteName(name);
		_sb.Append(value ? "true" : "false");
		_needsComma = true;

		return this;
	}

	/// <summary>Writes an integer property, skipping it when the value is <see langword="null"/>.</summary>
	public JsonWriter Number(string name, int? value)
	{
		if (value is null)
		{
			return this;
		}

		Separate();
		WriteName(name);
		_sb.Append(value.Value.ToString(CultureInfo.InvariantCulture));
		_needsComma = true;

		return this;
	}

	/// <summary>
	/// Writes an already-serialised JSON fragment verbatim. Used to splice per-context fragments that
	/// were produced independently by the incremental pipeline.
	/// </summary>
	public JsonWriter Raw(string json)
	{
		Separate();
		_sb.Append(json);
		_needsComma = true;

		return this;
	}

	/// <summary>Writes a bare string element inside an array.</summary>
	public JsonWriter Value(string value)
	{
		Separate();
		WriteEscaped(value);
		_needsComma = true;

		return this;
	}

	/// <inheritdoc/>
	public override string ToString() => _sb.ToString();

	void Separate()
	{
		if (_needsComma)
		{
			_sb.Append(',');
		}
	}

	void WriteName(string name)
	{
		WriteEscaped(name);
		_sb.Append(':');
	}

	void WriteEscaped(string value)
	{
		_sb.Append('"');

		foreach (var c in value)
		{
			switch (c)
			{
				case '"':
					_sb.Append("\\\"");
					break;
				case '\\':
					_sb.Append("\\\\");
					break;
				case '\n':
					_sb.Append("\\n");
					break;
				case '\r':
					_sb.Append("\\r");
					break;
				case '\t':
					_sb.Append("\\t");
					break;
				default:
					if (c < ' ')
					{
						_sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						_sb.Append(c);
					}

					break;
			}
		}

		_sb.Append('"');
	}
}
