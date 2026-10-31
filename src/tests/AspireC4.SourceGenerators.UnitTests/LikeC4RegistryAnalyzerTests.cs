using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

public sealed class LikeC4RegistryAnalyzerTests
{
	const string AttributeSource = """
		namespace Aspire.Hosting.AspireC4
		{
		    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
		    internal sealed class LikeC4RegistryAttribute : System.Attribute
		    {
		        public LikeC4Severity Strict { get; init; } = LikeC4Severity.Inherit;
		    }
		    internal enum LikeC4RegistryType { Tag = 0, ElementKind = 1, RelationshipKind = 2, Group = 3, MetadataKey = 4 }
		    internal enum LikeC4Severity { Inherit = 0, Off = 1, Suggestion = 2, Warning = 3, Error = 4 }
		    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
		    internal sealed class KnownTypeAttribute : System.Attribute
		    {
		        public KnownTypeAttribute(LikeC4RegistryType type) { Type = type; }
		        public LikeC4RegistryType Type { get; }
		        public LikeC4Severity Strict { get; init; } = LikeC4Severity.Inherit;
		    }
		}
		""";

	// -----------------------------------------------------------------------
	// Multiple [LikeC4Registry] classes (ASPIREC4003)
	// -----------------------------------------------------------------------

	[Test]
	public async Task Analyze_WithMultipleDefinitionsClasses_EmitsMultipleDefinitionsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class FirstDefinitions { public static class Tags { public const string External = "external"; } }

			[LikeC4Registry]
			class SecondDefinitions { public static class Tags { public const string Internal = "internal"; } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		var result = GetDiagnostics(diagnostics, "ASPIREC4003");
		await Assert.That(result.Count).IsGreaterThan(0);
	}

	[Test]
	public async Task Analyze_WithSingleDefinitionsClass_EmitsNoMultipleDefinitionsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class OnlyDefinitions { public static class Tags { public const string External = "external"; } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4003")).IsEmpty();
	}

	// -----------------------------------------------------------------------
	// Group validation (ASPIREC4004)
	// -----------------------------------------------------------------------

	[Test]
	public async Task Analyze_WithDeclaredGroup_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class MyRegistry
			{
			    public static class Groups { public const string Frontend = "Frontend"; }
			}

			class Setup { static void Configure() { var a = new object(); a.WithLikeC4Group("Frontend"); } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithUndeclaredGroup_EmitsUndeclaredGroupDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class MyRegistry
			{
			    public static class Groups { public const string Frontend = "Frontend"; }
			}

			class Setup { static void Configure() { var a = new object(); a.WithLikeC4Group("Backend"); } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		var result = GetDiagnostics(diagnostics, "ASPIREC4004");
		await Assert.That(result.Count).IsGreaterThan(0);
		await Assert.That(result[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Backend");
	}

	[Test]
	public async Task Analyze_WithGroupCaseVariant_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange — group comparison is case-insensitive
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class MyRegistry
			{
			    public static class Groups { public const string Frontend = "Frontend"; }
			}

			class Setup { static void Configure() { var a = new object(); a.WithLikeC4Group("frontend"); } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithoutGroupsNestedClass_EmitsNoDiagnosticForGroup(CancellationToken cancellationToken)
	{
		// Arrange — registry exists but has no Groups nested class → no group validation
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class MyRegistry { public static class Tags { public const string External = "external"; } }

			class Setup { static void Configure() { var a = new object(); a.WithLikeC4Group("anything"); } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithNoDefinitionsClassAndGroupCallSite_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace TestApp;
			class Setup { static void Configure() { var a = new object(); a.WithLikeC4Group("Frontend"); } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4004")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithKnownTypeGroupField_ValidatesGroupCallSite(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Group)]
			    public const string Frontend = "Frontend";
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithLikeC4Group("Backend");
			    }
			}
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		var result = GetDiagnostics(diagnostics, "ASPIREC4004");
		await Assert.That(result.Count).IsGreaterThan(0);
		await Assert.That(result[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Backend");
	}

	// -----------------------------------------------------------------------
	// Duplicate type declaration (ASPIREC4005)
	// -----------------------------------------------------------------------

	[Test]
	public async Task Analyze_WithDuplicateTypeDeclaration_EmitsDuplicateTypeDeclarationDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — Tags declared both as nested class AND via [KnownType] on a field
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag)]
			    public const string InlineTag = "inline-tag";

			    public static class Tags
			    {
			        public const string External = "external";
			    }
			}

			class Setup { static void Configure() { } }
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		var result = GetDiagnostics(diagnostics, "ASPIREC4005");
		await Assert.That(result.Count).IsGreaterThan(0);
	}

