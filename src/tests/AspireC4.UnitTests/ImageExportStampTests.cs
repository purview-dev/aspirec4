namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Covers what does and does not invalidate the record of what the exported images were rendered from.
/// </summary>
public sealed class ImageExportStampTests
{
	// Tests

	[Test]
	public async Task Compute_ForTheSameInputs_IsStable()
	{
		// Act
		var first = ImageExportStamp.Compute("model", "png");
		var second = ImageExportStamp.Compute("model", "png");

		// Assert
		await Assert.That(first).IsEqualTo(second);
	}

	[Test]
	public async Task Compute_WhenTheModelChanges_Differs()
	{
		// Act
		var before = ImageExportStamp.Compute("model", "png");
		var after = ImageExportStamp.Compute("model with a new element", "png");

		// Assert
		await Assert.That(before).IsNotEqualTo(after);
	}

	[Test]
	public async Task Compute_WhenTheRequestedFormatsChange_Differs()
	{
		// The images already on disk are not the ones now being asked for, so they are not current.
		// Act
		var before = ImageExportStamp.Compute("model", "png");
		var after = ImageExportStamp.Compute("model", "png,jpg");

		// Assert
		await Assert.That(before).IsNotEqualTo(after);
	}

	[Test]
	public async Task Compute_WhenOnlyEndpointPortsDiffer_IsUnchanged()
	{
		// Aspire allocates a different port on every run, and the architecture model embeds the endpoint
		// URLs. Hashing those verbatim would invalidate the stamp every single time, so this diagram
		// would start a server and a browser on every run to redraw something that has not changed.
		// Act
		var first = ImageExportStamp.Compute("link http://localhost:51780/api", "png");
		var second = ImageExportStamp.Compute("link http://localhost:62991/api", "png");

		// Assert
		await Assert.That(first).IsEqualTo(second);
	}

	[Test]
	public async Task Compute_WhenTheHostDiffers_Differs()
	{
		// Only the port is treated as noise; a host that genuinely moved is a real change.
		// Act
		var first = ImageExportStamp.Compute("link http://localhost:51780/api", "png");
		var second = ImageExportStamp.Compute("link http://example.test:51780/api", "png");

		// Assert
		await Assert.That(first).IsNotEqualTo(second);
	}

	[Test]
	public async Task Normalise_LeavesNonUrlNumbersAlone()
	{
		// A port is only a port after a scheme and host; a decimal or a time must survive untouched.
		// Act
		var result = ImageExportStamp.Normalise("total decimal(18,2) at 10:30 and http://host:8080/x");

		// Assert
		await Assert.That(result).Contains("decimal(18,2)");
		await Assert.That(result).Contains("10:30");
		await Assert.That(result).Contains("http://host:<port>/x");
	}

	[Test]
	public async Task IsCurrent_WithAMatchingStampAndTheFilesPresent_IsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();
		var image = Path.Combine(directory, "schema.svg");

		await File.WriteAllTextAsync(image, "<svg/>", cancellationToken);

		var stamp = ImageExportStamp.Compute("model", "svg");

		await ImageExportStamp.WriteAsync(directory, "erd", stamp, cancellationToken);

		// Act
		var result = await ImageExportStamp.IsCurrentAsync(directory, "erd", stamp, [image], cancellationToken);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task IsCurrent_WhenTheImageWasDeleted_IsFalse(CancellationToken cancellationToken)
	{
		// A matching stamp alone would skip the export forever once someone cleared the folder out.
		// Arrange
		var directory = CreateDirectory();
		var stamp = ImageExportStamp.Compute("model", "svg");

		await ImageExportStamp.WriteAsync(directory, "erd", stamp, cancellationToken);

		// Act
		var result = await ImageExportStamp.IsCurrentAsync(
			directory,
			"erd",
			stamp,
			[Path.Combine(directory, "schema.svg")],
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task IsCurrent_WithNoStampAtAll_IsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = CreateDirectory();
		var image = Path.Combine(directory, "schema.svg");

		await File.WriteAllTextAsync(image, "<svg/>", cancellationToken);

		// Act
		var result = await ImageExportStamp.IsCurrentAsync(
			directory,
			"erd",
			ImageExportStamp.Compute("model", "svg"),
			[image],
			cancellationToken
		);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task FileName_ForTheTwoDiagrams_Differs()
	{
		// Both diagrams export into the same folder, so one stamp file would have them overwriting
		// each other's and each concluding the other's images were its own.
		// Act
		var erd = ImageExportStamp.FileName("erd");
		var architecture = ImageExportStamp.FileName("architecture");

		// Assert
		await Assert.That(erd).IsNotEqualTo(architecture);
	}

	static string CreateDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"aspirec4-stamp-{Guid.NewGuid():N}");

		Directory.CreateDirectory(directory);

		return directory;
	}
}
