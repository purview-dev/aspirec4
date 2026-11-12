using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

/// <summary>
/// Proves that value objects, unions and the relationships built on them come out the same from both
/// static sources: the analyzer's descriptor and the metadata reader.
/// </summary>
/// <remarks>
/// One model is compiled once, with the EF Core stubs in an assembly of their own so it references
/// <c>Microsoft.EntityFrameworkCore</c> the way a real service does, and then read twice — through the
/// descriptor the generator baked into it, and through <see cref="EfModelMetadataReader"/>. Every test
/// runs against both, so the two sources cannot drift apart. No value-object generator runs here, so a
/// Purview <c>[Scalar&lt;T&gt;]</c> type has no <c>Value</c> member in either: the attribute alone has
/// to be enough, which is exactly the analyzer's situation in a real build.
/// </remarks>
public sealed class EfValueObjectRoundTripTests
	: TUnitSourceGeneratorTestBase<AspireC4EfModelGenerator, AspireC4EfModelGeneratorTestOptions>
{
	/// <summary>Which static source a test reads the model through.</summary>
	public enum Source
	{
		Descriptor,
		Metadata,
	}

	/// <summary>The Purview attributes and interface, plus the EF and runtime markers, matched by name.</summary>
	const string Markers = """
		namespace Purview.ValueObjects.Serialization
		{
			public enum EntityFrameworkMapping { ComplexType = 0, Json = 1, None = 2 }

			[System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class)]
			public sealed class ScalarAttribute : System.Attribute
			{
				public ScalarAttribute(string propertyName = "Value") { }

				public ScalarAttribute(System.Type valueType, string propertyName = "Value") { }
			}

			[System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class)]
			public sealed class ScalarAttribute<TValue> : System.Attribute
			{
				public ScalarAttribute(string propertyName = "Value") { }
			}

			[System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class)]
			public sealed class ValueObjectAttribute : System.Attribute
			{
				public EntityFrameworkMapping EFMapping { get; set; }
			}
		}

		namespace Purview.ValueObjects
		{
			public interface IScalarValueObject<TSelf, TValue> { TValue Value { get; } }
		}

		namespace Microsoft.EntityFrameworkCore
		{
			[System.AttributeUsage(System.AttributeTargets.Class)]
			public sealed class OwnedAttribute : System.Attribute { }
		}

		namespace System.Runtime.CompilerServices
		{
			[System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class)]
			public sealed class UnionAttribute : System.Attribute { }

			public interface IUnion { object? Value { get; } }
		}
		""";

	const string ModelSource = $$"""
		using System;
		using System.Collections.Generic;
		using System.Runtime.CompilerServices;
		using Microsoft.EntityFrameworkCore;
		using Purview.ValueObjects;
		using Purview.ValueObjects.Serialization;
		using TestAnnotations;

		{{Markers}}

		namespace Sample
		{
			// Purview's generic scalar: the generator writes Value, and nothing here can see it.
			[Scalar<Guid>]
			public readonly partial struct AssetId { }

			// A class-based scalar, which used to become a phantom table.
			[Scalar(typeof(int))]
			public sealed partial class TagId { }

			// The manual form: the author names the property that holds the value.
			[Scalar("Amount")]
			public readonly partial struct Money { public decimal Amount { get; init; } }

			// Shape alone, and the interface alone.
			public readonly struct LegacyId { public long Value { get; init; } }

			public readonly struct Code : IScalarValueObject<Code, string> { public string Value { get; init; } }

			public enum Status : byte { Active, Retired }

			// A bare [ValueObject] is ComplexType by Purview's own default, and holds a scalar value object.
			[ValueObject]
			public sealed record Address
			{
				public string Street { get; init; } = "";

				public string? PostCode { get; init; }

				public AssetId Landmark { get; init; }
			}

			[ValueObject(EFMapping = EntityFrameworkMapping.Json)]
			public sealed record Dimensions { public int Width { get; init; } public int Height { get; init; } }

			[System.ComponentModel.DataAnnotations.Schema.ComplexType]
			public sealed class Geo { public double Lat { get; set; } public double Lon { get; set; } }

			[Owned]
			public sealed class Audit { public DateTime CreatedAt { get; set; } public string CreatedBy { get; set; } = ""; }

			// A hand-written union: a Value typed object, which is exactly a scalar value object's shape.
			[Union]
			public readonly struct Reading : IUnion
			{
				readonly object? _value;

				public Reading(int count) => _value = count;

				public Reading(double measure) => _value = measure;

				public object? Value => _value;
			}

			public class Asset
			{
				public AssetId Id { get; set; }

				public AssetId? ReplacedBy { get; set; }

				public TagId PrimaryTag { get; set; } = null!;

				public Money Price { get; set; }

				public LegacyId Legacy { get; set; }

				public Code Code { get; set; }

				public Status Status { get; set; }

				public Address Location { get; set; } = null!;

				public Dimensions Size { get; set; } = null!;

				public Geo Position { get; set; } = null!;

				public Audit Audit { get; set; } = null!;

				public Reading Reading { get; set; }

				public List<string> Labels { get; set; } = [];

				public List<AssetId> Related { get; set; } = [];

				// Declared only on the principal: a matching foreign key property, and none at all.
				public ICollection<Part> Parts { get; set; } = [];

				public ICollection<Note> Notes { get; set; } = [];
			}

			public class Part { public int Id { get; set; } public AssetId AssetId { get; set; } }

			public class Note { public int Id { get; set; } public string Text { get; set; } = ""; }

			// A composite principal key, referenced through a single typed foreign key.
			public readonly struct OrderId { public int Value { get; init; } }

			public class Order
			{
				[Key]
				public OrderId Id { get; set; }

				[Key]
				public int Revision { get; set; }
			}

			public class Line
			{
				public int Id { get; set; }

				public OrderId OrderId { get; set; }

				public Order Order { get; set; } = null!;
			}

			public class AppDbContext : DbContext
			{
				public DbSet<Asset> Assets { get; set; }

				public DbSet<Order> Orders { get; set; }

				public DbSet<Line> Lines { get; set; }
			}
		}
		""";

	// Helpers

	/// <summary>Compiles <see cref="ModelSource"/> and reads the result through <paramref name="source"/>.</summary>
	async Task<IReadOnlyList<EfContext>> ReadAsync(Source source, CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(ModelSource, cancellationToken: cancellationToken);

		var directory = Directory.CreateTempSubdirectory("aspirec4-vo-").FullName;
		var modelPath = EmitSeparately(result.CompilationResult.Compilation, directory, cancellationToken);

		if (source == Source.Metadata)
		{
			return EfModelMetadataReader.ReadFromAssembly(modelPath);
		}

		await Assert.That(EfModelReader.TryRead(modelPath, out var descriptor)).IsTrue();

		return EfModelLoader.Parse(descriptor).Contexts;
	}

	/// <summary>
	/// Emits the EF Core stubs as their own <c>Microsoft.EntityFrameworkCore</c> assembly and the model
	/// against it, side by side, so the metadata reader finds the EF reference it gates on.
	/// </summary>
	static string EmitSeparately(Compilation compilation, string directory, CancellationToken cancellationToken)
	{
		// The stub source is the only tree that declares the annotations namespace; the model only uses it.
		var stubTree = compilation.SyntaxTrees.Single(tree =>
			tree.ToString().Contains("namespace TestAnnotations", StringComparison.Ordinal)
		);

		var stubs = CSharpCompilation.Create(
			"Microsoft.EntityFrameworkCore",
			[stubTree],
			compilation.References,
			new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary,
				nullableContextOptions: NullableContextOptions.Enable
			)
		);

		var stubPath = Path.Combine(directory, "Microsoft.EntityFrameworkCore.dll");
		Emit(stubs, stubPath, cancellationToken);

		var model = compilation
			.RemoveSyntaxTrees(stubTree)
			.AddReferences(MetadataReference.CreateFromFile(stubPath))
			.WithAssemblyName("Sample.Data");

		var modelPath = Path.Combine(directory, "Sample.Data.dll");
		Emit(model, modelPath, cancellationToken);

		return modelPath;
	}

	static void Emit(Compilation compilation, string path, CancellationToken cancellationToken)
	{
		using FileStream stream = new(path, FileMode.Create, FileAccess.Write);
		var emit = compilation.Emit(stream, cancellationToken: cancellationToken);

		if (!emit.Success)
		{
			throw new InvalidOperationException(
				$"Emit of {compilation.AssemblyName} failed: "
					+ string.Join("; ", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
			);
		}
	}

	static EfTable Table(IReadOnlyList<EfContext> contexts, string name) =>
		contexts.SelectMany(c => c.Tables).Single(t => t.Name == name);

	static EfColumn Column(IReadOnlyList<EfContext> contexts, string table, string column) =>
		Table(contexts, table).Columns.Single(c => c.Name == column);

	// Tests

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_PurviewGenericScalarKey_IsAValueObjectOfItsValueType(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// The generator writes Value in a file no other generator sees, so the shape rule found nothing
		// and the key — and every foreign key to it — disappeared.
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var id = Column(contexts, "Assets", "Id");
		await Assert.That(id.StoreType).IsEqualTo("guid");
		await Assert.That(id.DomainTypeName).IsEqualTo("AssetId");
		await Assert.That(id.DomainTypeKind).IsEqualTo(EfDomainTypeKind.ValueObject);
		await Assert.That(id.IsPrimaryKey).IsTrue();
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_ClassBasedScalar_IsAColumnRatherThanATable(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var tag = Column(contexts, "Assets", "PrimaryTag");
		await Assert.That(tag.StoreType).IsEqualTo("int");
		await Assert.That(tag.DomainTypeName).IsEqualTo("TagId");
		await Assert.That(contexts.SelectMany(c => c.Tables).Select(t => t.Name)).DoesNotContain("TagId");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_ScalarNamingItsOwnProperty_UsesThatPropertysType(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var price = Column(contexts, "Assets", "Price");
		await Assert.That(price.StoreType).IsEqualTo("decimal");
		await Assert.That(price.DomainTypeName).IsEqualTo("Money");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_ShapeAndInterfaceValueObjects_AreRecognised(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		await Assert.That(Column(contexts, "Assets", "Legacy").StoreType).IsEqualTo("bigint");
		await Assert.That(Column(contexts, "Assets", "Legacy").DomainTypeName).IsEqualTo("LegacyId");
		await Assert.That(Column(contexts, "Assets", "Code").StoreType).IsEqualTo("varchar");
		await Assert.That(Column(contexts, "Assets", "Code").DomainTypeName).IsEqualTo("Code");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_NullableValueObject_KeepsItsDomainType(Source source, CancellationToken cancellationToken)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var replacedBy = Column(contexts, "Assets", "ReplacedBy");
		await Assert.That(replacedBy.DomainTypeName).IsEqualTo("AssetId");
		await Assert.That(replacedBy.StoreType).IsEqualTo("guid");
		await Assert.That(replacedBy.IsNullable).IsTrue();
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_EnumWithAByteUnderlyingType_IsStoredAsItsUnderlyingType(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var status = Column(contexts, "Assets", "Status");
		await Assert.That(status.StoreType).IsEqualTo("tinyint");
		await Assert.That(status.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Enum);
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_BareValueObjectAttribute_IsFlattenedAndEveryMemberNamesItsOwner(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Purview defaults EFMapping to ComplexType, so a bare [ValueObject] is flattened — and its scalar
		// value-object member, which carries its own type, still says which property it belongs to.
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var members = Table(contexts, "Assets")
			.Columns.Where(c => c.Name.StartsWith("Location_", StringComparison.Ordinal))
			.ToList();
		await Assert
			.That(members.Select(c => c.Name))
			.IsEquivalentTo(["Location_Street", "Location_PostCode", "Location_Landmark"]);
		await Assert
			.That(members.All(c => c.ComplexPropertyName == "Location" && c.ComplexTypeName == "Address"))
			.IsTrue();

		var landmark = members.Single(c => c.Name == "Location_Landmark");
		await Assert.That(landmark.DomainTypeKind).IsEqualTo(EfDomainTypeKind.ValueObject);
		await Assert.That(landmark.DomainTypeName).IsEqualTo("AssetId");
		await Assert.That(members.Single(c => c.Name == "Location_PostCode").IsNullable).IsTrue();
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_JsonMappedValueObject_IsOneJsonColumn(Source source, CancellationToken cancellationToken)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var size = Column(contexts, "Assets", "Size");
		await Assert.That(size.StoreType).IsEqualTo("json");
		await Assert.That(size.DomainTypeName).IsEqualTo("Dimensions");
		await Assert.That(size.DomainTypeKind).IsEqualTo(EfDomainTypeKind.ComplexValueObject);
		await Assert.That(Table(contexts, "Assets").Columns.Select(c => c.Name)).DoesNotContain("Size_Width");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_EfComplexTypeAndOwnedAttributes_AreFlattenedIntoTheOwner(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var columns = Table(contexts, "Assets").Columns.Select(c => c.Name).ToList();
		await Assert.That(columns).Contains("Position_Lat");
		await Assert.That(columns).Contains("Audit_CreatedBy");
		await Assert.That(contexts.SelectMany(c => c.Tables).Select(t => t.Name)).DoesNotContain("Audit");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_UnionProperty_IsAUnionColumnNotAValueObject(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// A union's Value accessor is a scalar value object's shape, so without a union check first it
		// was shown as one, with no database type behind it.
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var reading = Column(contexts, "Assets", "Reading");
		await Assert.That(reading.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Union);
		await Assert.That(reading.DomainTypeName).IsEqualTo("Reading");
		await Assert.That(reading.StoreType).IsEqualTo("unknown");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_PrimitiveAndValueObjectCollections_AreJsonColumnsNotTables(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var labels = Column(contexts, "Assets", "Labels");
		await Assert.That(labels.StoreType).IsEqualTo("json");
		await Assert.That(labels.DomainTypeName).IsNull();

		var related = Column(contexts, "Assets", "Related");
		await Assert.That(related.StoreType).IsEqualTo("json");
		await Assert.That(related.DomainTypeName).IsEqualTo("AssetId[]");

		var tables = contexts.SelectMany(c => c.Tables).Select(t => t.Name).ToList();
		await Assert.That(tables).DoesNotContain("String");
		await Assert.That(tables).DoesNotContain("AssetId");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_RelationshipDeclaredOnlyOnThePrincipal_ResolvesTheForeignKey(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Asset.Parts with no Part.Asset navigation: EF finds Part.AssetId by name. It used to vanish,
		// because foreign keys were only looked for from a reference navigation on the dependent.
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var foreignKey = Table(contexts, "Part").ForeignKeys.Single();
		await Assert.That(foreignKey.PrincipalTable).IsEqualTo("Assets");
		await Assert.That(foreignKey.Columns).IsEquivalentTo(["AssetId"]);
		await Assert.That(foreignKey.PrincipalColumns).IsEquivalentTo(["Id"]);
		await Assert.That(foreignKey.PrincipalNavigationName).IsEqualTo("Parts");
		await Assert.That(Column(contexts, "Part", "AssetId").IsForeignKey).IsTrue();
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_PrincipalOnlyRelationshipWithNoForeignKeyProperty_ShowsTheShadowForeignKey(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// EF adds an AssetId column to Note even though the class declares none; the diagram shows the
		// column the database really has, typed like the key it references.
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var shadow = Column(contexts, "Note", "AssetId");
		await Assert.That(shadow.IsForeignKey).IsTrue();
		await Assert.That(shadow.IsNullable).IsTrue();
		await Assert.That(shadow.DomainTypeName).IsEqualTo("AssetId");
		await Assert.That(Table(contexts, "Note").ForeignKeys.Single().PrincipalTable).IsEqualTo("Assets");
	}

	[Test]
	[Arguments(Source.Descriptor)]
	[Arguments(Source.Metadata)]
	public async Task Read_SingleColumnForeignKeyIntoACompositeKey_PairsItWithTheKeyColumnOfItsType(
		Source source,
		CancellationToken cancellationToken
	)
	{
		// Attaching every key column gave a one-column reference two principal columns: d2 paired it with
		// whichever came first, and DBML could not parse it.
		// Act
		var contexts = await ReadAsync(source, cancellationToken);

		// Assert
		var foreignKey = Table(contexts, "Lines").ForeignKeys.Single();
		await Assert.That(foreignKey.Columns).IsEquivalentTo(["OrderId"]);
		await Assert.That(foreignKey.PrincipalColumns).IsEquivalentTo(["Id"]);
	}
}
