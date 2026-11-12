using Aspire.Hosting.AspireC4.EntityFrameworkCore.ModelDump;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// The dump projector's handling of value objects, owned types, JSON mapping and column types — the
/// cases where the real model knows more than either static source, and so must not show less.
/// </summary>
public sealed partial class EfModelDumpProjectorTests
{
	// Helpers

	static EfModel ProjectValueObjects()
	{
		using DumpValueObjectDbContext context = new(
			new DbContextOptionsBuilder<DumpValueObjectDbContext>().UseSqlite("Data Source=:memory:").Options
		);

		var json = EfModelDumpProjector.Project(
			context.GetService<IDesignTimeModel>().Model,
			nameof(DumpValueObjectDbContext),
			typeof(DumpValueObjectDbContext).FullName!
		);

		return EfModelLoader.Parse(json);
	}

	static EfColumn AssetColumn(string name) =>
		Table(ProjectValueObjects(), "DumpAssets").Columns.Single(c => c.Name == name);

	// Tests

	[Test]
	public async Task Project_NullableValueObject_KeepsItsDomainType()
	{
		// Nullable<T> is generic, so the value-object rule rejected it and the column showed a bare guid —
		// the most complete source losing what the weaker two keep.
		// Act
		var replacedBy = AssetColumn("ReplacedBy");

		// Assert
		await Assert.That(replacedBy.DomainTypeName).IsEqualTo("DumpOrderId");
		await Assert.That(replacedBy.DomainTypeKind).IsEqualTo(EfDomainTypeKind.ValueObject);
		await Assert.That(replacedBy.StoreType).IsEqualTo("guid");
		await Assert.That(replacedBy.IsNullable).IsTrue();
	}

	[Test]
	public async Task Project_EnumConvertedToString_IsStoredAsAStringAndStillMarked()
	{
		// Act
		var status = AssetColumn("Status");

		// Assert
		await Assert.That(status.StoreType).IsEqualTo("varchar");
		await Assert.That(status.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Enum);
	}

	[Test]
	public async Task Project_ExplicitColumnType_IsShownAsConfigured()
	{
		// Act
		var price = AssetColumn("Price");

		// Assert
		await Assert.That(price.StoreType).IsEqualTo("decimal(18,4)");
	}

	[Test]
	public async Task Project_ComplexMembers_NameTheirOwningProperty()
	{
		// Act
		var city = Table(Project(), "DumpCustomers")
			.Columns.Single(c => c.Name.Contains("City", StringComparison.Ordinal));

		// Assert
		await Assert.That(city.ComplexPropertyName).IsEqualTo("Address");
		await Assert.That(city.ComplexTypeName).IsEqualTo("DumpAddress");
	}

	[Test]
	public async Task Project_OwnedTypeSharingTheTable_IsFlattenedIntoItsOwner()
	{
		// Owned types used to be skipped outright, so their columns vanished from the owner's table.
		// Act
		var model = ProjectValueObjects();
		var createdBy = Table(model, "DumpAssets").Columns.Single(c => c.Name == "Audit_CreatedBy");

		// Assert
		await Assert.That(createdBy.ComplexPropertyName).IsEqualTo("Audit");
		await Assert.That(createdBy.ComplexTypeName).IsEqualTo("DumpAudit");
		await Assert.That(model.Contexts.SelectMany(c => c.Tables).Select(t => t.Name)).DoesNotContain("DumpAudit");
	}

	[Test]
	public async Task Project_OwnedTypeMappedToJson_IsOneJsonColumn()
	{
		// Act
		var dimensions = AssetColumn("Dimensions");

		// Assert
		await Assert.That(dimensions.StoreType).IsEqualTo("json");
		await Assert.That(dimensions.DomainTypeName).IsEqualTo("DumpDimensions");
		await Assert.That(dimensions.DomainTypeKind).IsEqualTo(EfDomainTypeKind.ComplexValueObject);
	}

