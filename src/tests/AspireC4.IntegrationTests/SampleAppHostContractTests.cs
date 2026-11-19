namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Asserts the C# sample AppHost draws the shared <see cref="SampleAppHostContract"/>. This file is
/// compiled into both C# integration suites, so it covers <c>TestAppHost</c> on Aspire 17 and
/// <c>TestAppHost.Aspire13</c> on 13.6.5; <c>AspireC4.AppHosts.IntegrationTests</c> asserts the same
/// contract against the TypeScript, Go and Python samples.
/// </summary>
[ClassDataSource<Fixtures.TestAppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class SampleAppHostContractTests(Fixtures.TestAppHostFixture fixture)
{
	[Before(Test)]
	public void SkipWhenNoContainerRuntime()
	{
		if (fixture.SkipReason is not null)
			Skip.Test(fixture.SkipReason);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_DrawsTheSharedSampleArchitecture(CancellationToken cancellationToken)
	{
		// Arrange
		var model = await File.ReadAllTextAsync(fixture.ModelPath, cancellationToken);

		// Act
		var violations = SampleAppHostContract.Verify(model);

		// Assert
		await Assert.That(violations).IsEmpty().Because(string.Join(Environment.NewLine, violations));
	}
}
