namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>Style rule patterns: case-insensitive, <c>*</c> for any run, <c>?</c> for one character.</summary>
public sealed class ErdWildcardTests
{
	// Tests

	[Test]
	[Arguments("Orders", "Orders")]
	[Arguments("orders", "ORDERS")]
	[Arguments("Order*", "OrderLines")]
	[Arguments("*Id", "CustomerId")]
	[Arguments("*Id", "Id")]
	[Arguments("*", "anything")]
	[Arguments("*", "")]
	[Arguments("Order?", "Orders")]
	[Arguments("*Line*", "OrderLines")]
	[Arguments("a*b*c", "axxbyyc")]
	[Arguments("decimal*", "decimal(18,2)")]
	public async Task IsMatch_MatchingValue_IsTrue(string pattern, string value)
	{
		// Act & Assert
		await Assert.That(ErdWildcard.IsMatch(pattern, value)).IsTrue();
	}

	[Test]
	[Arguments("Orders", "Order")]
	[Arguments("Order", "Orders")]
	[Arguments("*Id", "Identity")]
	[Arguments("Order?", "Order")]
	[Arguments("a*b*c", "axxbyy")]
	[Arguments("", "x")]
	public async Task IsMatch_NonMatchingValue_IsFalse(string pattern, string value)
	{
		// Act & Assert
		await Assert.That(ErdWildcard.IsMatch(pattern, value)).IsFalse();
	}

	[Test]
	public async Task IsMatch_NullValue_IsFalse()
	{
		// Act & Assert
		await Assert.That(ErdWildcard.IsMatch("*", null)).IsFalse();
	}
}
