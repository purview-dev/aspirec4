using System.Globalization;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Splits a column's type into the headline a renderer shows and the label it keeps alongside.
/// </summary>
/// <remarks>
/// A column has a store type — what the database holds — and optionally a domain type: a value object
/// or an enum. The database type is never lost: it is the headline, the accompanying label, or present
/// in the model for a consumer to surface.
/// </remarks>
static class EfColumnTypeDisplay
{
	/// <summary>The real database type, including any configured length.</summary>
	public static string DatabaseType(EfColumn column)
	{
		ArgumentNullException.ThrowIfNull(column);

		return column.MaxLength is > 0 && !column.StoreType.Contains('(', StringComparison.Ordinal)
			? $"{column.StoreType}({column.MaxLength.Value.ToString(CultureInfo.InvariantCulture)})"
			: column.StoreType;
	}

	/// <summary>The headline type for a column.</summary>
	public static string Primary(EfColumn column, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(column);
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrEmpty(column.DomainTypeName))
			return DatabaseType(column);

		// The domain type is present, so the headline is either the domain type or the database type,
		// depending on the configured display order.
		return options.DomainTypeDisplay is DomainTypeDisplay.DatabaseFirst or DomainTypeDisplay.DatabaseOnly
			? DatabaseType(column)
			: column.DomainTypeName;
	}

	/// <summary>
	/// The label kept alongside the headline — the other type, plus the <c>enum</c> marker when the
	/// column's declared type is an enum.
	/// </summary>
	/// <returns><see langword="null"/> when there is nothing to keep.</returns>
	public static string? Annotation(EfColumn column, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(column);
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrEmpty(column.DomainTypeName))
		{
			return null;
		}

		var marker = column.DomainTypeKind == EfDomainTypeKind.Enum;

#pragma warning disable IDE0072 // Add missing cases
		return options.DomainTypeDisplay switch
		{
			DomainTypeDisplay.DomainTypeFirst => marker ? $"{DatabaseType(column)}, enum" : DatabaseType(column),
			DomainTypeDisplay.DatabaseFirst => marker ? $"{column.DomainTypeName}, enum" : column.DomainTypeName,
			// The database type is not the headline, so it is kept here; the enum marker is inline.
			DomainTypeDisplay.DomainTypeOnly => DatabaseType(column),
			_ => null,
		};
#pragma warning restore IDE0072 // Add missing cases
	}

	/// <summary>Composes the inline label, e.g. <c>AssetId (guid)</c> or <c>ActorType (int, enum)</c>.</summary>
	public static string Compose(EfColumn column, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(column);
		ArgumentNullException.ThrowIfNull(options);

		var primary = Primary(column, options);
		List<string> parts = [];

		if (
			options.DomainTypeDisplay is DomainTypeDisplay.DomainTypeFirst or DomainTypeDisplay.DatabaseFirst
			&& !string.IsNullOrEmpty(column.DomainTypeName)
		)
		{
			parts.Add(
				options.DomainTypeDisplay == DomainTypeDisplay.DatabaseFirst
					? column.DomainTypeName
					: DatabaseType(column)
			);
		}

		if (column.DomainTypeKind == EfDomainTypeKind.Enum)
		{
			parts.Add("enum");
		}

		return parts.Count == 0 ? primary : $"{primary} ({string.Join(", ", parts)})";
	}
}
