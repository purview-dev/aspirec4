using System.Text.Json;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Reads the authoritative model a build-time dump leaves beneath a project's <c>obj/</c> directory.
/// </summary>
/// <remarks>
/// <para>
/// The dump is written by the AspireC4 MSBuild target for a project that opts in. It reflects the real
/// EF Core model — conventions, value converters and composite foreign keys included — so it is
/// preferred over the analyzer descriptor and the metadata reader.
/// </para>
/// <para>
/// It lives under <c>obj/</c>, which is never copied to <c>bin/</c> or <c>publish/</c>, so it carries
/// no deployment footprint. A dump older than the assembly it describes is ignored, so a stale build
/// output can never produce a wrong diagram.
/// </para>
/// </remarks>
static class EfModelDumpReader
{
	/// <summary>Directory holding the dump, relative to a project's directory.</summary>
	internal const string RelativeDirectory = "obj/aspirec4/ef";

	/// <summary>Attempts to read a fresh dump for a project.</summary>
	/// <param name="projectPath">The project's <c>.csproj</c> path.</param>
	/// <param name="outputDirectory">
	/// The project's build output directory, used only for the freshness check. May be
	/// <see langword="null"/>, in which case the dump is trusted as-is.
	/// </param>
	/// <param name="model">The dumped model, or <see cref="EfModel.Empty"/>.</param>
	/// <returns>Whether a usable dump was found.</returns>
	public static bool TryRead(string projectPath, string? outputDirectory, out EfModel model)
	{
		model = EfModel.Empty;

		if (string.IsNullOrWhiteSpace(projectPath))
		{
			return false;
		}

		var projectDirectory = Path.GetDirectoryName(projectPath);

		if (projectDirectory is null)
		{
			return false;
		}

		var directory = Path.Combine(projectDirectory, "obj", "aspirec4", "ef");

		if (!Directory.Exists(directory))
		{
			return false;
		}

		try
		{
			List<string> files = [.. Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)];

			if (files.Count == 0)
			{
				return false;
			}

			// A dump that predates the built assembly describes a model that no longer exists.
			if (NewestAssemblyTimestamp(outputDirectory) is { } built)
			{
				foreach (var file in files)
				{
					if (File.GetLastWriteTimeUtc(file) < built)
					{
						return false;
					}
				}
			}

			List<EfModel> models = [];

			foreach (var file in files)
			{
				var parsed = EfModelLoader.Parse(File.ReadAllText(file));

				if (parsed.Contexts.Count > 0)
				{
					models.Add(parsed);
				}
			}

			if (models.Count == 0)
			{
				return false;
			}

			model = EfModelLoader.Merge(models);

			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	/// <summary>
	/// Reads the dump for a project and every project it references.
	/// </summary>
	/// <remarks>
	/// The dump runs in the project that owns the EF Core provider, which is frequently a referenced
	/// persistence project rather than the service the AppHost starts. The restored asset graph carries
	/// the transitive project closure, so that is what is walked.
	/// </remarks>
	public static bool TryReadGraph(string projectPath, string? outputDirectory, out EfModel model)
	{
		model = EfModel.Empty;

		List<EfModel> models = [];

		if (TryRead(projectPath, outputDirectory, out var own) && own.Contexts.Count > 0)
		{
			models.Add(own);
		}

		foreach (var referenced in ReferencedProjects(projectPath))
		{
			if (TryRead(referenced, null, out var dump) && dump.Contexts.Count > 0)
			{
				models.Add(dump);
			}
		}

		if (models.Count == 0)
		{
			return false;
		}

		model = EfModelLoader.Merge(models);

		return true;
	}

	/// <summary>The projects a project references, taken from its restored asset graph.</summary>
	/// <remarks>The graph is already the transitive closure, so no recursion is needed.</remarks>
	static List<string> ReferencedProjects(string projectPath)
	{
		List<string> projects = [];
		var projectDirectory = Path.GetDirectoryName(projectPath);

		if (projectDirectory is null)
		{
			return projects;
		}

		var assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");

		if (!File.Exists(assetsPath))
		{
			return projects;
		}

		try
		{
			using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));

			if (
				!document.RootElement.TryGetProperty("libraries", out var libraries)
				|| libraries.ValueKind != JsonValueKind.Object
			)
			{
				return projects;
			}

			foreach (var library in libraries.EnumerateObject())
			{
				if (!library.Value.TryGetProperty("type", out var type) || type.GetString() != "project")
				{
					continue;
				}

				if (
					!library.Value.TryGetProperty("msbuildProject", out var msbuildProject)
					|| msbuildProject.GetString() is not { Length: > 0 } relative
				)
				{
					continue;
				}

				var path = Path.GetFullPath(Path.Combine(projectDirectory, relative));

				if (File.Exists(path))
				{
					projects.Add(path);
				}
			}
		}
		catch (JsonException)
		{
			// An unreadable graph yields no referenced projects; the project's own dump still counts.
		}
		catch (IOException)
		{
			// As above.
		}

		return projects;
	}

	/// <summary>The newest assembly in a directory, or <see langword="null"/> when it holds none.</summary>
	static DateTime? NewestAssemblyTimestamp(string? outputDirectory)
	{
		if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
		{
			return null;
		}

		try
		{
			DateTime? newest = null;

			foreach (var assembly in Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly))
			{
				var written = File.GetLastWriteTimeUtc(assembly);

				if (newest is null || written > newest)
				{
					newest = written;
				}
			}

			return newest;
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
	}
}
