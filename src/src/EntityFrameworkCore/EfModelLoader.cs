using System.Text.Json;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Locates the built assembly for a project resource and loads its EF descriptor.
/// </summary>
public static class EfModelLoader
{
	static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

	/// <summary>Deserialises a descriptor produced by the source generator.</summary>
	/// <returns>The model, or <see cref="EfModel.Empty"/> when the descriptor is absent or unreadable.</returns>
	public static EfModel Parse(string? descriptor)
	{
		if (string.IsNullOrWhiteSpace(descriptor))
		{
			return EfModel.Empty;
		}

		try
		{
			return JsonSerializer.Deserialize<EfModel>(descriptor, SerializerOptions) ?? EfModel.Empty;
		}
		catch (JsonException)
		{
			// A descriptor written by a newer generator may not round-trip; an empty model degrades
			// to an empty diagram rather than failing AppHost startup.
			return EfModel.Empty;
		}
	}

	/// <summary>
	/// Finds the most recently built assembly for a project and reads its descriptor.
	/// </summary>
	/// <param name="projectPath">Full path to the project's <c>.csproj</c>.</param>
	/// <remarks>
	/// Aspire exposes the project file path but not its build output, so the assembly is located by
	/// searching <c>bin/</c> for the assembly matching the project name and taking the newest match.
	/// </remarks>
	public static EfModel LoadFromProject(string projectPath)
	{
		var outputDirectory = FindOutputDirectory(projectPath);

		if (outputDirectory is null)
		{
			return EfModel.Empty;
		}

		List<EfModel> models = [];

		try
		{
			// Every assembly in the output directory is checked, not just the project's own: a
			// DbContext usually lives in a referenced data library rather than the service executable.
			// The read is metadata-only and bails immediately on assemblies without the attribute.
			foreach (var candidate in Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly))
			{
				if (EfModelReader.TryRead(candidate, out var descriptor))
				{
					models.Add(Parse(descriptor));
				}
			}
		}
		catch (IOException)
		{
			return EfModel.Empty;
		}
		catch (UnauthorizedAccessException)
		{
			return EfModel.Empty;
		}

		return Merge(models);
	}

	/// <summary>Merges several descriptors into one model.</summary>
	public static EfModel Merge(IEnumerable<EfModel> models)
	{
		ArgumentNullException.ThrowIfNull(models);

		return new EfModel { Contexts = [.. models.SelectMany(m => m.Contexts)] };
	}

	/// <summary>Locates a project's most recent build output directory.</summary>
	/// <remarks>
	/// Deliberately does not assume the assembly is named after the project file. An
	/// <c>AssemblyName</c> that differs from the project name is common — this repository's own build
	/// SDK prefixes every assembly — so the directory is found by taking the newest non-runtime
	/// assembly under <c>bin</c> instead.
	/// </remarks>
	public static string? FindOutputDirectory(string projectPath)
	{
		if (string.IsNullOrWhiteSpace(projectPath))
		{
			return null;
		}

		var directory = Path.GetDirectoryName(projectPath);

		if (directory is null)
		{
			return null;
		}

		var bin = Path.Combine(directory, "bin");

		if (!Directory.Exists(bin))
		{
			return null;
		}

		try
		{
			var newest = Directory
				.EnumerateFiles(bin, "*.dll", SearchOption.AllDirectories)
				// Architecture-specific native payloads are never the project's own output.
				.Where(static path =>
					path.IndexOf(
						$"{Path.DirectorySeparatorChar}runtimes{Path.DirectorySeparatorChar}",
						StringComparison.OrdinalIgnoreCase
					) < 0
				)
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.FirstOrDefault();

			return newest is null ? null : Path.GetDirectoryName(newest);
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
