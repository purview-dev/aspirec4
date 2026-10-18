// AspireC4 EF Core model dump.
//
// Invoked by the AspireC4 MSBuild target in a project that owns an EF Core provider. See
// EfModelDumpRunner for what it does and where it writes.

var options = Options.Parse(args);

if (options.AssemblyPath is null || options.OutputDirectory is null)
{
	Console.Error.WriteLine("usage: --assembly <path> --output <dir> [--connection <connection-string>]");

	return 2;
}

return EfModelDumpRunner.Run(options.AssemblyPath, options.OutputDirectory, options.ConnectionString);

/// <summary>The tool's command line.</summary>
sealed record Options(string? AssemblyPath, string? OutputDirectory, string? ConnectionString)
{
	public static Options Parse(string[] args)
	{
		string? assembly = null;
		string? output = null;
		string? connection = null;

		for (var i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--assembly" when i + 1 < args.Length:
					assembly = args[++i];
					break;
				case "--output" when i + 1 < args.Length:
					output = args[++i];
					break;
				case "--connection" when i + 1 < args.Length:
					connection = args[++i];
					break;
				default:
					break;
			}
		}

		return new Options(assembly, output, connection);
	}
}
