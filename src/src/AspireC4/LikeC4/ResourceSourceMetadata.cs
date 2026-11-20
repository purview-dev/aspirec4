using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// Reads where a resource comes from — the local project it is built from and the runtime it targets, or
/// the container image it runs — as LikeC4 metadata.
/// </summary>
/// <remarks>
/// <para>
/// A <b>local project</b> is a .NET project (any resource carrying <see cref="IProjectMetadata"/>, so both
/// <c>AddProject</c> and Aspire 17's <c>AddDotnetProject</c>) or an executable whose working directory holds
/// a Node, Python or Go project marker. It receives <see cref="ProjectPathKey"/>, relative to the AppHost
/// directory so the value is the same on every machine, and the runtime version its own files declare.
/// </para>
/// <para>
/// A <b>container</b> receives its image reference from <see cref="ContainerImageAnnotation"/>, and the
/// Dockerfile it is built from when there is one.
/// </para>
/// <para>
/// Reading touches the file system, so it runs once per AppHost start rather than on every regeneration,
/// and it is best-effort: a file that is missing, unreadable or malformed contributes nothing and never
/// fails diagram generation.
/// </para>
/// </remarks>
static class ResourceSourceMetadata
{
	/// <summary>The project file (.NET) or project directory (Node, Python, Go), relative to the AppHost.</summary>
	public const string ProjectPathKey = "project-path";

	/// <summary>The target framework(s) of a .NET project, e.g. <c>net10.0</c>.</summary>
	public const string DotnetTargetFrameworkKey = "dotnet-target-framework";

	/// <summary>The Node version a JavaScript project declares.</summary>
	public const string NodeVersionKey = "node-version";

	/// <summary>The Bun version a JavaScript project declares in <c>engines.bun</c>.</summary>
	public const string BunVersionKey = "bun-version";

	/// <summary>The package manager a JavaScript project pins, e.g. <c>pnpm@9.12.0</c>.</summary>
	public const string PackageManagerKey = "package-manager";

	/// <summary>The Python version a Python project declares.</summary>
	public const string PythonVersionKey = "python-version";

	/// <summary>The Go toolchain or language version a Go module declares.</summary>
	public const string GoVersionKey = "go-version";

	/// <summary>The full container image reference, e.g. <c>mcr.microsoft.com/mssql/server:2025-latest</c>.</summary>
	public const string ContainerImageKey = "container-image";

	/// <summary>The container image tag, e.g. <c>2025-latest</c>.</summary>
	public const string ContainerTagKey = "container-tag";

	/// <summary>The pinned image digest, when the image is pinned by one.</summary>
	public const string ContainerDigestKey = "container-digest";

	/// <summary>The Dockerfile a container is built from, relative to the AppHost.</summary>
	public const string DockerfileKey = "dockerfile";

	static readonly JsonDocumentOptions JsonOptions = new()
	{
		AllowTrailingCommas = true,
		CommentHandling = JsonCommentHandling.Skip,
	};

	/// <summary>
	/// Reads the source metadata of every resource, keyed by resource name.
	/// </summary>
	/// <remarks>
	/// A hidden resource and the visible surrogate that replaces it (an Azure resource run as a container)
	/// share a name; the first one with anything to report wins, which is the surrogate's image.
	/// </remarks>
	public static IReadOnlyDictionary<string, IReadOnlyList<LikeC4Metadata>> ReadAll(
		IEnumerable<IResource> resources,
		string appHostDirectory
	)
	{
		ArgumentNullException.ThrowIfNull(resources);

		Dictionary<string, IReadOnlyList<LikeC4Metadata>> result = new(StringComparer.OrdinalIgnoreCase);
		foreach (var resource in resources)
		{
			if (result.ContainsKey(resource.Name))
			{
				continue;
			}

			var metadata = Read(resource, appHostDirectory);
			if (metadata.Count > 0)
			{
				result[resource.Name] = metadata;
			}
		}

		return result;
	}

	/// <summary>Reads the source metadata of one resource.</summary>
	public static IReadOnlyList<LikeC4Metadata> Read(IResource resource, string appHostDirectory)
	{
		ArgumentNullException.ThrowIfNull(resource);
		ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);

		List<LikeC4Metadata> metadata = [];

		// IProjectMetadata first: Aspire 17's DotnetProjectResource is also an ExecutableResource.
		if (
			resource.Annotations.OfType<IProjectMetadata>().LastOrDefault() is { } project
			&& !string.IsNullOrWhiteSpace(project.ProjectPath)
		)
		{
			var projectPath = Path.GetFullPath(project.ProjectPath, appHostDirectory);
			metadata.Add(new(ProjectPathKey, RelativePath(appHostDirectory, projectPath)));
			AddIfPresent(metadata, DotnetTargetFrameworkKey, ReadTargetFrameworks(projectPath));
		}
		else if (resource is ExecutableResource { WorkingDirectory: { Length: > 0 } workingDirectory })
		{
			AddLocalProject(metadata, Path.GetFullPath(workingDirectory, appHostDirectory), appHostDirectory);
		}
		else if (resource is ContainerResource)
		{
			AddContainer(metadata, resource, appHostDirectory);
		}

