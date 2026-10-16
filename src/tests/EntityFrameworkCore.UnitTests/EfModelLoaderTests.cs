using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Aspire.Hosting.AspireC4.TestAppHost.SimpleData;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

public sealed class EfModelLoaderTests
{
	/// <summary>
	/// A descriptor in exactly the shape the source generator emits. This is the contract between the
	/// generator and the AppHost, so it is asserted literally rather than round-tripped through a
	/// serialiser that would hide a naming mismatch.
	/// </summary>
	const string GeneratorDescriptor = /*lang=json,strict*/
		"""
		{"schemaVersion":1,"contexts":[{"name":"AppDbContext","typeName":"Sample.AppDbContext",
		"defaultSchema":"app","tables":[
		{"name":"Users","clrTypeName":"Sample.User","columns":[
		{"name":"Id","storeType":"int","clrTypeName":"int","isPrimaryKey":true},
		{"name":"Email","storeType":"varchar","clrTypeName":"string","isNullable":true,"isUnique":true,"maxLength":256}],
		"foreignKeys":[]},
		{"name":"Orders","clrTypeName":"Sample.Order","columns":[
		{"name":"Id","storeType":"int","isPrimaryKey":true},
		{"name":"UserId","storeType":"int","isForeignKey":true}],
		"foreignKeys":[{"principalTable":"Users","columns":["UserId"],"principalColumns":["Id"],
		"navigationName":"User"}]}]}]}
		""";

	// Helpers

	static AspireC4EfDiagramOptions CreateOptions() => new();

	/// <summary>Creates a throwaway project directory with a <c>bin</c> folder to lay outputs out in.</summary>
	static string CreateProject()
	{
		var root = Path.Combine(Path.GetTempPath(), $"aspirec4-{Guid.NewGuid():N}");
		var projectPath = Path.Combine(root, "Sample.csproj");

		Directory.CreateDirectory(Path.Combine(root, "bin"));
		File.WriteAllText(projectPath, "<Project />");

		return projectPath;
	}

	/// <summary>Copies an assembly into a directory with an explicit write time.</summary>
	static void WriteAssembly(string directory, string sourceAssembly, DateTime writtenUtc)
	{
		Directory.CreateDirectory(directory);

		var destination = Path.Combine(directory, Path.GetFileName(sourceAssembly));

		File.Copy(sourceAssembly, destination, overwrite: true);
		File.SetLastWriteTimeUtc(destination, writtenUtc);
	}

	// Tests

	[Test]
	public async Task Parse_GeneratorDescriptor_ReadsContextMetadata()
	{
		// Act
		var model = EfModelLoader.Parse(GeneratorDescriptor);

		// Assert
		await Assert.That(model.SchemaVersion).IsEqualTo(1);
		await Assert.That(model.Contexts.Count).IsEqualTo(1);
		await Assert.That(model.Contexts[0].Name).IsEqualTo("AppDbContext");
		await Assert.That(model.Contexts[0].DefaultSchema).IsEqualTo("app");
	}

	[Test]
	public async Task Parse_GeneratorDescriptor_ReadsColumnsAndFlags()
	{
		// Act
		var model = EfModelLoader.Parse(GeneratorDescriptor);

		// Assert
		var users = model.Contexts[0].Tables[0];
		await Assert.That(users.Name).IsEqualTo("Users");
		await Assert.That(users.Columns[0].IsPrimaryKey).IsTrue();
		await Assert.That(users.Columns[1].IsUnique).IsTrue();
		await Assert.That(users.Columns[1].IsNullable).IsTrue();
		await Assert.That(users.Columns[1].MaxLength).IsEqualTo(256);
	}

	[Test]
	public async Task Parse_GeneratorDescriptor_ReadsForeignKeys()
	{
		// Act
		var model = EfModelLoader.Parse(GeneratorDescriptor);

		// Assert
		var foreignKey = model.Contexts[0].Tables[1].ForeignKeys[0];
		await Assert.That(foreignKey.PrincipalTable).IsEqualTo("Users");
		await Assert.That(foreignKey.Columns[0]).IsEqualTo("UserId");
		await Assert.That(foreignKey.PrincipalColumns[0]).IsEqualTo("Id");
		await Assert.That(foreignKey.NavigationName).IsEqualTo("User");
	}

