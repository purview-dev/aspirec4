#:project ../src/src/EntityFrameworkCore/EntityFrameworkCore.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true

// Emits an ER diagram in any supported format from a generated model descriptor.
//
// The AppHost writes whichever format its renderer is set to; this exists so the other formats can be
// produced from the same model without reconfiguring and re-running the AppHost.
//
// The source may be a previously generated model JSON, or a .csproj — in which case the descriptors
// are read straight out of that project's build output, exactly as the AppHost does, so the result
// can never be a stale snapshot.
//
//   dotnet run scripts/erd-emit.cs -- <model.json|project.csproj> <Mermaid|Dbml|D2|Interactive> [output-directory]
using Aspire.Hosting.AspireC4.EntityFrameworkCore;

if (args.Length < 2)
{
	Console.Error.WriteLine(
		"usage: erd-emit <model.json|project.csproj> <Mermaid|Dbml|D2|Interactive> [output-directory]"
	);

	return 1;
}

var source = args[0];

if (!File.Exists(source))
{
	Console.Error.WriteLine($"Source not found: {source}");

	return 1;
}

if (!Enum.TryParse<ErdRenderer>(args[1], ignoreCase: true, out var renderer))
{
	Console.Error.WriteLine(
		$"Unknown renderer '{args[1]}'. Expected one of: {string.Join(", ", Enum.GetNames<ErdRenderer>())}"
	);

	return 1;
}

var outputDirectory = args.Length > 2 ? args[2] : Path.GetDirectoryName(Path.GetFullPath(source))!;

Directory.CreateDirectory(outputDirectory);

var fromProject = Path.GetExtension(source).Equals(".csproj", StringComparison.OrdinalIgnoreCase);

var model = fromProject
	? EfModelLoader.LoadFromProject(Path.GetFullPath(source))
	: EfModelLoader.Parse(await File.ReadAllTextAsync(source));

if (model.Contexts.Count == 0)
{
	Console.Error.WriteLine(
		fromProject
			? $"No EF descriptors found in the build output of {source}. Build it first."
			: $"No contexts in {source}."
	);

	return 1;
}

AspireC4EfDiagramOptions options = new()
{
	Renderer = renderer,
	FileName = fromProject ? "schema.gen" : Path.GetFileNameWithoutExtension(source),
	Title = "AspireC4 Sample Schema",
};

foreach (var file in ErdOutput.Create(model, options))
{
	var path = Path.Combine(outputDirectory, file.FileName);

	await File.WriteAllTextAsync(path, file.Content);

	Console.WriteLine($"wrote {path}");
}

return 0;
