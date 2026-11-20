namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// A diagram cell holds one line of plain text, so a summary has its markup resolved and its wrapping
/// collapsed. All three discovery paths compile this same source, so it is proven once here.
/// </summary>
public sealed class XmlDocumentationTextTests
{
	// Tests

	[Test]
	public async Task Summary_MemberElement_ReturnsItsSummary()
	{
		// Arrange
		const string xml =
			"""<member name="T:Shop.Order"><summary>An order.</summary><remarks>Ignored.</remarks></member>""";

		// Act
		var summary = XmlDocumentationText.Summary(xml);

		// Assert
		await Assert.That(summary).IsEqualTo("An order.");
	}

	[Test]
	public async Task Summary_BareCommentContent_ReturnsItsSummary()
	{
		// Arrange
		const string xml = """
			 <summary>
			 An order a customer has placed.
			 </summary>
			""";

		// Act
		var summary = XmlDocumentationText.Summary(xml);

		// Assert
		await Assert.That(summary).IsEqualTo("An order a customer has placed.");
	}

	[Test]
	public async Task Summary_References_ReadAsTheirShortNames()
	{
		// Arrange
		const string xml =
			"""<summary>Placed by a <see cref="T:Shop.Customers.Customer"/>, paid with <see cref="M:Shop.Order.Pay(System.Int32)"/> or <see langword="null"/>, see <paramref name="total"/>.</summary>""";

		// Act
		var summary = XmlDocumentationText.Summary(xml);

		// Assert
		await Assert.That(summary).IsEqualTo("Placed by a Customer, paid with Pay or null, see total.");
	}

	[Test]
	public async Task Summary_ReferenceWithText_ReadsAsItsText()
	{
		// Arrange
		const string xml = """<summary>See <see cref="T:Shop.Order">the order</see>.</summary>""";

		// Act
		var summary = XmlDocumentationText.Summary(xml);

		// Assert
		await Assert.That(summary).IsEqualTo("See the order.");
	}

	[Test]
	public async Task Summary_InlineMarkupAndParagraphs_CollapseToOneLine()
	{
		// Arrange
		const string xml = "<summary>The <c>Total</c>   in\n\t pence.<para>Never negative.</para></summary>";

		// Act
		var summary = XmlDocumentationText.Summary(xml);

		// Assert
		await Assert.That(summary).IsEqualTo("The Total in pence. Never negative.");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("<summary>unclosed")]
	[Arguments("<remarks>No summary here.</remarks>")]
	[Arguments("<summary>   </summary>")]
	[Arguments("<inheritdoc/>")]
	public async Task Summary_NothingUsable_ReturnsNull(string? xml)
	{
		// Act
		var summary = XmlDocumentationText.Summary(xml);

		// Assert
		await Assert.That(summary).IsNull();
	}

	[Test]
	public async Task Param_NamedParameter_ReturnsItsText()
	{
		// Arrange
		const string xml =
			"""<summary>A user.</summary><param name="Id">The key.</param><param name="Name">What they are called.</param>""";

		// Act
		var param = XmlDocumentationText.Param(xml, "Name");

		// Assert
		await Assert.That(param).IsEqualTo("What they are called.");
	}

	[Test]
	public async Task FromCommentLines_StripsTheCommentMarkers()
	{
		// Arrange
		string[] lines =
		[
			"    /// <summary>",
			"\t/// Wrapped over",
			"/// two lines.",
			"/// </summary>",
			"// not documentation",
		];

		// Act
		var summary = XmlDocumentationText.Summary(XmlDocumentationText.FromCommentLines(lines));

		// Assert
		await Assert.That(summary).IsEqualTo("Wrapped over two lines.");
	}

	[Test]
	public async Task FromCommentLines_NoDocumentationLines_ReturnsNull()
	{
		// Act
		var xml = XmlDocumentationText.FromCommentLines(["// a comment", "/* another */"]);

		// Assert
		await Assert.That(xml).IsNull();
	}
}
