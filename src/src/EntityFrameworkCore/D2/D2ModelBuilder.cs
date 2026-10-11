using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Models;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;

/// <summary>
/// Converts the EF Core descriptor produced by the source generator into a <see cref="D2Model"/>.
/// </summary>
public static class D2ModelBuilder
{
	/// <summary>Builds a d2 model from one or more EF descriptors.</summary>
	/// <param name="model">The merged EF descriptor.</param>
	/// <param name="options">Diagram options controlling grouping and column rendering.</param>
	public static D2Model Build(EfModel model, AspireC4EfDiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		if (model.Contexts.Count == 0)
		{
			return D2Model.Empty;
		}

		var groupByContext = options.GroupByContext && model.Contexts.Count > 1;

		List<D2Table> tables = [];
		// Maps a table name to its fully-qualified d2 path, so foreign keys can resolve their target.
		Dictionary<string, string> pathsByTableName = [with(StringComparer.OrdinalIgnoreCase)];
		HashSet<string> usedPaths = [with(StringComparer.Ordinal)];

		foreach (var context in model.Contexts)
		{
			foreach (var table in context.Tables)
			{
				var container = BuildContainer(context, table, options, groupByContext);
				var id = UniqueId(D2DSLGenerator.Sanitize(table.Name), container, usedPaths);

				tables.Add(
					new D2Table
					{
						Id = id,
						Label = table.Name,
						Container = container,
						Columns = [.. table.Columns.Select(c => BuildColumn(c, options))],
					}
				);

				pathsByTableName[table.Name] = Join(container, id);
			}
		}

		return new D2Model { Tables = tables, Edges = BuildEdges(model, pathsByTableName) };
	}

	static string? BuildContainer(
		EfContext context,
		EfTable table,
		AspireC4EfDiagramOptions options,
		bool groupByContext
	)
	{
		List<string> segments = [];

		if (groupByContext)
		{
			segments.Add(D2DSLGenerator.Sanitize(context.Name));
		}

		var schema = table.Schema ?? context.DefaultSchema;

		if (options.GroupBySchema && !string.IsNullOrWhiteSpace(schema))
		{
			segments.Add(D2DSLGenerator.Sanitize(schema));
		}

		return segments.Count == 0 ? null : string.Join(".", segments);
	}

	static D2Column BuildColumn(EfColumn column, AspireC4EfDiagramOptions options)
	{
		List<D2Constraint> constraints = [];

		if (column.IsPrimaryKey)
		{
			constraints.Add(D2Constraint.PrimaryKey);
		}

		if (column.IsForeignKey)
		{
			constraints.Add(D2Constraint.ForeignKey);
		}

		// A primary key is already unique; repeating it adds noise to every row.
		if (column.IsUnique && !column.IsPrimaryKey)
		{
			constraints.Add(D2Constraint.Unique);
		}

		return new D2Column
		{
			Name = column.Name,
			Type = BuildType(column, options),
			Constraints = constraints,
		};
	}

	static string BuildType(EfColumn column, AspireC4EfDiagramOptions options)
	{
		var type = column.StoreType;

		// Only append the length when the configured store type does not already carry one,
		// e.g. an explicit HasColumnType("varchar(64)").
		if (column.MaxLength is > 0 && !type.Contains('(', StringComparison.Ordinal))
		{
			type = $"{type}({column.MaxLength.Value})";
		}

		// A nullable primary key is not meaningful, so the marker is suppressed there.
		if (options.ShowNullability && column.IsNullable && !column.IsPrimaryKey)
		{
			type += "?";
		}

		return type;
	}

	static List<D2Edge> BuildEdges(EfModel model, Dictionary<string, string> pathsByTableName)
	{
		List<D2Edge> edges = [];
		HashSet<string> seen = [with(StringComparer.Ordinal)];

		foreach (var context in model.Contexts)
		{
			foreach (var table in context.Tables)
			{
				if (!pathsByTableName.TryGetValue(table.Name, out var sourcePath))
				{
					continue;
				}

				foreach (var foreignKey in table.ForeignKeys)
				{
					// A foreign key can point at a table outside the analysed set, e.g. when the
					// principal entity was not discoverable. Those edges are skipped rather than
					// emitted as dangling references d2 would reject.
					if (!pathsByTableName.TryGetValue(foreignKey.PrincipalTable, out var targetPath))
					{
						continue;
					}

					var pairs = Math.Min(foreignKey.Columns.Count, foreignKey.PrincipalColumns.Count);

					for (var i = 0; i < pairs; i++)
					{
						D2Edge edge = new()
						{
							SourceTable = sourcePath,
							SourceColumn = foreignKey.Columns[i],
							TargetTable = targetPath,
							TargetColumn = foreignKey.PrincipalColumns[i],
							Label = foreignKey.NavigationName,
						};

						// Composite keys and multiple navigations can describe the same column pair.
						var key = $"{edge.SourceTable}.{edge.SourceColumn}->{edge.TargetTable}.{edge.TargetColumn}";

						if (seen.Add(key))
						{
							edges.Add(edge);
						}
					}
				}
			}
		}

		return edges;
	}

	static string UniqueId(string id, string? container, HashSet<string> usedPaths)
	{
		var candidate = id;
		var suffix = 2;

		while (!usedPaths.Add(Join(container, candidate)))
		{
			candidate = $"{id}_{suffix++}";
		}

		return candidate;
	}

	static string Join(string? container, string id) => string.IsNullOrEmpty(container) ? id : $"{container}.{id}";
}
