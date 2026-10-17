using Aspire.Hosting.AspireC4.EntityFrameworkCore.ModelDump;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Runs the dump tool's logic end to end against the test assembly, which exposes a design-time
/// factory — the same discovery the MSBuild target relies on.
/// </summary>
public sealed class EfModelDumpToolTests
{
	[Test]
	public async Task Run_DiscoversTheDesignTimeFactoryAndWritesTheModel()
	{
		// Arrange
		var output = Path.Combine(Path.GetTempPath(), $"aspirec4-dump-{Guid.NewGuid():N}");

		try
		{
			// Act
			var exitCode = EfModelDumpRunner.Run(typeof(DumpDbContext).Assembly.Location, output);

			// Assert
			await Assert.That(exitCode).IsEqualTo(0);

			var files = Directory.GetFiles(output, "*.json");

			await Assert.That(files).IsNotEmpty();

			var model = EfModelLoader.Parse(await File.ReadAllTextAsync(files[0]));

			await Assert.That(model.Contexts.Select(c => c.Name)).Contains("DumpDbContext");
		}
		finally
		{
			if (Directory.Exists(output))
			{
				Directory.Delete(output, recursive: true);
			}
		}
	}

	[Test]
	public async Task Run_ClearsPreviousOutputSoARemovedContextCannotLinger()
	{
		// Arrange
		var output = Path.Combine(Path.GetTempPath(), $"aspirec4-dump-{Guid.NewGuid():N}");

		Directory.CreateDirectory(output);
		await File.WriteAllTextAsync(Path.Combine(output, "Stale.json"), "{}");

		try
		{
			// Act
			EfModelDumpRunner.Run(typeof(DumpDbContext).Assembly.Location, output);

			// Assert
			await Assert.That(File.Exists(Path.Combine(output, "Stale.json"))).IsFalse();
		}
		finally
		{
			if (Directory.Exists(output))
			{
				Directory.Delete(output, recursive: true);
			}
		}
	}
}
