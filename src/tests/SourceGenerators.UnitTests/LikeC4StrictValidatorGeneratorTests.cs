using System.Collections.Immutable;
using System.Globalization;
using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

public sealed class LikeC4StrictValidatorGeneratorTests : TUnitSourceGeneratorTestBase<LikeC4StrictValidatorGenerator>
{
	[Test]
	public async Task RunGenerator_Always_InjectsLikeC4RegistryAttributes(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = "namespace TestApp;";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — the marker attribute types are always emitted as post-initialization output
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("LikeC4RegistryAttribute.g.cs");
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("KnownTypeAttribute.g.cs");
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("SeverityAttribute.g.cs");
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("LikeC4RegistryType.g.cs");
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("LikeC4Severity.g.cs");
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — DSL strict mode (ASPIREC4001 / ASPIREC4002)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithStrictModeAndDeclaredTag_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string dsl = """
			specification {
			  tag external
			}
			""";
		var source = BuildSourceWithCallSites(".WithTag(\"external\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: "warning",
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndUndeclaredTag_EmitsUndeclaredTagDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string dsl = """
			specification {
			  tag existing-tag
			}
			""";
		var source = BuildSourceWithCallSites(".WithTag(\"unknown-tag\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: "error",
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("unknown-tag");
	}

	[Test]
	public async Task RunGenerator_WithStrictModeDisabledAndUndeclaredTag_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — strict mode is off even though there are DSL files
		const string dsl = "specification { tag existing-tag }";
		var source = BuildSourceWithCallSites(".WithTag(\"unknown-tag\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: "off",
			cancellationToken: cancellationToken
		);

		// Assert — no validation without strict mode
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndNoDSLFiles_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange — strict mode is on but no DSL additional files are provided
		var source = BuildSourceWithCallSites(".WithTag(\"any-value\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [],
			strictMode: "all",
			cancellationToken: cancellationToken
		);

		// Assert — no DSL definitions = nothing to validate against
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndDeclaredKind_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string dsl = "specification { element service }";
		var source = BuildSourceWithCallSites(".WithKind(\"service\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: "all",
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredKind);
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndUndeclaredKind_EmitsUndeclaredKindDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			}
			""";
		var source = BuildSourceWithCallSites(".WithKind(\"unknown-kind\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: "all",
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredKind);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("unknown-kind");
	}

	[Test]
	public async Task RunGenerator_WithStrictModeAndRelationshipKindDeclared_EmitsNoDiagnosticForWithKind(
		CancellationToken cancellationToken
	)
	{
		// Arrange — relationship kinds are also valid for WithKind()
		const string dsl = """
			specification {
			  relationship async
			}
			""";
		var source = BuildSourceWithCallSites(".WithKind(\"async\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: "all",
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredKind);
	}

	[Test]
	public async Task RunGenerator_WithConstReferenceToValidTag_EmitsNoDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange — const reference should be resolved to its value
		const string dsl = "specification { tag my-tag }";
		var source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;
			class Setup
			{
			    const string MyTag = "my-tag";
			    static object Create(object a) => a;
			    static void Configure()
			    {
			        var a = Create(null);
			        a.WithTag(MyTag);
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: "all",
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — class-based mode (ASPIREC4001 / ASPIREC4002)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndValidTag_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithTag(\"external\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndUndeclaredTag_EmitsUndeclaredTagDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithTag(\"unknown\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("unknown");
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndValidElementKind_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			elementKindConstants: [("Service", "service")],
			callSites: [".WithKind(\"service\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredKind);
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndValidRelationshipKind_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			relationshipKindConstants: [("Async", "async")],
			callSites: [".WithKind(\"async\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredKind);
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassAndUndeclaredKind_EmitsUndeclaredKindDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			elementKindConstants: [("Container", "container")],
			callSites: [".WithKind(\"unknown-kind\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredKind);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("unknown-kind");
	}

	[Test]
	public async Task RunGenerator_WithPrivateNestedDefinitionsClass_DiscoverDefinitions(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [LikeC4Registry] class can be private and/or nested
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("MyTag", "my-tag")],
			callSites: [".WithTag(\"my-tag\")"],
			classAccessibility: "private"
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — multiple [LikeC4Registry] classes (ASPIREC4003)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithMultipleDefinitionsClasses_EmitsMultipleDefinitionsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — two [LikeC4Registry] classes in the same assembly
		var source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			class FirstDefinitions
			{
			    public static class Tags { public const string External = "external"; }
			}

			[LikeC4Registry]
			class SecondDefinitions
			{
			    public static class Tags { public const string Internal = "internal"; }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.MultipleRegistryClassesDefined);
	}

	[Test]
	public async Task RunGenerator_WithNoDefinitionsAndNoStrictMode_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithCallSites(".WithTag(\"anything\")", ".WithKind(\"anything\")");

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — no validation active when no definitions are present
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredKind);
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — group validation (ASPIREC4004)
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithDefinitionsClassGroupsAndDeclaredGroup_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			groupConstants: [("Frontend", "Frontend")],
			callSites: [".WithLikeC4Group(\"Frontend\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredGroup);
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassGroupsAndUndeclaredGroup_EmitsUndeclaredGroupDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			groupConstants: [("Frontend", "Frontend")],
			callSites: [".WithLikeC4Group(\"Backend\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredGroup);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("Backend");
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassGroupsAndCaseVariant_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — group comparison is case-insensitive (OrdinalIgnoreCase), so "frontend" matches "Frontend"
		var source = BuildSourceWithDefinitionsClass(
			groupConstants: [("Frontend", "Frontend")],
			callSites: [".WithLikeC4Group(\"frontend\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredGroup);
	}

	[Test]
	public async Task RunGenerator_WithDefinitionsClassWithoutGroupsNestedClass_EmitsNoDiagnosticForGroup(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [LikeC4Registry] exists but has no Groups nested class → no group validation
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithLikeC4Group(\"anything\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — group validation is opt-in via declaring a Groups nested class
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredGroup);
	}

	[Test]
	public async Task RunGenerator_WithNoDefinitionsClassAndGroupCallSite_EmitsNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — no [LikeC4Registry] at all
		var source = BuildSourceWithCallSites(".WithLikeC4Group(\"Frontend\")");

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredGroup);
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — [KnownType] attribute on individual constants
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithKnownTypeAttributeOnTopLevelField_ValidatesCallSite(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag)]
			    public const string External = "external";
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithTag("unknown-tag");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("unknown-tag");
	}

	[Test]
	public async Task RunGenerator_WithKnownTypeAttributeOnTopLevelField_AllowsDeclaredValue(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag)]
			    public const string External = "external";
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithTag("external");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithDuplicateTypeDeclaration_EmitsDuplicateTypeDeclarationDiagnostic(
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

			class Setup
			{
			    static void Configure() { }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.DuplicateTypeDeclaration);
	}

	[Test]
	public async Task RunGenerator_WithKnownTypeGroupField_ValidatesGroupCallSite(CancellationToken cancellationToken)
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
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredGroup);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("Backend");
	}

	[Test]
	public async Task RunGenerator_WithKnownTypeStrictDisable_DoesNotValidateTypeEvenWithDeclaredValues(
		CancellationToken cancellationToken
	)
	{
		// Arrange — Tag has Strict = Disable, so no validation fires
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag, Strict = LikeC4Severity.Off)]
			    public const string External = "external";
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithTag("anything-undeclared");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — strict disabled for tags, so no diagnostic
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithRegistryStrictEnable_ValidatesEvenWithEmptyAllowedSet(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [LikeC4Registry(Strict = Enable)] with no Tags declared → all tags are undeclared
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
			        a.WithTag("any-tag");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — strict enabled at registry level, no tags declared → fires
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("any-tag");
	}

	// -----------------------------------------------------------------------
	// Full generator pipeline — MetadataKeys nested class
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithMetadataKeysNestedClass_ExtractsWithoutError(CancellationToken cancellationToken)
	{
		// Arrange
		var source = BuildSourceWithDefinitionsClass(
			metadataKeyConstants: [("AzureSku", "Azure SKU"), ("UseCase", "Use Case")],
			callSites: []
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — no diagnostics from having a MetadataKeys nested class
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredMetadataKey);
	}

	[Test]
	public async Task RunGenerator_WithMetadataCallSite_ExactMatch_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange — registry declares "Azure_SKU"; call site uses the exact same value
		var source = BuildSourceWithDefinitionsClass(
			metadataKeyConstants: [("AzureSku", "Azure_SKU")],
			callSites: [".WithMetadata(\"Azure_SKU\", \"Standard_LRS\")"]
		);

		// Act — AllIncludingMetadata enables metadata key validation
		var result = await RunGeneratorAsync(
			source,
			strictMode: "AllIncludingMetadata",
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredMetadataKey);
	}

	[Test]
	public async Task RunGenerator_WithMetadataCallSite_SpaceVariant_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange — registry declares "Azure_SKU"; call site uses "azure sku" (space instead of underscore)
		var source = BuildSourceWithDefinitionsClass(
			metadataKeyConstants: [("AzureSku", "Azure_SKU")],
			callSites: [".WithMetadata(\"azure sku\", \"Standard_LRS\")"]
		);

		// Act — AllIncludingMetadata enables metadata key validation
		var result = await RunGeneratorAsync(
			source,
			strictMode: "AllIncludingMetadata",
			cancellationToken: cancellationToken
		);

		// Assert — "azure sku" normalises to "azure_sku" which matches "Azure_SKU" case-insensitively
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredMetadataKey);
	}

	[Test]
	public async Task RunGenerator_WithMetadataCallSite_CaseVariant_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange — registry declares "Azure_SKU"; call site uses "AZURE_sku"
		var source = BuildSourceWithDefinitionsClass(
			metadataKeyConstants: [("AzureSku", "Azure_SKU")],
			callSites: [".WithMetadata(\"AZURE_sku\", \"Standard_LRS\")"]
		);

		// Act — AllIncludingMetadata enables metadata key validation
		var result = await RunGeneratorAsync(
			source,
			strictMode: "AllIncludingMetadata",
			cancellationToken: cancellationToken
		);

		// Assert — OrdinalIgnoreCase handles the case difference
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredMetadataKey);
	}

	[Test]
	public async Task RunGenerator_WithMetadataCallSite_RegistryKeyHasSpaces_CallSiteHasUnderscores_EmitsNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Arrange — registry declares "Azure SKU" (with space); call site uses "Azure_SKU"
		var source = BuildSourceWithDefinitionsClass(
			metadataKeyConstants: [("AzureSku", "Azure SKU")],
			callSites: [".WithMetadata(\"Azure_SKU\", \"Standard_LRS\")"]
		);

		// Act — AllIncludingMetadata enables metadata key validation
		var result = await RunGeneratorAsync(
			source,
			strictMode: "AllIncludingMetadata",
			cancellationToken: cancellationToken
		);

		// Assert — "Azure SKU" normalises to "Azure_SKU" in the registry set
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredMetadataKey);
	}

	[Test]
	public async Task RunGenerator_WithMetadataCallSite_UndeclaredKey_EmitsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// Arrange — registry declares "Azure_SKU" only; call site uses an unknown key
		var source = BuildSourceWithDefinitionsClass(
			metadataKeyConstants: [("AzureSku", "Azure_SKU")],
			callSites: [".WithMetadata(\"unknown_key\", \"value\")"]
		);

		// Act — AllIncludingMetadata enables metadata key validation
		var result = await RunGeneratorAsync(
			source,
			strictMode: "AllIncludingMetadata",
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredMetadataKey);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("unknown_key");
	}

	// -----------------------------------------------------------------------
	// NormaliseMetadataKeyForComparison — direct unit tests
	// -----------------------------------------------------------------------

	[Test]
	public async Task NormaliseMetadataKeyForComparison_SpaceReplacedWithUnderscore()
	{
		// Arrange / Act
		var result = LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison("Azure SKU");

		// Assert
		await Assert.That(result).IsEqualTo("Azure_SKU");
	}

	[Test]
	public async Task NormaliseMetadataKeyForComparison_AlreadyValidKey_Unchanged()
	{
		// Arrange / Act
		var result = LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison("Azure_SKU");

		// Assert
		await Assert.That(result).IsEqualTo("Azure_SKU");
	}

	[Test]
	public async Task NormaliseMetadataKeyForComparison_HyphenPreserved()
	{
		// Arrange / Act
		var result = LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison("azure-sku");

		// Assert
		await Assert.That(result).IsEqualTo("azure-sku");
	}

	[Test]
	public async Task NormaliseMetadataKeyForComparison_MultiplePunctuation_AllReplacedWithUnderscore()
	{
		// Arrange / Act
		var result = LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison("Azure.SKU/Tier");

		// Assert
		await Assert.That(result).IsEqualTo("Azure_SKU_Tier");
	}

	[Test]
	public async Task NormaliseMetadataKeyForComparison_EmptyString_ReturnsEmpty()
	{
		// Arrange / Act
		var result = LikeC4StrictValidatorGenerator.NormaliseMetadataKeyForComparison(string.Empty);

		// Assert
		await Assert.That(result).IsEqualTo(string.Empty);
	}

	// -----------------------------------------------------------------------
	// DisableAspireC4SourceGenerator MSBuild property
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithDisabledPropertyTrue_EmitsNoDiagnosticsEvenWithUndeclaredValues(
		CancellationToken cancellationToken
	)
	{
		// Arrange — declared registry has "valid-tag" only; call site uses "undeclared-tag"
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("ValidTag", "valid-tag")],
			callSites: [".WithTag(\"undeclared-tag\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, disabled: true, cancellationToken: cancellationToken);

		// Assert — generator disabled, so no ASPIREC4001 should fire
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UndeclaredTag);
	}

	[Test]
	public async Task RunGenerator_WithDisabledPropertyFalse_EmitsDiagnosticsAsNormal(
		CancellationToken cancellationToken
	)
	{
		// Arrange — declared registry has "valid-tag" only; call site uses "undeclared-tag"
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("ValidTag", "valid-tag")],
			callSites: [".WithTag(\"undeclared-tag\")"]
		);

		// Act — disabled=false is the default; verification that normal validation still runs
		var result = await RunGeneratorAsync(source, disabled: false, cancellationToken: cancellationToken);

		// Assert — validation is active, undeclared tag triggers ASPIREC4001
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("undeclared-tag");
	}

	// -----------------------------------------------------------------------
	// Diagnostic severity escalation — warning vs error based on strict mode
	// -----------------------------------------------------------------------

	[Test]
	public async Task RunGenerator_WithRegistryClassAndNoStrictMode_EmitsSuggestionSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — no explicit strict mode; class-based validation uses suggestion severity by default
		var source = BuildSourceWithDefinitionsClass(
			tagsConstants: [("External", "external")],
			callSites: [".WithTag(\"unknown\")"]
		);

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Info);
	}

	[Test]
	public async Task RunGenerator_WithDSLWarningModeAndUndeclaredTag_EmitsWarningSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — AspireC4Strict=warning emits warnings for DSL validation
		const string dsl = """
			specification {
			  tag existing-tag
			}
			""";
		var source = BuildSourceWithCallSites(".WithTag(\"unknown-tag\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: "warning",
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
	}

	[Test]
	public async Task RunGenerator_WithDSLErrorModeAndUndeclaredTag_EmitsErrorSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — AspireC4Strict=error escalates to Error
		const string dsl = """
			specification {
			  tag existing-tag
			}
			""";
		var source = BuildSourceWithCallSites(".WithTag(\"unknown-tag\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("model.c4", dsl)],
			strictMode: "error",
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task RunGenerator_WithDSLWarningModeAndUndeclaredKind_EmitsWarningSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string dsl = """
			specification {
			  element container
			}
			""";
		var source = BuildSourceWithCallSites(".WithKind(\"unknown-kind\")");

		// Act
		var result = await RunGeneratorAsync(
			source,
			additionalFiles: [new TestAdditionalText("spec.c4", dsl)],
			strictMode: "warning",
			cancellationToken: cancellationToken
		);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredKind);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
	}

	[Test]
	public async Task RunGenerator_WithRegistryStrictEnableAndUndeclaredTag_EmitsErrorSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [LikeC4Registry(Strict = Enable)] escalates all undeclared-tag diagnostics to Error
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry(Strict = LikeC4Severity.Error)]
			static class MyRegistry
			{
			    public static class Tags { public const string External = "external"; }
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithTag("undeclared");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task RunGenerator_WithRegistryStrictEnableAndUndeclaredKind_EmitsErrorSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry(Strict = LikeC4Severity.Error)]
			static class MyRegistry
			{
			    public static class ElementKinds { public const string Container = "container"; }
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithKind("undeclared-kind");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredKind);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task RunGenerator_WithRegistryStrictEnableAndUndeclaredGroup_EmitsErrorSeverity(
		CancellationToken cancellationToken
	)
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
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredGroup);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task RunGenerator_WithKnownTypeStrictEnableAndUndeclaredTag_EmitsErrorSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [KnownType(Tag, Strict = Enable)] escalates only the tag type to Error
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag, Strict = LikeC4Severity.Error)]
			    public const string External = "external";
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithTag("undeclared");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert
		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task RunGenerator_WithNestedTypeSeverity_OverridesRegistrySeverity(CancellationToken cancellationToken)
	{
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [Severity(LikeC4Severity.Error)]
			    public static class Tags { public const string External = "external"; }
			}

			class Setup
			{
			    static void Configure()
			    {
			        var value = new object();
			        value.WithTag("undeclared");
			    }
			}
			""";

		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		var diagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task RunGenerator_WithKnownTypeStrictEnableOnTagsOnly_OtherTypesRemainsSuggestionSeverity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — only Tags has Strict=Enable; Kind diagnostics should remain Suggestion
		const string source = """
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			[LikeC4Registry]
			static class MyRegistry
			{
			    [KnownType(LikeC4RegistryType.Tag, Strict = LikeC4Severity.Error)]
			    public const string External = "external";

			    public static class ElementKinds { public const string Container = "container"; }
			}

			class Setup
			{
			    static void Configure()
			    {
			        var a = new object();
			        a.WithTag("undeclared-tag");
			        a.WithKind("undeclared-kind");
			    }
			}
			""";

		// Act
		var result = await RunGeneratorAsync(source, cancellationToken: cancellationToken);

		// Assert — tags are errors, kinds remain suggestions
		var tagDiagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredTag);
		await Assert.That(tagDiagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);

