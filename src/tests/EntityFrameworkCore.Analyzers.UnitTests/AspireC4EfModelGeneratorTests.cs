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
		const string Source = "namespace Sample;";

		// Act
		using var result = await GenerateAsync(Source, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result.Generated()).HasGeneratedSyntaxTree("AspireC4EfModelAttribute.g.cs");
	}

	[Test]
	public async Task RunGenerator_WithNoDbContext_EmitsNoDescriptor(CancellationToken cancellationToken)
	{
		// Arrange
		const string Source = "namespace Sample; public class NotAContext { }";

		// Act
		var descriptor = await GenerateDescriptorAsync(Source, cancellationToken);

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
		const string Source = """
			using Microsoft.EntityFrameworkCore;

			namespace Sample;

			public class EmptyDbContext : DbContext { }
			""";

		// Act
		using var result = await GenerateAsync(Source, cancellationToken: cancellationToken);

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