		return metadata;
	}

	static void AddLocalProject(List<LikeC4Metadata> metadata, string directory, string appHostDirectory)
	{
		if (!Directory.Exists(directory))
		{
			return;
		}

		var packageJson = Path.Combine(directory, "package.json");
		var goMod = Path.Combine(directory, "go.mod");
		var isNode = File.Exists(packageJson);
		var isGo = File.Exists(goMod);
		var isPython =
			File.Exists(Path.Combine(directory, "pyproject.toml"))
			|| File.Exists(Path.Combine(directory, ".python-version"))
			|| File.Exists(Path.Combine(directory, "requirements.txt"));

		// Only a directory that is recognisably a project: an arbitrary executable's working directory (a
		// temp folder, the AppHost itself) says nothing about what it runs.
		if (!isNode && !isGo && !isPython)
		{
			return;
		}

		metadata.Add(new(ProjectPathKey, RelativePath(appHostDirectory, directory)));

		if (isNode)
		{
			var (nodeEngine, bunEngine, packageManager) = ReadPackageJson(packageJson);
			AddIfPresent(
				metadata,
				NodeVersionKey,
				ReadFirstLine(Path.Combine(directory, ".nvmrc"))
					?? ReadFirstLine(Path.Combine(directory, ".node-version"))
					?? nodeEngine
			);
			AddIfPresent(metadata, BunVersionKey, bunEngine);
			AddIfPresent(metadata, PackageManagerKey, packageManager);
		}

		if (isPython)
		{
			AddIfPresent(
				metadata,
				PythonVersionKey,
				ReadFirstLine(Path.Combine(directory, ".python-version"))
					?? ReadRequiresPython(Path.Combine(directory, "pyproject.toml"))
			);
		}

		if (isGo)
		{
			AddIfPresent(metadata, GoVersionKey, ReadGoVersion(goMod));
		}
	}

	static void AddContainer(List<LikeC4Metadata> metadata, IResource resource, string appHostDirectory)
	{
		if (resource.Annotations.OfType<ContainerImageAnnotation>().LastOrDefault() is { } image)
		{
			var reference = string.IsNullOrWhiteSpace(image.Registry) ? image.Image : $"{image.Registry}/{image.Image}";
			// Aspire keeps a tag and a digest mutually exclusive (setting one clears the other), and the
			// reference is spelled the way docker pull takes it: image:tag or image@sha256:digest.
			var digest = string.IsNullOrWhiteSpace(image.SHA256) ? null : $"sha256:{image.SHA256}";
			if (digest is not null)
			{
				reference += $"@{digest}";
			}
			else if (!string.IsNullOrWhiteSpace(image.Tag))
			{
				reference += $":{image.Tag}";
			}

			AddIfPresent(metadata, ContainerImageKey, reference);
			AddIfPresent(metadata, ContainerTagKey, image.Tag);
			AddIfPresent(metadata, ContainerDigestKey, digest);
		}

		if (
			resource.Annotations.OfType<DockerfileBuildAnnotation>().LastOrDefault() is { } dockerfile
			&& !string.IsNullOrWhiteSpace(dockerfile.DockerfilePath)
		)
		{
			metadata.Add(
				new(
					DockerfileKey,
					RelativePath(appHostDirectory, Path.GetFullPath(dockerfile.DockerfilePath, appHostDirectory))
				)
			);
		}
	}

	/// <summary>
	/// The target framework(s) of a .NET project: from the restore output first, because an SDK such as
	/// Purview.BuildSdk sets them outside the project file, then from the project file itself.
	/// </summary>
	internal static string? ReadTargetFrameworks(string projectPath)
	{
		var directory = Path.GetDirectoryName(projectPath);
		if (directory is null)
		{
			return null;
		}

		if (projectPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
		{
			return ReadFileBasedAppTargetFramework(projectPath);
		}

		return ReadAssetsTargetFrameworks(Path.Combine(directory, "obj", "project.assets.json"))
			?? ReadProjectFileTargetFrameworks(projectPath);
	}

	static string? ReadAssetsTargetFrameworks(string assetsPath)
	{
		var root = TryParseJson(assetsPath);
		if (root is null)
		{
			return null;
		}

		using (root)
		{
			if (!root.RootElement.TryGetProperty("project", out var project))
			{
				return null;
			}

			// originalTargetFrameworks keeps the project's own order and spelling; frameworks is the fallback.
			if (
				project.TryGetProperty("restore", out var restore)
				&& restore.TryGetProperty("originalTargetFrameworks", out var original)
				&& original.ValueKind == JsonValueKind.Array
			)
			{
				var names = original
					.EnumerateArray()
					.Where(e => e.ValueKind == JsonValueKind.String)
					.Select(e => e.GetString())
					.ToList();
				if (names.Count > 0)
				{
					return string.Join(", ", names);
				}
			}

			if (
				project.TryGetProperty("frameworks", out var frameworks)
				&& frameworks.ValueKind == JsonValueKind.Object
			)
			{
				var names = frameworks.EnumerateObject().Select(p => p.Name).ToList();
				if (names.Count > 0)
				{
					return string.Join(", ", names);
				}
			}

			return null;
		}
	}

	static string? ReadProjectFileTargetFrameworks(string projectPath)
	{
		try
		{
			if (!File.Exists(projectPath))
			{
				return null;
			}

			var document = XDocument.Load(projectPath);
			var value = document
				.Descendants()
				.Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
				.Select(e => e.Value.Trim())
				// A property reference is not a framework; the restore output is the only way to resolve it.
				.FirstOrDefault(v => v.Length > 0 && !v.Contains("$(", StringComparison.Ordinal));

			return value is null
				? null
				: string.Join(
					", ",
					value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
		{
			return null;
		}
	}

	static string? ReadFileBasedAppTargetFramework(string sourcePath)
	{
		const string directive = "#:property TargetFramework=";
		return ReadLines(sourcePath)
			.TakeWhile(line => line.StartsWith('#') || line.Length == 0)
			.Where(line => line.StartsWith(directive, StringComparison.OrdinalIgnoreCase))
			.Select(line => line[directive.Length..].Trim())
			.FirstOrDefault(v => v.Length > 0);
	}

	static (string? Node, string? Bun, string? PackageManager) ReadPackageJson(string packageJsonPath)
	{
		var document = TryParseJson(packageJsonPath);
		if (document is null)
		{
			return (null, null, null);
		}

		using (document)
		{
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
			{
				return (null, null, null);
			}

			string? node = null;
			string? bun = null;
			if (root.TryGetProperty("engines", out var engines) && engines.ValueKind == JsonValueKind.Object)
			{
				node = StringProperty(engines, "node");
				bun = StringProperty(engines, "bun");
			}

			// Corepack allows a "+sha512.<hash>" suffix; the hash is noise in a diagram.
			var packageManager = StringProperty(root, "packageManager")?.Split('+')[0];

			return (NullIfBlank(node), NullIfBlank(bun), NullIfBlank(packageManager));
		}
	}

	static string? StringProperty(JsonElement element, string name) =>
		element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	static string? ReadRequiresPython(string pyprojectPath)
	{
		// The one key needed does not justify a TOML parser: `requires-python = ">=3.12"`.
		foreach (var line in ReadLines(pyprojectPath))
		{
			if (!line.StartsWith("requires-python", StringComparison.Ordinal))
			{
				continue;
			}

			var equals = line.IndexOf('=', StringComparison.Ordinal);
			if (equals < 0)
			{
				continue;
			}

			return NullIfBlank(line[(equals + 1)..].Trim().Trim('"', '\''));
		}

		return null;
	}

	static string? ReadGoVersion(string goModPath)
	{
		string? language = null;
		foreach (var line in ReadLines(goModPath))
		{
			// The toolchain is what actually builds the module, so it wins over the language version.
			if (line.StartsWith("toolchain ", StringComparison.Ordinal))
			{
				var toolchain = line["toolchain ".Length..].Trim();
				return toolchain.StartsWith("go", StringComparison.Ordinal) ? toolchain[2..] : toolchain;
			}

			if (line.StartsWith("go ", StringComparison.Ordinal))
			{
				language = NullIfBlank(line["go ".Length..].Trim());
			}
		}

		return language;
	}

	static JsonDocument? TryParseJson(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}

			using var stream = File.OpenRead(path);
			return JsonDocument.Parse(stream, JsonOptions);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			return null;
		}
	}

	static string? ReadFirstLine(string path) =>
		ReadLines(path).Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));

	static string[] ReadLines(string path)
	{
		try
		{
			// Materialised so an I/O failure is caught here rather than part-way through enumeration.
			return File.Exists(path) ? File.ReadAllLines(path).Select(line => line.Trim()).ToArray() : [];
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	static string RelativePath(string appHostDirectory, string path) =>
		Path.GetRelativePath(appHostDirectory, path).Replace('\\', '/');

	static void AddIfPresent(List<LikeC4Metadata> metadata, string key, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			metadata.Add(new(key, value));
		}
	}

	static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
