using Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Helpers;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

public sealed class AspireC4EfModelGeneratorTests
	: TUnitSourceGeneratorTestBase<AspireC4EfModelGenerator, AspireC4EfModelGeneratorTestOptions>
{
	const string DescriptorHintName = "AspireC4EfModel.g.cs";

	// Helpers

	static string CreateContextSource(
		string entities = "public class User { public int Id { get; set; } public string Name { get; set; } }",
		string sets = "public DbSet<User> Users { get; set; }",
		string onModelCreating = ""
	) =>
		$$"""
			using System;
			using System.Collections.Generic;
			using Microsoft.EntityFrameworkCore;
			using TestAnnotations;

			namespace Sample;

			{{entities}}

			public class AppDbContext : DbContext
			{
				{{sets}}

				protected override void OnModelCreating(ModelBuilder modelBuilder)
				{
					{{onModelCreating}}
				}
			}
			""";

	async Task<string> GenerateDescriptorAsync(string source, CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		return result.GetSource(DescriptorHintName)?.ToString() ?? string.Empty;
	}

	// Tests

	[Test]
	public async Task RunGenerator_Always_EmitsTheModelAttributeType(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = "namespace Sample;";

		// Act
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("AspireC4EfModelAttribute.g.cs");
	}

	[Test]
	public async Task RunGenerator_ModelAttribute_IsEmbedded(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = "namespace Sample;";

		// Act
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);
		var type = result.CompilationResult.Compilation.Assembly.GetTypeByMetadataName(
			"Aspire.Hosting.AspireC4.EntityFrameworkCore.AspireC4EfModelAttribute"
		);

		// Assert
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("EmbeddedAttribute.g.cs");
		await Assert.That(type).IsNotNull();
		await Assert
			.That(type!.GetAttributes().Select(attribute => attribute.AttributeClass?.ToDisplayString()))
			.Contains("Microsoft.CodeAnalysis.EmbeddedAttribute");
	}

	[Test]
	public async Task RunGenerator_WithNoDbContext_EmitsNoDescriptor(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = "namespace Sample; public class NotAContext { }";

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).IsEmpty();
	}

	[Test]
	public async Task RunGenerator_WithDbContext_EmitsAssemblyDescriptor(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource();

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("AspireC4EfModelAttribute");
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"AppDbContext\\\"");
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task RunGenerator_PartialContextAcrossFiles_EmitsOneContextWithFluentConfiguration(
		bool repeatsBaseList,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string context = """
			using Microsoft.EntityFrameworkCore;
			namespace Sample;
			public class User { public int Id { get; set; } }
			public partial class AppDbContext : DbContext
			{
				public DbSet<User> Users { get; set; }
			}
			""";
		const string configuration = """
			using Microsoft.EntityFrameworkCore;
			namespace Sample;
			public partial class AppDbContext : DbContext
			{
				protected override void OnModelCreating(ModelBuilder modelBuilder)
				{
					modelBuilder.Entity<User>().ToTable("people", "hr");
				}
			}
			""";
		AspireC4EfModelGeneratorTestOptions options = new();
		options = options with
		{
			AdditionalSources = options.AdditionalSources.Add(
				repeatsBaseList ? configuration : configuration.Replace(" : DbContext", "", StringComparison.Ordinal)
			),
		};

		// Act
		using var result = await GenerateAsync(context, options, cancellationToken);
		var attribute = result
			.CompilationResult.Compilation.Assembly.GetAttributes()
			.Single(a => a.AttributeClass?.Name == "AspireC4EfModelAttribute");
		var model = EfModelLoader.Parse((string)attribute.ConstructorArguments[0].Value!);

		// Assert
		await Assert.That(model.Contexts.Count).IsEqualTo(1);
		await Assert.That(model.Contexts[0].Tables[0].Name).IsEqualTo("people");
		await Assert.That(model.Contexts[0].Tables[0].Schema).IsEqualTo("hr");
	}

	[Test]
	public async Task RunGenerator_WithDbSet_UsesSetNameAsTableName(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource();

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"Users\\\"");
	}

	[Test]
	public async Task RunGenerator_WithConventionalIdProperty_MarksItAsPrimaryKey(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource();

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("isPrimaryKey");
	}

	[Test]
	public async Task RunGenerator_WithKeyAttribute_MarksItAsPrimaryKey(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { [Key] public int Reference { get; set; } }",
			sets: "public DbSet<User> Users { get; set; }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"Reference\\\",\\\"storeType\\\":\\\"int\\\"");
		await Assert.That(descriptor).Contains("isPrimaryKey");
	}

	[Test]
	public async Task RunGenerator_WithTableAttribute_OverridesTableName(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "[Table(\"people\", Schema = \"hr\")] public class User { public int Id { get; set; } }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"people\\\"");
		await Assert.That(descriptor).Contains("\\\"schema\\\":\\\"hr\\\"");
	}

	[Test]
	public async Task RunGenerator_WithColumnAttribute_OverridesColumnNameAndType(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Id { get; set; } "
				+ "[Column(\"full_name\", TypeName = \"text\")] public string Name { get; set; } }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"full_name\\\",\\\"storeType\\\":\\\"text\\\"");
	}

	[Test]
	public async Task RunGenerator_WithNotMappedAttribute_SkipsTheColumn(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Id { get; set; } "
				+ "[NotMapped] public string Transient { get; set; } }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).DoesNotContain("Transient");
	}

	[Test]
	public async Task RunGenerator_WithMaxLengthAttribute_RecordsTheLength(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Id { get; set; } "
				+ "[MaxLength(256)] public string Email { get; set; } }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"maxLength\\\":256");
	}

	[Test]
	public async Task RunGenerator_WithNullableProperty_MarksColumnNullable(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Id { get; set; } public int? Age { get; set; } }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("isNullable");
	}

	[Test]
	public async Task RunGenerator_WithReferenceNavigation_ProducesForeignKey(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Id { get; set; } } "
				+ "public class Order { public int Id { get; set; } public int UserId { get; set; } "
				+ "public User User { get; set; } }",
			sets: "public DbSet<User> Users { get; set; } public DbSet<Order> Orders { get; set; }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("foreignKeys");
		await Assert.That(descriptor).Contains("\\\"principalTable\\\":\\\"Users\\\"");
		await Assert.That(descriptor).Contains("\\\"navigationName\\\":\\\"User\\\"");
	}

	[Test]
	public async Task RunGenerator_WithScalarValueObjectKey_ShowsTheValueObjectAndResolvesTheRelationship(
		CancellationToken cancellationToken
	)
	{
		// A strongly-typed identifier is a struct wrapping a Value member. Its primitive is only known
		// to a value converter, so the domain type is shown — and its foreign keys still resolve.
		// Arrange
		var source = CreateContextSource(
			entities: "public readonly record struct UserId { public int Value { get; init; } } "
				+ "public class User { public UserId Id { get; set; } } "
				+ "public class Order { public int Id { get; set; } public UserId UserId { get; set; } "
				+ "public User User { get; set; } }",
			sets: "public DbSet<User> Users { get; set; } public DbSet<Order> Orders { get; set; }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert — the domain type is beside the real database type, and the relationship still resolves.
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"Id\\\",\\\"storeType\\\":\\\"int\\\"");
		await Assert.That(descriptor).Contains("\\\"domainTypeName\\\":\\\"UserId\\\"");
		await Assert.That(descriptor).Contains("\\\"domainTypeKind\\\":\\\"ValueObject\\\"");
		await Assert.That(descriptor).Contains("\\\"principalTable\\\":\\\"Users\\\"");
	}

	const string InheritedSource = """
		using System;
		using System.Collections.Generic;
		using Microsoft.EntityFrameworkCore;

		namespace Sample;

		public class UserBase { public int Id { get; set; } public DateTime CreatedAt { get; set; } }

		public class User : UserBase { public string Name { get; set; } }

		public abstract class AppDbContextBase<TContext> : DbContext
			where TContext : DbContext
		{
			public DbSet<User> Users { get; set; }
		}

		public class AppDbContext : AppDbContextBase<AppDbContext> { }
		""";

	[Test]
	public async Task RunGenerator_WithDbSetOnABaseContext_StillDescribesItsTable(CancellationToken cancellationToken)
	{
		// Roslyn's GetMembers returns only what a type declares, so a context whose sets live on a shared
		// base would otherwise describe no tables at all.
		// Act
		var descriptor = await GenerateDescriptorAsync(InheritedSource, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"Users\\\"");
	}

	[Test]
	public async Task RunGenerator_WithPropertiesOnABaseEntity_DescribesThemAsColumns(
		CancellationToken cancellationToken
	)
	{
		// The key and the audit columns are usually declared once on a base entity; without them the table
		// has no primary key and nothing to relate.
		// Act
		var descriptor = await GenerateDescriptorAsync(InheritedSource, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"CreatedAt\\\"");
		await Assert.That(descriptor).Contains("\\\"isPrimaryKey\\\":true");
	}

	[Test]
	public async Task RunGenerator_WithAbstractGenericBaseContext_DescribesOnlyTheRealContext(
		CancellationToken cancellationToken
	)
	{
		// A base context is never instantiated and owns no model of its own, so describing it would add an
		// empty node to the diagram and report it as having no entity types.
		// Act
		var descriptor = await GenerateDescriptorAsync(InheritedSource, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"AppDbContext\\\"");
		await Assert.That(descriptor).DoesNotContain("AppDbContextBase");
	}

	const string SharedPrimaryKeyEntities =
		"public readonly record struct AssetId { public int Value { get; init; } } "
		+ "public class Asset { public AssetId Id { get; set; } public RepositoryState State { get; set; } } "
		+ "public class RepositoryState { public AssetId Id { get; set; } public Asset Asset { get; set; } }";

	const string SharedPrimaryKeySets =
		"public DbSet<Asset> Assets { get; set; } public DbSet<RepositoryState> RepositoryStates { get; set; }";

	[Test]
	public async Task RunGenerator_WithSharedPrimaryKey_ProducesForeignKeyFromTheKeyItself(
		CancellationToken cancellationToken
	)
	{
		// The dependent's key is the foreign key, so there is no <Navigation>Id property to match: without
		// recognising the borrowed identity the relationship is dropped altogether.
		// Arrange
		var source = CreateContextSource(entities: SharedPrimaryKeyEntities, sets: SharedPrimaryKeySets);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"principalTable\\\":\\\"Assets\\\"");
		await Assert.That(descriptor).Contains("\\\"navigationName\\\":\\\"Asset\\\"");
		await Assert.That(descriptor).Contains("\\\"principalNavigationName\\\":\\\"State\\\"");
	}

	[Test]
	public async Task RunGenerator_WithSharedPrimaryKeyAcrossEntitySuffixedTypes_ProducesForeignKey(
		CancellationToken cancellationToken
	)
	{
		// A persistence model names its types with an Entity suffix while its identifiers do not, so the
		// suffix has to be discounted before AssetId can be recognised as AssetEntity's own key.
		// Arrange
		var source = CreateContextSource(
			entities: "public readonly record struct AssetId { public int Value { get; init; } } "
				+ "public class AssetEntity { public AssetId Id { get; set; } "
				+ "public RepositoryStateEntity RepositoryState { get; set; } } "
				+ "public class RepositoryStateEntity { public AssetId Id { get; set; } "
				+ "public bool IsArchived { get; set; } public AssetEntity Asset { get; set; } }",
			sets: "public DbSet<AssetEntity> Assets { get; set; } "
				+ "public DbSet<RepositoryStateEntity> RepositoryStates { get; set; }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert — exactly one foreign key, declared by RepositoryStates and named from both ends
		await Assert.That(CountOccurrences(descriptor, "principalTable")).IsEqualTo(1);
		await Assert.That(descriptor).Contains("\\\"principalTable\\\":\\\"Assets\\\"");
		await Assert.That(descriptor).Contains("\\\"navigationName\\\":\\\"Asset\\\"");
		await Assert.That(descriptor).Contains("\\\"principalNavigationName\\\":\\\"RepositoryState\\\"");
	}

	[Test]
	public async Task RunGenerator_WithSharedPrimaryKey_DoesNotInvertTheRelationship(
		CancellationToken cancellationToken
	)
	{
		// Both ends are keyed by the same identifier, so the key types alone are symmetric; only the
		// principal owns that identity, and it must not also declare the foreign key.
		// Arrange
		var source = CreateContextSource(entities: SharedPrimaryKeyEntities, sets: SharedPrimaryKeySets);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert — exactly one foreign key, declared by RepositoryStates
		await Assert.That(CountOccurrences(descriptor, "principalTable")).IsEqualTo(1);
	}

	[Test]
	public async Task RunGenerator_WithEnumProperty_MarksItAsAnEnum(CancellationToken cancellationToken)
	{
		// An enum is a domain type too: the emitted descriptor carries its name and its kind, so the
		// AppHost can show the type and mark it.
		// Arrange
		var source = CreateContextSource(
			entities: "public enum Status { Draft, Active } "
				+ "public class User { public int Id { get; set; } public Status Status { get; set; } }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"Status\\\",\\\"storeType\\\":\\\"int\\\"");
		await Assert.That(descriptor).Contains("\\\"domainTypeName\\\":\\\"Status\\\"");
		await Assert.That(descriptor).Contains("\\\"domainTypeKind\\\":\\\"Enum\\\"");
	}

	[Test]
	public async Task RunGenerator_WithCollectionNavigation_DoesNotProduceForeignKeyOnPrincipal(
		CancellationToken cancellationToken
	)
	{
		// The foreign key belongs to the dependent, so the principal must not also declare it.
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Id { get; set; } public ICollection<Order> Orders { get; set; } } "
				+ "public class Order { public int Id { get; set; } public int UserId { get; set; } "
				+ "public User User { get; set; } }",
			sets: "public DbSet<User> Users { get; set; } public DbSet<Order> Orders { get; set; }"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert — exactly one foreign key, declared by Orders
		await Assert.That(CountOccurrences(descriptor, "principalTable")).IsEqualTo(1);
	}

	[Test]
	public async Task RunGenerator_WithFluentToTable_OverridesTableName(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(onModelCreating: "modelBuilder.Entity<User>().ToTable(\"people\", \"hr\");");

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"people\\\"");
		await Assert.That(descriptor).Contains("\\\"schema\\\":\\\"hr\\\"");
	}

	[Test]
	public async Task RunGenerator_WithFluentHasDefaultSchema_RecordsTheSchema(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(onModelCreating: "modelBuilder.HasDefaultSchema(\"app\");");

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"defaultSchema\\\":\\\"app\\\"");
	}

	[Test]
	public async Task RunGenerator_WithFluentHasColumnName_OverridesColumnName(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			onModelCreating: "modelBuilder.Entity<User>().Property(e => e.Name).HasColumnName(\"full_name\");"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"full_name\\\"");
	}

	[Test]
	public async Task RunGenerator_WithFluentIgnore_SkipsTheColumn(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(onModelCreating: "modelBuilder.Entity<User>().Ignore(e => e.Name);");

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).DoesNotContain("\\\"name\\\":\\\"Name\\\"");
	}

	[Test]
	public async Task RunGenerator_WithFluentHasKey_OverridesPrimaryKey(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public int Reference { get; set; } public string Name { get; set; } }",
			onModelCreating: "modelBuilder.Entity<User>().HasKey(e => e.Reference);"
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains("\\\"name\\\":\\\"Reference\\\"");
		await Assert.That(descriptor).Contains("isPrimaryKey");
	}

	[Test]
	public async Task RunGenerator_WithApplyConfigurationsFromAssembly_ReportsUninterpretableConfiguration(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateContextSource(onModelCreating: "modelBuilder.ApplyConfigurationsFromAssembly(null);");

		// Act
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UninterpretableConfiguration);
	}

	[Test]
	public async Task RunGenerator_WithEntityLackingAKey_ReportsNoPrimaryKeyDiscovered(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateContextSource(
			entities: "public class User { public string Name { get; set; } }",
			sets: "public DbSet<User> Users { get; set; }"
		);

		// Act
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NoPrimaryKeyDiscovered);
	}

	[Test]
	public async Task RunGenerator_WithContextHavingNoEntities_ReportsNoEntityTypesDiscovered(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Microsoft.EntityFrameworkCore;

			namespace Sample;

			public class EmptyDbContext : DbContext { }
			""";

		// Act
		using var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NoEntityTypesDiscovered);
	}

	static int CountOccurrences(string haystack, string needle)
	{
		var count = 0;
		var index = haystack.IndexOf(needle, StringComparison.Ordinal);

		while (index >= 0)
		{
			count++;
			index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
		}

		return count;
	}
}
