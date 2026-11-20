namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers;

/// <summary>
/// The descriptor carries each table's and column's description sources separately — the display
/// attributes, the XML documentation summary and the database comment — so the AppHost can choose
/// between them without the analyzer knowing its options.
/// </summary>
public sealed partial class AspireC4EfModelGeneratorTests
{
	// Helpers

	/// <summary>A property and its value as they appear inside the descriptor's C# string literal.</summary>
	static string Field(string name, string value) => $"\\\"{name}\\\":\\\"{value}\\\"";

	// Tests

	[Test]
	public async Task RunGenerator_DescriptionAttribute_IsRecordedOnTableAndColumn(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: """
			[Description("Someone who can sign in")]
			public class User
			{
				public int Id { get; set; }

				[Description("The name shown to other users")]
				public string Name { get; set; }
			}
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("description", "Someone who can sign in"));
		await Assert.That(descriptor).Contains(Field("description", "The name shown to other users"));
	}

	[Test]
	public async Task RunGenerator_DisplayDescription_IsRecordedWhenNoDescriptionAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateContextSource(
			entities: """
			public class User
			{
				public int Id { get; set; }

				[Display(Name = "Full name", Description = "As printed on the account")]
				public string Name { get; set; }
			}
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("description", "As printed on the account"));
	}

	[Test]
	public async Task RunGenerator_DescriptionAttribute_WinsOverDisplayAndDisplayName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = CreateContextSource(
			entities: """
			public class User
			{
				public int Id { get; set; }

				[DisplayName("Name")]
				[Display(Description = "From Display")]
				[Description("From Description")]
				public string Name { get; set; }
			}
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("description", "From Description"));
		await Assert.That(descriptor).DoesNotContain("From Display");
	}

	[Test]
	public async Task RunGenerator_DisplayName_IsTheLastResort(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: """
			public class User
			{
				public int Id { get; set; }

				[DisplayName("Account holder")]
				public string Name { get; set; }
			}
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("description", "Account holder"));
	}

	[Test]
	public async Task RunGenerator_XmlSummary_IsRecordedAsPlainText(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: """
			/// <summary>
			/// Someone who can sign in to the <see cref="AppDbContext"/>.
			/// </summary>
			public class User
			{
				public int Id { get; set; }

				/// <summary>The   name shown to <c>other</c> users.</summary>
				public string Name { get; set; }
			}
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("documentation", "Someone who can sign in to the AppDbContext."));
		await Assert.That(descriptor).Contains(Field("documentation", "The name shown to other users."));
	}

	[Test]
	public async Task RunGenerator_PositionalRecordProperty_TakesItsParamDocumentation(
		CancellationToken cancellationToken
	)
	{
		// A positional record's properties are documented on the record, not on themselves.
		// Arrange
		var source = CreateContextSource(
			entities: """
			/// <summary>A user.</summary>
			/// <param name="Id">The key.</param>
			/// <param name="Name">What the user is called.</param>
			public record User(int Id, string Name);
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("documentation", "What the user is called."));
	}

	[Test]
	public async Task RunGenerator_ClassCommentAttribute_IsTheTableComment(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			entities: """
			[Comment("Everyone with an account")]
			public class User { public int Id { get; set; } }
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).Contains(Field("comment", "Everyone with an account"));
	}

	[Test]
	public async Task RunGenerator_EntityHasComment_IsTheTableCommentNotAColumns(CancellationToken cancellationToken)
	{
		// Arrange
		var source = CreateContextSource(
			onModelCreating: """
			modelBuilder.Entity<User>().HasComment("Everyone with an account");
			modelBuilder.Entity<User>().Property(u => u.Name).HasComment("Their display name");
			"""
		);

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert — the table's comment follows the table's own type name; the column's stays on the column.
		await Assert
			.That(descriptor)
			.Contains($"{Field("clrTypeName", "Sample.User")},{Field("comment", "Everyone with an account")}");
		await Assert.That(descriptor).Contains(Field("comment", "Their display name"));
	}

	[Test]
	public async Task RunGenerator_NoDescriptionSource_EmitsNoDescriptionFields(CancellationToken cancellationToken)
	{
		// A model with nothing to describe is byte-for-byte what it was before descriptions existed.
		// Arrange
		var source = CreateContextSource();

		// Act
		var descriptor = await GenerateDescriptorAsync(source, cancellationToken);

		// Assert
		await Assert.That(descriptor).DoesNotContain("description");
		await Assert.That(descriptor).DoesNotContain("documentation");
	}
}
