using Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

/// <summary>
/// A description must not depend on whether the service project generates a documentation file: when
/// it does not, Roslyn lexes <c>///</c> as ordinary comments and hands back no documentation, and the
/// text has to be recovered from the syntax instead.
/// </summary>
public sealed class DocumentationTests
{
	// Helpers

	const string Source = """
		namespace Sample;

		/// <summary>
		/// Someone who can sign in.
		/// </summary>
		public class User
		{
			/// <summary>What the user is called.</summary>
			public string Name { get; set; }
		}
		""";

	static INamedTypeSymbol CreateUserType(DocumentationMode mode)
	{
		var tree = CSharpSyntaxTree.ParseText(Source, new CSharpParseOptions(documentationMode: mode));
		var compilation = CSharpCompilation.Create(
			"Documented",
			[tree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		return compilation.GetTypeByMetadataName("Sample.User")!;
	}

	// Tests

	[Test]
	[Arguments(DocumentationMode.None)]
	[Arguments(DocumentationMode.Parse)]
	[Arguments(DocumentationMode.Diagnose)]
	public async Task OfType_InEveryDocumentationMode_ReadsTheSummary(
		DocumentationMode mode,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var user = CreateUserType(mode);

		// Act
		var summary = Documentation.OfType(user, cancellationToken);

		// Assert
		await Assert.That(summary).IsEqualTo("Someone who can sign in.");
	}

	[Test]
	[Arguments(DocumentationMode.None)]
	[Arguments(DocumentationMode.Parse)]
	public async Task OfProperty_InEveryDocumentationMode_ReadsTheSummary(
		DocumentationMode mode,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var name = CreateUserType(mode).GetMembers("Name").OfType<IPropertySymbol>().Single();

		// Act
		var summary = Documentation.OfProperty(name, cancellationToken);

		// Assert
		await Assert.That(summary).IsEqualTo("What the user is called.");
	}
}