	[Test]
	public async Task Project_OwnedCollection_IsATableOfItsOwn()
	{
		// OwnsMany has a table, and a foreign key to its owner; skipping every owned type hid both.
		// Act
		var lines = Table(ProjectValueObjects(), "DumpAssetLine");

		// Assert
		await Assert.That(lines.ForeignKeys.Single().PrincipalTable).IsEqualTo("DumpAssets");
	}

	[Test]
	public async Task Project_PrimitiveCollection_IsAJsonColumn()
	{
		// Act
		var labels = AssetColumn("Labels");

		// Assert
		await Assert.That(labels.StoreType).IsEqualTo("json");
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task Project_UnionWithAConverter_IsAUnionColumnOfTheConvertedType()
	{
		// EF needs a converter to store a union, so the database type is whatever it produces; the
		// union's Value accessor must not be mistaken for a scalar value object's.
		// Act
		var reading = AssetColumn("Reading");

		// Assert
		await Assert.That(reading.DomainTypeKind).IsEqualTo(EfDomainTypeKind.Union);
		await Assert.That(reading.DomainTypeName).IsEqualTo("DumpReading");
		await Assert.That(reading.StoreType).IsEqualTo("varchar");
	}
#endif
}

/// <summary>An owned type sharing its owner's table.</summary>
public sealed class DumpAudit
{
	public DateTimeOffset CreatedAt { get; set; }

	public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>An owned type mapped to a JSON column.</summary>
public sealed class DumpDimensions
{
	public int Width { get; set; }

	public int Height { get; set; }
}

/// <summary>An owned collection, which EF gives a table of its own.</summary>
public sealed class DumpAssetLine
{
	public string Description { get; set; } = string.Empty;
}

#if NET11_0_OR_GREATER
/// <summary>A hand-written union of a count and a label, using the .NET 11 runtime's union types.</summary>
[System.Runtime.CompilerServices.Union]
public readonly struct DumpReading : System.Runtime.CompilerServices.IUnion
{
	readonly object? _value;

	public DumpReading(int count) => _value = count;

	public DumpReading(string label) => _value = label;

	public object? Value => _value;
}
#endif

public sealed class DumpAsset
{
	public DumpOrderId Id { get; set; }

	public DumpOrderId? ReplacedBy { get; set; }

	public DumpStatus Status { get; set; }

	public decimal Price { get; set; }

	public DumpAudit Audit { get; set; } = new();

	public DumpDimensions Dimensions { get; set; } = new();

	public List<DumpAssetLine> Lines { get; set; } = [];

	public List<string> Labels { get; set; } = [];

#if NET11_0_OR_GREATER
	public DumpReading Reading { get; set; }
#endif
}

/// <summary>Value objects, owned types, JSON mapping and explicit column types, in one real model.</summary>
public sealed class DumpValueObjectDbContext(DbContextOptions<DumpValueObjectDbContext> options) : DbContext(options)
{
	public DbSet<DumpAsset> DumpAssets => Set<DumpAsset>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ValueConverter<DumpOrderId, Guid> orderId = new(value => value.Value, raw => new DumpOrderId { Value = raw });

		modelBuilder.Entity<DumpAsset>(builder =>
		{
			builder.HasKey(a => a.Id);
			builder.Property(a => a.Id).HasConversion(orderId);
			builder.Property(a => a.ReplacedBy).HasConversion(orderId);
			builder.Property(a => a.Status).HasConversion<string>();
			builder.Property(a => a.Price).HasColumnType("decimal(18,4)");
			builder.OwnsOne(a => a.Audit);
			builder.OwnsOne(a => a.Dimensions, owned => owned.ToJson());
			builder.OwnsMany(a => a.Lines);
#if NET11_0_OR_GREATER
			builder
				.Property(a => a.Reading)
				.HasConversion(value => value.Value!.ToString()!, raw => new DumpReading(raw));
#endif
		});
	}
}
