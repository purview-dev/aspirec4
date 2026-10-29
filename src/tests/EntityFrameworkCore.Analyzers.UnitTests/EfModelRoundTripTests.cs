using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.D2.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

/// <summary>
/// Proves the full loop: the generator emits a descriptor into a real compiled assembly, and the AppHost
/// reads it back out of that assembly's metadata and turns it into a d2 diagram.
/// </summary>
/// <remarks>
/// The two halves ship in separate packages and only meet through the emitted assembly attribute, so a
/// test that exercises one half against a hand-written fixture of the other would not catch a mismatch.
/// </remarks>
public sealed class EfModelRoundTripTests
	: TUnitSourceGeneratorTestBase<AspireC4EfModelGenerator, AspireC4EfModelGeneratorTestOptions>
{
	const string Source = """
		using System;
		using System.Collections.Generic;
		using Microsoft.EntityFrameworkCore;
		using TestAnnotations;

		namespace Sample;

		public class User
		{
			public int Id { get; set; }

			[MaxLength(256)]
			public string Email { get; set; }

			public ICollection<Order> Orders { get; set; }
		}

		public class Order
		{
			public int Id { get; set; }

			public int UserId { get; set; }

			public User User { get; set; }

			public decimal? Total { get; set; }
		}

		public class AppDbContext : DbContext
		{
			public DbSet<User> Users { get; set; }

			public DbSet<Order> Orders { get; set; }

			protected override void OnModelCreating(ModelBuilder modelBuilder)
			{
				modelBuilder.HasDefaultSchema("sales");
			}
		}
		""";

	// Helpers

	/// <summary>Runs the generator, emits the resulting compilation to a real assembly on disk.</summary>
	async Task<string> EmitAssemblyAsync(string source, CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		return EmitAssembly(result.CompilationResult.Compilation, cancellationToken);
	}

	static PortableExecutableReference CreateFriendAssemblyReference(
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		var attributeTree = compilation.SyntaxTrees.Single(tree =>
			Path.GetFileName(tree.FilePath) == "AspireC4EfModelAttribute.g.cs"
		);
		var friendTree = CSharpSyntaxTree.ParseText(
			"""
			[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Persistence.IntegrationTests")]
			""",
			(CSharpParseOptions)attributeTree.Options,
			cancellationToken: cancellationToken
		);
		var referencedCompilation = compilation
			.RemoveSyntaxTrees([
				.. compilation.SyntaxTrees.Where(tree =>
					tree != attributeTree && Path.GetFileName(tree.FilePath) != "EmbeddedAttribute.g.cs"
				),
			])
			.WithAssemblyName("Persistence")
			.AddSyntaxTrees(friendTree);
		using MemoryStream stream = new();
		var emit = referencedCompilation.Emit(stream, cancellationToken: cancellationToken);

		if (!emit.Success)
		{
			throw new InvalidOperationException(
				"Reference emit failed: " + string.Join("; ", emit.Diagnostics.Select(d => d.ToString()))
			);
		}

		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	static string EmitAssembly(Compilation compilation, CancellationToken cancellationToken)
	{
		var path = Path.Combine(Path.GetTempPath(), $"aspirec4-roundtrip-{Guid.NewGuid():N}.dll");

		using (FileStream stream = new(path, FileMode.Create, FileAccess.Write))
		{
			var emit = compilation.Emit(stream, cancellationToken: cancellationToken);

			if (!emit.Success)
			{
				throw new InvalidOperationException(
					"Emit failed: " + string.Join("; ", emit.Diagnostics.Select(d => d.ToString()))
				);
			}
		}

		return path;
	}

	/// <summary>
	/// A model whose entity holds a JSON column and an XML one — both framework classes the analyser has
	/// no store type for.
	/// </summary>
	const string FrameworkTypeSource = """
		using System;
		using System.Collections.Generic;
		using Microsoft.EntityFrameworkCore;
		using TestAnnotations;

		namespace Sample;

		public class Event
		{
			public int Id { get; set; }

			public System.Text.Json.JsonDocument Payload { get; set; }

			public System.Xml.Linq.XDocument Body { get; set; }
		}

		public class AppDbContext : DbContext
		{
			public DbSet<Event> Events { get; set; }
		}
		""";

	// Tests

	[Test]
	public async Task RoundTrip_ReferencedFriendAssemblyHasGeneratedAttribute_KeepsOwnDescriptorWithoutConflicts(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(Source, cancellationToken: cancellationToken);
		var compilation = result.CompilationResult.Compilation.WithAssemblyName("Persistence.IntegrationTests");
		var reference = CreateFriendAssemblyReference(compilation, cancellationToken);
		compilation = compilation
			.AddReferences(reference)
			.WithOptions(
				compilation.Options.WithSpecificDiagnosticOptions(
					compilation.Options.SpecificDiagnosticOptions.SetItem("CS0436", ReportDiagnostic.Error)
				)
			);

		// Act
		var conflicts = compilation.GetDiagnostics(cancellationToken).Where(d => d.Id == "CS0436").ToArray();

		// Assert
		await Assert.That(conflicts).IsEmpty();
		var attribute = compilation
			.Assembly.GetAttributes()
			.Single(a => a.AttributeClass?.Name == EfModelReader.AttributeName);
		await Assert
			.That(attribute.AttributeClass!.ContainingAssembly.Identity.Name)
			.IsEqualTo(compilation.AssemblyName);

		var path = EmitAssembly(compilation, cancellationToken);

		try
		{
			await Assert.That(EfModelReader.TryRead(path, out var descriptor)).IsTrue();
			await Assert.That(descriptor!).Contains("AppDbContext");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task RoundTrip_EntityHoldingFrameworkTypes_GainsNoTableForThem(CancellationToken cancellationToken)
	{
		// A class the analyser has no store type for used to be taken for a navigation, so the diagram
		// grew a table named after the framework type — "JsonDocument" — that is in nobody's model.
		// Asserted on the parsed tables rather than the descriptor text, because the CLR type name
		// legitimately appears there on the column itself.
		// Arrange
		var path = await EmitAssemblyAsync(FrameworkTypeSource, cancellationToken);

		try
		{
			EfModelReader.TryRead(path, out var descriptor);

			// Act
			var model = EfModelLoader.Parse(descriptor);

			// Assert
			var tables = model.Contexts[0].Tables.Select(t => t.Name).ToArray();

			await Assert.That(tables).IsEquivalentTo(["Events"]);
			await Assert.That(tables).DoesNotContain("JsonDocument");
			await Assert.That(tables).DoesNotContain("XDocument");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task RoundTrip_JsonDocumentProperty_BecomesAJsonColumn(CancellationToken cancellationToken)
	{
		// Not merely "no table": a JSON column is a real column and belongs in the diagram as one.
		// Arrange
		var path = await EmitAssemblyAsync(FrameworkTypeSource, cancellationToken);

		try
		{
			EfModelReader.TryRead(path, out var descriptor);

			// Act
			var model = EfModelLoader.Parse(descriptor);

			// Assert
			var payload = model.Contexts[0].Tables[0].Columns.FirstOrDefault(c => c.Name == "Payload");

			await Assert.That(payload).IsNotNull();
			await Assert.That(payload!.StoreType).IsEqualTo("json");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task RoundTrip_GeneratedAssembly_CarriesAReadableDescriptor(CancellationToken cancellationToken)
	{
		// Arrange
		var path = await EmitAssemblyAsync(Source, cancellationToken);

		try
		{
			// Act
			var found = EfModelReader.TryRead(path, out var descriptor);

			// Assert
			await Assert.That(found).IsTrue();
			await Assert.That(descriptor).IsNotNull();
			await Assert.That(descriptor!).Contains("AppDbContext");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task RoundTrip_DescriptorFromAssembly_ParsesIntoTheExpectedModel(CancellationToken cancellationToken)
	{
		// Arrange
		var path = await EmitAssemblyAsync(Source, cancellationToken);

		try
		{
			EfModelReader.TryRead(path, out var descriptor);

			// Act
			var model = EfModelLoader.Parse(descriptor);

			// Assert
			await Assert.That(model.Contexts.Count).IsEqualTo(1);
			await Assert.That(model.Contexts[0].Name).IsEqualTo("AppDbContext");
			await Assert.That(model.Contexts[0].DefaultSchema).IsEqualTo("sales");
			await Assert.That(model.Contexts[0].Tables.Select(t => t.Name)).Contains("Users");
			await Assert.That(model.Contexts[0].Tables.Select(t => t.Name)).Contains("Orders");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task RoundTrip_DescriptorFromAssembly_RendersAValidErDiagram(CancellationToken cancellationToken)
	{
		// Arrange
		var path = await EmitAssemblyAsync(Source, cancellationToken);
		AspireC4EfDiagramOptions options = new();

		try
		{
			EfModelReader.TryRead(path, out var descriptor);
			var model = EfModelLoader.Parse(descriptor);

			// Act
			var dsl = D2DSLGenerator.Generate(D2ModelBuilder.Build(model, options), options);

			// Assert
			await Assert.That(dsl).Contains("shape: sql_table");
			await Assert.That(dsl).Contains("layout-engine: elk");
			// The schema flows through to a d2 container, and the foreign key becomes a column edge.
			await Assert.That(dsl).Contains("sales: {");
			await Assert.That(dsl).Contains("sales.Orders.UserId -> sales.Users.Id");
			// The nullable decimal keeps its optionality marker.
			await Assert.That(dsl).Contains("Total: decimal?");
		}
		finally
		{
			File.Delete(path);
		}
	}
}
