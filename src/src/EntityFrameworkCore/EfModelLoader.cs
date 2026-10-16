using System.Globalization;
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
	/// searching <c>bin/</c> for the newest build output that carries a descriptor.
	/// </remarks>
	public static EfModel LoadFromProject(string projectPath) => LoadFromProject(projectPath, out _);

	/// <summary>
	/// Finds the most recently built assembly for a project and reads its descriptor, reporting what
	/// the lookup inspected.
	/// </summary>
	/// <param name="projectPath">Full path to the project's <c>.csproj</c>.</param>
	/// <param name="diagnostics">
	/// What the lookup inspected, so a caller can explain an empty model. Without it the usual cause —
	/// a build output that does not exist yet — is indistinguishable from a project that genuinely
	/// declares no <c>DbContext</c>.
	/// </param>
	/// <remarks>
	/// Aspire exposes the project file path but not its build output, so the assembly is located by
	/// searching <c>bin/</c> for the newest build output that carries a descriptor.
	/// </remarks>
	public static EfModel LoadFromProject(string projectPath, out EfModelLoadDiagnostics diagnostics)
	{
		var outputDirectory = FindOutputDirectory(projectPath);

		if (outputDirectory is null)
		{
			diagnostics = new EfModelLoadDiagnostics(projectPath, null, 0, 0);

			return EfModel.Empty;
		}

		List<EfModel> models = [];
		var scanned = 0;

		try
		{
			// Every assembly in the output directory is checked, not just the project's own: a
			// DbContext usually lives in a referenced data library rather than the service executable.
			// The read is metadata-only and bails immediately on assemblies without the attribute.
			foreach (var candidate in Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly))
			{
				scanned++;

				if (EfModelReader.TryRead(candidate, out var descriptor))
				{
					models.Add(Parse(descriptor));
				}
			}
		}
		catch (IOException)
		{
			diagnostics = new EfModelLoadDiagnostics(projectPath, outputDirectory, scanned, models.Count);

			return EfModel.Empty;
		}
		catch (UnauthorizedAccessException)
		{
			diagnostics = new EfModelLoadDiagnostics(projectPath, outputDirectory, scanned, models.Count);

			return EfModel.Empty;
		}

		diagnostics = new EfModelLoadDiagnostics(projectPath, outputDirectory, scanned, models.Count);

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
	/// <para>
	/// A directory that actually carries a descriptor wins over a newer one that does not. Taking the
	/// newest assembly alone is not enough: satellite resource folders, <c>ref</c> assemblies and
	/// unrelated build output all live under <c>bin</c>, and choosing one of those produces an empty
	/// diagram while every path involved still looks correct.
	/// </para>
	/// </remarks>
	public static string? FindOutputDirectory(string projectPath)
	{
		var directories = FindOutputDirectories(projectPath);

		return directories.Count > 0 ? directories[0] : null;
	}

	/// <summary>Lists a project's candidate build output directories, best candidate first.</summary>
	static List<string> FindOutputDirectories(string projectPath)
	{
		if (string.IsNullOrWhiteSpace(projectPath))
		{
			return [];
		}

		var directory = Path.GetDirectoryName(projectPath);

		if (directory is null)
		{
			return [];
		}

		var bin = Path.Combine(directory, "bin");

		if (!Directory.Exists(bin))
		{
			return [];
		}

		try
		{
			List<(string Directory, DateTime Newest)> candidates = [];

			foreach (var candidate in EnumerateCandidateDirectories(bin))
			{
				if (NewestAssemblyTimestamp(candidate) is { } newest)
				{
					candidates.Add((candidate, newest));
				}
			}

			if (candidates.Count == 0)
			{
				return [];
			}

			// The common case is a single output directory; only pay for the descriptor probe when
			// there is genuinely more than one place the assembly could be.
			if (candidates.Count == 1)
			{
				return [candidates[0].Directory];
			}

			var newestFirst = candidates.OrderByDescending(c => c.Newest).Select(c => c.Directory).ToList();
			var withDescriptor = newestFirst.Where(ContainsDescriptor).ToList();

			return withDescriptor.Count == 0
				? newestFirst
				: [.. withDescriptor, .. newestFirst.Where(d => !withDescriptor.Contains(d))];
		}
		catch (IOException)
		{
			return [];
		}
		catch (UnauthorizedAccessException)
		{
			return [];
		}
	}

	/// <summary>Every directory under <c>bin</c> that could hold a build output, excluding noise.</summary>
	static IEnumerable<string> EnumerateCandidateDirectories(string bin)
	{
		yield return bin;

		foreach (var directory in Directory.EnumerateDirectories(bin, "*", SearchOption.AllDirectories))
		{
			if (IsExcludedDirectory(directory))
			{
				continue;
			}

			yield return directory;
		}
	}

	/// <summary>Whether a directory can never be the project's own build output.</summary>
	/// <remarks>
	/// <c>runtimes</c> holds architecture-specific native payloads, and a culture-named folder holds
	/// satellite resource assemblies — neither carries the project's descriptor.
	/// </remarks>
	static bool IsExcludedDirectory(string directory)
	{
		var name = Path.GetFileName(directory);

		if (name.Length == 0)
		{
			return false;
		}

		if (
			name.Equals("runtimes", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("ref", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("refint", StringComparison.OrdinalIgnoreCase)
		)
		{
			return true;
		}

		try
		{
			_ = CultureInfo.GetCultureInfo(name);

			return true;
		}
		catch (ArgumentException)
		{
			// Not a culture name, so it is a real output folder (Debug, net10.0, …).
			return false;
		}
	}

	/// <summary>The newest assembly in a directory, or <see langword="null"/> when it holds none.</summary>
	static DateTime? NewestAssemblyTimestamp(string directory)
	{
		try
		{
			DateTime? newest = null;

			foreach (var assembly in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
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

	/// <summary>Whether any assembly in a directory carries an EF descriptor.</summary>
	static bool ContainsDescriptor(string directory)
	{
		try
		{
			foreach (var assembly in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
			{
				if (EfModelReader.TryRead(assembly, out _))
				{
					return true;
				}
			}
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}

		return false;
	}
}

/// <summary>Describes what a descriptor lookup inspected.</summary>
/// <param name="ProjectPath">The project whose build output was searched.</param>
/// <param name="OutputDirectory">The output directory that was read, or <see langword="null"/> when none was found.</param>
/// <param name="AssembliesScanned">How many assemblies in that directory were read.</param>
/// <param name="DescriptorsFound">How many of them carried an EF descriptor.</param>
public readonly record struct EfModelLoadDiagnostics(
	string ProjectPath,
	string? OutputDirectory,
	int AssembliesScanned,
	int DescriptorsFound
);
