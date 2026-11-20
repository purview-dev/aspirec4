using System.Text.Json;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Verifies the ER diagram is generated from the sample Entity Framework Core models during a real
/// AppHost run, using the default (interactive) renderer.
/// </summary>
/// <remarks>
/// This is the only test that exercises the full AppHost path — annotation discovery, locating each
/// contributing project's build output, reading descriptors from the assemblies in it, and writing the
/// diagram. The unit tests read sample assemblies directly, so they cannot catch a break in that path.
/// Emitter output itself is asserted exactly in <c>ErdEmitterTests</c>.
/// </remarks>
[ClassDataSource<Fixtures.TestAppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class EntityFrameworkDiagramTests(Fixtures.TestAppHostFixture fixture)
{
	[Before(Test)]
	public void SkipWhenNoContainerRuntime()
	{
		if (fixture.SkipReason is not null)
			Skip.Test(fixture.SkipReason);
	}

	static JsonElement Root(string json) => JsonElement.Parse(json);

	[Test]
	public async Task StartAsync_WhenAppStarts_GeneratesTheModelAndViewer()
	{
		// Assert — the default renderer emits its data plus the viewer that reads it.
		await Assert.That(File.Exists(fixture.ErDiagramPath)).IsTrue();
		await Assert.That(File.Exists(fixture.ErViewerPath)).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelContainsEveryDiscoveredContext(CancellationToken cancellationToken)
	{
		// Act
		var json = await fixture.ReadErModelAsync(cancellationToken);
		var names = Root(json)
			.GetProperty("contexts")
			.EnumerateArray()
			.Select(c => c.GetProperty("name").GetString())
			.ToArray();

		// Assert — the simple sample's single context plus the complex sample's three.
		await Assert.That(names).Contains("BlogDbContext");
		await Assert.That(names).Contains("SalesDbContext");
		await Assert.That(names).Contains("IdentityDbContext");
		await Assert.That(names).Contains("AuditDbContext");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelPreservesAdvancedMappings(CancellationToken cancellationToken)
	{
		// Act
		var json = await fixture.ReadErModelAsync(cancellationToken);

		// Assert — attribute renames, fluent renames and fluent column types all survive.
		await Assert.That(json).Contains("product_id");
		await Assert.That(json).Contains("email_address");
		await Assert.That(json).Contains("decimal(18,2)");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelOmitsExcludedProperties(CancellationToken cancellationToken)
	{
		// Act
		var json = await fixture.ReadErModelAsync(cancellationToken);

		// Assert — [NotMapped] and the fluent Ignore call.
		await Assert.That(json).DoesNotContain("Initials");
		await Assert.That(json).DoesNotContain("CachedDisplayName");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ModelCarriesTheSampleStyling(CancellationToken cancellationToken)
	{
		// The sample applies one example of every styling option (SampleErdStyling), so the whole path —
		// options, rules, callbacks, descriptions read from the built projects — is proven end to end.
		// Act
		var presentation = Root(await fixture.ReadErModelAsync(cancellationToken)).GetProperty("presentation");
		var styles = presentation.GetProperty("styles");
		var descriptions = presentation.GetProperty("descriptions");

		// Assert — a table rule, a single column, a relationship rule and a column callback.
		await Assert
			.That(styles.GetProperty("tables").GetProperty("Orders").GetProperty("badge").GetString())
			.IsEqualTo("SALES");
		await Assert
			.That(
				styles
					.GetProperty("columns")
					.GetProperty("Customers")
					.GetProperty("email_address")
					.GetProperty("badge")
					.GetString()
			)
			.IsEqualTo("PII");
		await Assert
			.That(styles.GetProperty("relationships").TryGetProperty("Orders.CustomerId->Customers", out _))
			.IsTrue();
		await Assert
			.That(styles.GetProperty("columns").GetProperty("OrderLines").TryGetProperty("ProductId", out _))
			.IsTrue();

		// Layout and viewer defaults.
		await Assert.That(presentation.GetProperty("layout").GetProperty("direction").GetString()).IsEqualTo("RIGHT");
		await Assert.That(presentation.GetProperty("viewer").GetProperty("legend").GetBoolean()).IsTrue();
		await Assert.That(presentation.GetProperty("viewer").GetProperty("descriptions").GetBoolean()).IsTrue();

		// Descriptions from an attribute, and from a summary the analyzer read out of the sample's source.
		await Assert
			.That(descriptions.GetProperty("tables").GetProperty("products").GetString())
			.IsEqualTo("Something the shop sells");
		await Assert
			.That(descriptions.GetProperty("tables").GetProperty("Customers").GetString())
			.IsEqualTo("A customer, keyed conventionally and uniquely indexed on email.");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ErDiagramResourceReportsAState(CancellationToken cancellationToken)
	{
		// The resource runs no process of its own, so without publishing its own state the dashboard
		// leaves it Unknown with no logs — which is exactly how this shipped the first time.
		// Act
		var state = await fixture.GetResourceStateAsync("aspirec4-er", cancellationToken);

		// Assert
		await Assert.That(state).IsEqualTo(KnownResourceStates.Running);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_PublishesAUrlForTheViewer(CancellationToken cancellationToken)
	{
		// The viewer is served so the dashboard has a real link; a file:// URL could not be navigated
		// to from the dashboard's own origin.
		// Act
		var urls = await fixture.GetResourceUrlsAsync("aspirec4-er", cancellationToken);

		// Assert
		await Assert.That(urls).IsNotEmpty();
		await Assert.That(urls.Any(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase))).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ServesTheViewerOverHttp(CancellationToken cancellationToken)
	{
		// Act
		var urls = await fixture.GetResourceUrlsAsync("aspirec4-er", cancellationToken);
		using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };

		var response = await client.GetAsync(new Uri(urls[0]), cancellationToken);
		var body = await response.Content.ReadAsStringAsync(cancellationToken);

		// Assert
		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(body).Contains("</html>");
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ServesTheViewerDependencies(CancellationToken cancellationToken)
	{
		// The dependencies ship with the package and are written out on generation. When they were a
		// manual step the viewer served fine and rendered a blank page.
		// Act
		var urls = await fixture.GetResourceUrlsAsync("aspirec4-er", cancellationToken);
		using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };

		var bundle = await client.GetAsync(new Uri($"{urls[0].TrimEnd('/')}/vendor/bundle.js"), cancellationToken);
		var css = await client.GetAsync(new Uri($"{urls[0].TrimEnd('/')}/vendor/reactflow.css"), cancellationToken);

		// Assert
		await Assert.That(bundle.IsSuccessStatusCode).IsTrue();
		await Assert.That(css.IsSuccessStatusCode).IsTrue();
		await Assert.That(bundle.Content.Headers.ContentLength ?? 0).IsGreaterThan(100_000);
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ServesTheGeneratedModelAlongsideTheViewer(
		CancellationToken cancellationToken
	)
	{
		// The whole output directory is served, so every export is downloadable from the same URL.
		// Act
		var urls = await fixture.GetResourceUrlsAsync("aspirec4-er", cancellationToken);
		using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };

		var response = await client.GetAsync(new Uri($"{urls[0].TrimEnd('/')}/schema.gen.json"), cancellationToken);

		// Assert
		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task StartAsync_WhenAppStarts_ViewerPointsAtTheGeneratedModel(CancellationToken cancellationToken)
	{
		// Act
		var script = await File.ReadAllTextAsync(
			Path.Combine(Path.GetDirectoryName(fixture.ErViewerPath)!, "app.js"),
			cancellationToken
		);

		// Assert — the placeholder must have been substituted, or the viewer loads nothing.
		await Assert.That(script).Contains("schema.gen.json");
		await Assert.That(script).DoesNotContain("__MODEL__");
	}
}