	[Test]
	public async Task Parse_GeneratorDescriptor_ProducesRenderableD2()
	{
		// The end-to-end contract: a generator descriptor must become a diagram with the expected
		// tables, constraints and column-level edge.
		// Act
		var dsl = D2DSLGenerator.Generate(
			D2ModelBuilder.Build(EfModelLoader.Parse(GeneratorDescriptor), CreateOptions()),
			CreateOptions()
		);

		// Assert
		await Assert.That(dsl).Contains("shape: sql_table");
		await Assert.That(dsl).Contains("Id: int {constraint: primary_key}");
		await Assert.That(dsl).Contains("Email: varchar(256)? {constraint: unique}");
		await Assert.That(dsl).Contains("UserId: int {constraint: foreign_key}");
		await Assert.That(dsl).Contains("app.Orders.UserId -> app.Users.Id: \"User\"");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task Parse_MissingDescriptor_ReturnsEmptyModel(string? descriptor)
	{
		// Act
		var model = EfModelLoader.Parse(descriptor);

		// Assert
		await Assert.That(model.Contexts).IsEmpty();
	}

	[Test]
	public async Task Parse_MalformedDescriptor_ReturnsEmptyModelRatherThanThrowing()
	{
		// A descriptor from a newer generator must degrade to an empty diagram, not fail startup.
		// Act
		var model = EfModelLoader.Parse("{ this is not json");

		// Assert
		await Assert.That(model.Contexts).IsEmpty();
	}

	[Test]
	public async Task Merge_SeveralModels_ConcatenatesContexts()
	{
		// Arrange
		var first = EfModelLoader.Parse(GeneratorDescriptor);
		var second = EfModelLoader.Parse(GeneratorDescriptor);

		// Act
		var merged = EfModelLoader.Merge([first, second]);

		// Assert
		await Assert.That(merged.Contexts.Count).IsEqualTo(2);
	}

	[Test]
	public async Task Merge_NoModels_ReturnsEmptyModel()
	{
		// Act
		var merged = EfModelLoader.Merge([]);

		// Assert
		await Assert.That(merged.Contexts).IsEmpty();
	}

	[Test]
	public async Task FindOutputDirectory_ProjectWithoutBinDirectory_ReturnsNull()
	{
		// Arrange
		var projectPath = Path.Combine(Path.GetTempPath(), $"aspirec4-{Guid.NewGuid():N}", "Sample.csproj");

		// Act
		var result = EfModelLoader.FindOutputDirectory(projectPath);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	public async Task FindOutputDirectory_MissingProjectPath_ReturnsNull(string? projectPath)
	{
		// Act
		var result = EfModelLoader.FindOutputDirectory(projectPath!);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task TryRead_MissingFile_ReturnsFalse()
	{
		// Arrange
		var path = Path.Combine(Path.GetTempPath(), $"aspirec4-{Guid.NewGuid():N}.dll");

		// Act
		var found = EfModelReader.TryRead(path, out var descriptor);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(descriptor).IsNull();
	}

	[Test]
	public async Task TryRead_NonManagedFile_ReturnsFalse()
	{
		// Arrange
		var path = Path.Combine(Path.GetTempPath(), $"aspirec4-{Guid.NewGuid():N}.dll");
		await File.WriteAllTextAsync(path, "not an assembly");

		try
		{
			// Act
			var found = EfModelReader.TryRead(path, out var descriptor);

			// Assert
			await Assert.That(found).IsFalse();
			await Assert.That(descriptor).IsNull();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task TryRead_AssemblyWithoutDescriptor_ReturnsFalse()
	{
		// Arrange — this test assembly carries no AspireC4 EF descriptor.
		var path = typeof(EfModelLoaderTests).Assembly.Location;

		// Act
		var found = EfModelReader.TryRead(path, out var descriptor);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(descriptor).IsNull();
	}

	[Test]
	public async Task LoadFromProject_ProjectWithoutBinDirectory_ReportsThatNothingWasSearched()
	{
		// Arrange
		var projectPath = Path.Combine(Path.GetTempPath(), $"aspirec4-{Guid.NewGuid():N}", "Sample.csproj");

		// Act
		var model = EfModelLoader.LoadFromProject(projectPath, out var diagnostics);

		// Assert
		await Assert.That(model.Contexts).IsEmpty();
		await Assert.That(diagnostics.ProjectPath).IsEqualTo(projectPath);
		await Assert.That(diagnostics.OutputDirectory).IsNull();
		await Assert.That(diagnostics.AssembliesScanned).IsEqualTo(0);
		await Assert.That(diagnostics.DescriptorsFound).IsEqualTo(0);
	}

	[Test]
	public async Task LoadFromProject_OutputWithoutDescriptors_ReportsHowMuchWasRead()
	{
		// A build output that exists but carries no model is exactly what a stale, or not yet
		// produced, build looks like — so the diagnostics have to say so rather than the caller
		// guessing.
		// Arrange
		var projectPath = CreateProject();
		var root = Path.GetDirectoryName(projectPath)!;
		var output = Path.Combine(root, "bin", "Debug", "net10.0");
		WriteAssembly(output, typeof(EfModelLoaderTests).Assembly.Location, DateTime.UtcNow);

		try
		{
			// Act
			var model = EfModelLoader.LoadFromProject(projectPath, out var diagnostics);

			// Assert
			await Assert.That(model.Contexts).IsEmpty();
			await Assert.That(diagnostics.OutputDirectory).IsEqualTo(output);
			await Assert.That(diagnostics.AssembliesScanned).IsEqualTo(1);
			await Assert.That(diagnostics.DescriptorsFound).IsEqualTo(0);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task LoadFromProject_OutputWithDescriptor_ReportsTheDescriptorItFound()
	{
		// Arrange
		var projectPath = CreateProject();
		var root = Path.GetDirectoryName(projectPath)!;
		var output = Path.Combine(root, "bin", "Debug", "net10.0");
		WriteAssembly(output, typeof(BlogDbContext).Assembly.Location, DateTime.UtcNow);

		try
		{
			// Act
			var model = EfModelLoader.LoadFromProject(projectPath, out var diagnostics);

			// Assert
			await Assert.That(model.Contexts).IsNotEmpty();
			await Assert.That(diagnostics.OutputDirectory).IsEqualTo(output);
			await Assert.That(diagnostics.DescriptorsFound).IsEqualTo(1);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task FindOutputDirectory_DescriptorInOlderDirectory_WinsOverANewerBuild()
	{
		// The newest assembly under bin is not always the project's own output — satellite resource
		// folders, ref assemblies and unrelated builds all live there — so a directory that actually
		// carries the model is preferred even when it is older.
		// Arrange
		var projectPath = CreateProject();
		var root = Path.GetDirectoryName(projectPath)!;
		var newer = Path.Combine(root, "bin", "Debug", "net10.0");
		var older = Path.Combine(root, "bin", "Release", "net10.0");
		WriteAssembly(newer, typeof(EfModelLoaderTests).Assembly.Location, DateTime.UtcNow);
		WriteAssembly(older, typeof(BlogDbContext).Assembly.Location, DateTime.UtcNow.AddDays(-1));

		try
		{
			// Act
			var result = EfModelLoader.FindOutputDirectory(projectPath);

			// Assert
			await Assert.That(result).IsEqualTo(older);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task FindOutputDirectory_CultureSatelliteFolder_IsNotTreatedAsBuildOutput()
	{
		// Satellite resource assemblies sit under a culture-named folder and are frequently the
		// newest files in bin; reading one yields an empty diagram.
		// Arrange
		var projectPath = CreateProject();
		var root = Path.GetDirectoryName(projectPath)!;
		var output = Path.Combine(root, "bin", "Debug", "net10.0");
		WriteAssembly(output, typeof(EfModelLoaderTests).Assembly.Location, DateTime.UtcNow.AddDays(-1));
		WriteAssembly(Path.Combine(output, "de"), typeof(EfModelLoaderTests).Assembly.Location, DateTime.UtcNow);

		try
		{
			// Act
			var result = EfModelLoader.FindOutputDirectory(projectPath);

			// Assert
			await Assert.That(result).IsEqualTo(output);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