	// -----------------------------------------------------------------------
	// MetadataKeys (ASPIREC4006)
	// -----------------------------------------------------------------------

	static string MetadataSource(string keyValue, string callSite) =>
		$$"""
		using Aspire.Hosting.AspireC4;
		namespace TestApp;

		[LikeC4Registry]
		class MyRegistry
		{
		    public static class MetadataKeys { public const string AzureSku = "{{keyValue}}"; }
		}

		class Setup { static void Configure() { var a = new object(); a{{callSite}}; } }
		""";

	[Test]
	public async Task Analyze_WithMetadataKeysNestedClass_ExtractsWithoutError(CancellationToken cancellationToken)
	{
		// Arrange — no call sites, just a MetadataKeys nested class
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class MyRegistry { public static class MetadataKeys { public const string AzureSku = "Azure SKU"; } }

			class Setup { static void Configure() { } }
			""";

		// Act — AllIncludingMetadata enables metadata key validation
		var diagnostics = RunAnalyzer(source, strictMode: "AllIncludingMetadata", cancellationToken: cancellationToken);

		// Assert — no diagnostics from having a MetadataKeys nested class
		await Assert.That(GetAspireDiagnostics(diagnostics)).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithMetadataCallSite_ExactMatch_EmitsNoDiagnostics(CancellationToken cancellationToken)
	{
		// Arrange
		var source = MetadataSource("Azure_SKU", ".WithMetadata(\"Azure_SKU\", \"Standard_LRS\")");

		// Act
		var diagnostics = RunAnalyzer(source, strictMode: "AllIncludingMetadata", cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4006")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithMetadataCallSite_SpaceVariant_EmitsNoDiagnostics(CancellationToken cancellationToken)
	{
		// Arrange — call site uses "azure sku" (space instead of underscore)
		var source = MetadataSource("Azure_SKU", ".WithMetadata(\"azure sku\", \"Standard_LRS\")");

		// Act
		var diagnostics = RunAnalyzer(source, strictMode: "AllIncludingMetadata", cancellationToken: cancellationToken);

		// Assert — normalises to the same key
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4006")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithMetadataCallSite_CaseVariant_EmitsNoDiagnostics(CancellationToken cancellationToken)
	{
		// Arrange
		var source = MetadataSource("Azure_SKU", ".WithMetadata(\"AZURE_sku\", \"Standard_LRS\")");

		// Act
		var diagnostics = RunAnalyzer(source, strictMode: "AllIncludingMetadata", cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4006")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithMetadataCallSite_RegistryKeyHasSpaces_CallSiteHasUnderscores_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange — registry declares "Azure SKU" (with space); call site uses "Azure_SKU"
		var source = MetadataSource("Azure SKU", ".WithMetadata(\"Azure_SKU\", \"Standard_LRS\")");

		// Act
		var diagnostics = RunAnalyzer(source, strictMode: "AllIncludingMetadata", cancellationToken: cancellationToken);

		// Assert
		await Assert.That(GetDiagnostics(diagnostics, "ASPIREC4006")).IsEmpty();
	}

	[Test]
	public async Task Analyze_WithMetadataCallSite_UndeclaredKey_EmitsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		var source = MetadataSource("Azure_SKU", ".WithMetadata(\"unknown_key\", \"value\")");

		// Act
		var diagnostics = RunAnalyzer(source, strictMode: "AllIncludingMetadata", cancellationToken: cancellationToken);

		// Assert
		var aspireDiags = GetDiagnostics(diagnostics, "ASPIREC4006");
		await Assert.That(aspireDiags.Count).IsEqualTo(1);
		await Assert.That(aspireDiags[0].GetMessage(CultureInfo.InvariantCulture)).Contains("unknown_key");
	}

	// -----------------------------------------------------------------------
	// Severity escalation
	// -----------------------------------------------------------------------

	[Test]
	public async Task Analyze_WithRegistryStrictEnableAndUndeclaredGroup_EmitsErrorSeverity(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry(Strict = LikeC4Severity.Error)]
			static class MyRegistry
			{
			    public static class Groups { public const string Frontend = "Frontend"; }
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithLikeC4Group("Backend");
			    }
			}
			""";

		// Act
		var diagnostics = RunAnalyzer(source, cancellationToken: cancellationToken);

		// Assert
		var result = GetDiagnostics(diagnostics, "ASPIREC4004");
		await Assert.That(result.Count).IsGreaterThan(0);
		await Assert.That(result[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	// -----------------------------------------------------------------------
	// DisableAspireC4SourceGenerator MSBuild property
	// -----------------------------------------------------------------------

	[Test]
	public async Task Analyze_WithDisabledPropertyTrue_EmitsNoDiagnostics(CancellationToken cancellationToken)
	{
		// Arrange — undeclared group and metadata, duplicate declaration, two registries
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class FirstRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag)]
			    public const string InlineTag = "inline-tag";

			    public static class Tags { public const string External = "external"; }
			}

			[LikeC4Registry]
			static class SecondRegistry
			{
			    public static class Groups { public const string Frontend = "Frontend"; }
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithLikeC4Group("Backend");
			        a.WithMetadata("unknown_key", "value");
			    }
			}
			""";

		// Act
		var diagnostics = RunAnalyzer(source, disabled: true, cancellationToken: cancellationToken);

		// Assert — analyzer disabled, so no ASPIREC4 diagnostics
		await Assert.That(GetAspireDiagnostics(diagnostics)).IsEmpty();
	}

	// -----------------------------------------------------------------------

	static ImmutableArray<Diagnostic> RunAnalyzer(
		string source,
		string? strictMode = null,
		bool disabled = false,
		CancellationToken cancellationToken = default
	)
	{
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[
				CSharpSyntaxTree.ParseText(AttributeSource, cancellationToken: cancellationToken),
				CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken),
			],
			GetMetadataReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var buildProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (strictMode is not null)
			buildProperties["build_property.AspireC4Strict"] = strictMode;
		if (disabled)
			buildProperties["build_property.DisableAspireC4SourceGenerator"] = "true";

		AnalyzerOptions? options =
			buildProperties.Count > 0
				? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new TestAnalyzerConfigOptionsProvider(buildProperties))
				: null;

		var withAnalyzers = compilation.WithAnalyzers(
			ImmutableArray.Create<DiagnosticAnalyzer>(new LikeC4RegistryAnalyzer()),
			options
		);

		return withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).GetAwaiter().GetResult();
	}

	static IEnumerable<MetadataReference> GetMetadataReferences() =>
	[
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location),
	];

	static IReadOnlyList<Diagnostic> GetDiagnostics(ImmutableArray<Diagnostic> diagnostics, string diagnosticId) =>
		[.. diagnostics.Where(d => d.Id == diagnosticId)];

	static IReadOnlyList<Diagnostic> GetAspireDiagnostics(ImmutableArray<Diagnostic> diagnostics) =>
		[.. diagnostics.Where(d => d.Id.StartsWith("ASPIREC4", StringComparison.Ordinal))];

	// -----------------------------------------------------------------------
	// Test infrastructure — AnalyzerConfigOptionsProvider
	// -----------------------------------------------------------------------

	sealed class TestAnalyzerConfigOptionsProvider(Dictionary<string, string> options) : AnalyzerConfigOptionsProvider
	{
		readonly TestAnalyzerConfigOptions _options = new(options);

		public override AnalyzerConfigOptions GlobalOptions => _options;

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
	}

	sealed class TestAnalyzerConfigOptions(Dictionary<string, string> options) : AnalyzerConfigOptions
	{
		public override bool TryGetValue(string requestedKey, [NotNullWhen(true)] out string? optionValue) =>
			options.TryGetValue(requestedKey, out optionValue);
	}
}
