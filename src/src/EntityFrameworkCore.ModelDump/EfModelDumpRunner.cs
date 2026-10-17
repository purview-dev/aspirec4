using System.Reflection;
using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.ModelDump;

/// <summary>
/// Builds each <c>DbContext</c>'s real model through the project's design-time factory and writes the
/// AspireC4 diagram contract to a directory.
/// </summary>
/// <remarks>
/// This is what the MSBuild target invokes. It runs in the project that owns the EF Core provider, so
/// the model is built exactly as the application would build it — conventions, value converters and
/// composite foreign keys included. The output directory is the project's <c>obj/</c>, so nothing it
/// writes is deployed or committed.
/// </remarks>
public static class EfModelDumpRunner
{
	/// <summary>Dumps every context the assembly's design-time factories expose.</summary>
	/// <param name="assemblyPath">The built assembly to read contexts from.</param>
	/// <param name="outputDirectory">Where to write the dump. Created when missing.</param>
	/// <param name="connectionString">
	/// Optional connection string passed to the factories. The model is never opened, so any
	/// syntactically valid value works.
	/// </param>
	/// <returns>0 on success, 1 when nothing was written, 2 for a usage error.</returns>
	public static int Run(string assemblyPath, string outputDirectory, string? connectionString = null)
	{
		ArgumentNullException.ThrowIfNull(assemblyPath);
		ArgumentNullException.ThrowIfNull(outputDirectory);

		var fullPath = Path.GetFullPath(assemblyPath);
		var assemblyDirectory = Path.GetDirectoryName(fullPath)!;

		AssemblyLoadContext.Default.Resolving += (context, name) =>
		{
			var candidate = Path.Combine(assemblyDirectory, name.Name + ".dll");

			return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
		};

		Assembly assembly;

		try
		{
			assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
		}
		catch (Exception ex) when (ex is BadImageFormatException or FileNotFoundException or FileLoadException)
		{
			Console.Error.WriteLine($"aspirec4: could not load '{fullPath}': {ex.Message}");

			return 1;
		}

		var factoryInterface = typeof(IDesignTimeDbContextFactory<>);
		List<(Type Factory, Type Context)> contexts = [];

		foreach (var type in LoadableTypes(assembly))
		{
			if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
			{
				continue;
			}

			var factory = type.GetInterfaces()
				.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == factoryInterface);

			if (factory is not null)
			{
				contexts.Add((type, factory.GetGenericArguments()[0]));
			}
		}

		if (contexts.Count == 0)
		{
			// No design-time factory: nothing to dump. The AppHost falls back to the metadata reader.
			return 0;
		}

		Directory.CreateDirectory(outputDirectory);

		// Clear previous output so a removed context does not linger as a stale, still-"fresh" file.
		foreach (var stale in Directory.EnumerateFiles(outputDirectory, "*.json"))
		{
			File.Delete(stale);
		}

		var connectionArgs = string.IsNullOrWhiteSpace(connectionString)
			? []
			: new[] { $"--connection={connectionString}" };

		var written = 0;

		foreach (var (factoryType, contextType) in contexts)
		{
			try
			{
				var factory = Activator.CreateInstance(factoryType);
				var create = factoryType.GetMethod("CreateDbContext")!;

				if (create.Invoke(factory, [connectionArgs]) is not DbContext context)
				{
					Console.Error.WriteLine($"aspirec4: '{factoryType.Name}' returned no context.");

					continue;
				}

				// The design-time model carries the relational configuration (comments, column types);
				// the read-optimized runtime model does not.
				var model = context.GetService<IDesignTimeModel>().Model;

				var json = EfModelDumpProjector.Project(
					model,
					contextType.Name,
					contextType.FullName ?? contextType.Name
				);

				File.WriteAllText(Path.Combine(outputDirectory, $"{contextType.Name}.json"), json);
				written++;
			}
#pragma warning disable CA1031 // A single bad context must not fail the build; the AppHost falls back.
			catch (Exception ex)
			{
				// A factory that cannot build its model (usually a missing connection string) must not
				// stop the others; the AppHost falls back to the metadata reader for that context.
				Console.Error.WriteLine(
					$"aspirec4: could not dump '{contextType.Name}': {ex.GetBaseException().Message}"
				);
			}
#pragma warning restore CA1031
		}

		return written > 0 ? 0 : 1;
	}

	static IEnumerable<Type> LoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(type => type is not null)!;
		}
	}
}