		var kindDiagnostic = await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UndeclaredKind);
		await Assert.That(kindDiagnostic.Severity).IsEqualTo(DiagnosticSeverity.Info);
	}

	async Task<DriverRunResult> RunGeneratorAsync(
		string source,
		AdditionalText[]? additionalFiles = null,
		string? strictMode = null,
		bool disabled = false,
		CancellationToken cancellationToken = default
	)
	{
		Dictionary<string, string> analyzerConfigOptions = [];
		if (strictMode is not null)
			analyzerConfigOptions[PropertyLibrary.AspireC4Strict] = strictMode;

		return await GenerateAsync(
			source,
			new LikeC4StrictValidatorGeneratorTestOptions
			{
				DisableSourceGeneratorValue = disabled,
				AnalyzerConfigOptions = analyzerConfigOptions.ToImmutableDictionary(),
				AdditionalText = additionalFiles?.ToImmutableArray() ?? [],
			},
			cancellationToken
		);
	}

	// -----------------------------------------------------------------------
	// Test infrastructure — AdditionalText
	// -----------------------------------------------------------------------

	sealed class TestAdditionalText(string path, string content) : AdditionalText
	{
		public override string Path { get; } = path;

		public override SourceText? GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
	}

	static string BuildSourceWithCallSites(params string[] invocations)
	{
		var body = string.Join(
			"\n        ",
			invocations.Select(static (inv, i) => $"var a{i} = new object(); a{i}{inv};")
		);

		return $$"""
			using Aspire.Hosting.AspireC4;
			namespace TestApp;
			class Setup
			{
			    static void Configure()
			    {
			        {{body}}
			    }
			}
			""";
	}

	static string BuildSourceWithDefinitionsClass(
		(string Name, string Value)[]? tagsConstants = null,
		(string Name, string Value)[]? elementKindConstants = null,
		(string Name, string Value)[]? relationshipKindConstants = null,
		(string Name, string Value)[]? groupConstants = null,
		(string Name, string Value)[]? metadataKeyConstants = null,
		string[]? callSites = null,
		string classAccessibility = "internal"
	)
	{
		static string BuildNestedClass(string className, (string Name, string Value)[]? constants)
		{
			if (constants is null || constants.Length == 0)
				return string.Empty;

			var fields = string.Join(
				"\n            ",
				constants.Select(static c => $"public const string {c.Name} = \"{c.Value}\";")
			);
			return $$"""
				public static class {{className}}
				{
					{{fields}}
				}
				""";
		}

		var tagsClass = BuildNestedClass("Tags", tagsConstants);
		var elementKindsClass = BuildNestedClass("ElementKinds", elementKindConstants);
		var relationshipKindsClass = BuildNestedClass("RelationshipKinds", relationshipKindConstants);
		var groupsClass = BuildNestedClass("Groups", groupConstants);
		var metadataKeysClass = BuildNestedClass("MetadataKeys", metadataKeyConstants);

		var body = callSites is null
			? string.Empty
			: string.Join("\n        ", callSites.Select(static (inv, i) => $"var a{i} = new object(); a{i}{inv};"));

		return $$"""
			using Aspire.Hosting.AspireC4;
			namespace TestApp;

			class DefinitionsContainer
			{
			[LikeC4Registry]
			{{classAccessibility}} class MyDiagramDefinitions
			{
			{{tagsClass}}
			{{elementKindsClass}}
			{{relationshipKindsClass}}
			{{groupsClass}}
			{{metadataKeysClass}}
			}
			}

			class Setup
			{
			    static void Configure()
			    {
			        {{body}}
			    }
			}
			""";
	}
}
