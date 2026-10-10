namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

/// <summary>
/// Options used by every EF model generator test. Seeds minimal EF Core and data-annotation stubs so the
/// sampled sources compile without taking a real <c>Microsoft.EntityFrameworkCore</c> dependency.
/// </summary>
/// <remarks>
/// The generator matches contexts by base-type display string and attributes by class name, so these
/// stubs only need the right names and shapes — not EF's real behaviour.
/// </remarks>
public sealed record AspireC4EfModelGeneratorTestOptions : SourceGeneratorTestOptions
{
	public AspireC4EfModelGeneratorTestOptions() => AdditionalSources = AdditionalSources.Add(EfStubSource);

	/// <summary>Minimal EF Core surface plus the data annotations the analyser reads.</summary>
	internal const string EfStubSource = """
		using System;
		using System.Collections.Generic;
		using System.Linq.Expressions;

		namespace Microsoft.EntityFrameworkCore
		{
			public class DbContext
			{
				protected virtual void OnModelCreating(ModelBuilder modelBuilder) { }
			}

			// The type parameter is named TEntity to match the real EF Core declaration. An earlier
			// stub used T, which let a display-string match pass here while failing against real EF.
			public class DbSet<TEntity> where TEntity : class { }

			public class ModelBuilder
			{
				public EntityTypeBuilder<T> Entity<T>() where T : class => new EntityTypeBuilder<T>();

				public ModelBuilder HasDefaultSchema(string schema) => this;

				public ModelBuilder ApplyConfigurationsFromAssembly(object assembly) => this;
			}

			public class EntityTypeBuilder<T> where T : class
			{
				public EntityTypeBuilder<T> ToTable(string name) => this;

				public EntityTypeBuilder<T> ToTable(string name, string schema) => this;

				public EntityTypeBuilder<T> HasKey(Expression<Func<T, object>> expression) => this;

				public EntityTypeBuilder<T> Ignore(Expression<Func<T, object>> expression) => this;

				public IndexBuilder HasIndex(Expression<Func<T, object>> expression) => new IndexBuilder();

				public PropertyBuilder Property(Expression<Func<T, object>> expression) => new PropertyBuilder();
			}

			public class IndexBuilder
			{
				public IndexBuilder IsUnique() => this;
			}

			public class PropertyBuilder
			{
				public PropertyBuilder HasColumnName(string name) => this;

				public PropertyBuilder HasColumnType(string type) => this;

				public PropertyBuilder HasMaxLength(int length) => this;

				public PropertyBuilder IsRequired(bool required = true) => this;
			}

			[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
			public sealed class IndexAttribute : Attribute
			{
				public IndexAttribute(params string[] propertyNames) => PropertyNames = propertyNames;

				public string[] PropertyNames { get; }

				public bool IsUnique { get; set; }
			}
		}

		namespace TestAnnotations
		{
			[AttributeUsage(AttributeTargets.Property)]
			public sealed class KeyAttribute : Attribute { }

			[AttributeUsage(AttributeTargets.Property)]
			public sealed class RequiredAttribute : Attribute { }

			[AttributeUsage(AttributeTargets.Property)]
			public sealed class NotMappedAttribute : Attribute { }

			[AttributeUsage(AttributeTargets.Property)]
			public sealed class MaxLengthAttribute : Attribute
			{
				public MaxLengthAttribute(int length) => Length = length;

				public int Length { get; }
			}

			[AttributeUsage(AttributeTargets.Property)]
			public sealed class ForeignKeyAttribute : Attribute
			{
				public ForeignKeyAttribute(string name) => Name = name;

				public string Name { get; }
			}

			[AttributeUsage(AttributeTargets.Property)]
			public sealed class ColumnAttribute : Attribute
			{
				public ColumnAttribute(string name) => Name = name;

				public string Name { get; }

				public string TypeName { get; set; }
			}

			[AttributeUsage(AttributeTargets.Class)]
			public sealed class TableAttribute : Attribute
			{
				public TableAttribute(string name) => Name = name;

				public string Name { get; }

				public string Schema { get; set; }
			}
		}
		""";
}
