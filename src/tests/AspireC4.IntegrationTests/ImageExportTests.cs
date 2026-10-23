using Aspire.Hosting.AspireC4.EntityFrameworkCore;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Drives both image exporters against the real <c>d2</c> and <c>likec4</c> containers.
/// </summary>
/// <remarks>
/// The unit tests assert the arguments; only running the containers proves those arguments are accepted
/// and that an image comes back. What breaks here is a mount or a working directory — a LikeC4 export
/// whose working directory is wrong fails on an ENOENT for an icon, which names nothing useful, and a d2
/// export whose output mount is read-only fails while the invocation itself still looks correct.
/// <para>
/// Run via <c>just test-integration</c>, not an IDE runner, and note that the LikeC4 export starts a
/// server and a headless browser, so it takes tens of seconds.
/// </para>
/// </remarks>
public sealed class ImageExportTests
{
	string _tempDir = string.Empty;

	[Before(Test)]
	public void SetUp()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"aspirec4-export-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
	}

	[After(Test)]
	public void TearDown()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, recursive: true);
	}

	// Helpers

	static EfModel CreateModel() =>
		new()
		{
			Contexts =
			[
				new EfContext
				{
					Name = "SalesDbContext",
					TypeName = "Sample.SalesDbContext",
					DefaultSchema = "sales",
					Tables =
					[
						new EfTable
						{
							Name = "Orders",
							ClrTypeName = "Sample.Order",
							Columns =
							[
								new EfColumn
								{
									Name = "Id",
									StoreType = "int",
									IsPrimaryKey = true,
								},
								new EfColumn { Name = "Total", StoreType = "decimal(18,2)" },
							],
						},
						new EfTable
						{
							Name = "OrderLines",
							ClrTypeName = "Sample.OrderLine",
							Columns =
							[
								new EfColumn
								{
									Name = "OrderId",
									StoreType = "int",
									IsForeignKey = true,
								},
							],
							ForeignKeys =
							[
								new EfForeignKey
								{
									Columns = ["OrderId"],
									PrincipalTable = "Orders",
									PrincipalColumns = ["Id"],
									NavigationName = "Order",
									IsRequired = true,
								},
							],
						},
					],
				},
			],
		};

	/// <summary>Writes the smallest LikeC4 project that still produces a view to export.</summary>
	async Task WriteLikeC4ProjectAsync(CancellationToken cancellationToken)
	{
		await File.WriteAllTextAsync(
			Path.Combine(_tempDir, "likec4.config.json"),
			"""{ "name": "aspirec4", "title": "Export Test" }""",
			cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(_tempDir, "model.gen.c4"),
			"""
			specification {
			  element system
			}

			model {
			  api = system 'API'
			  db = system 'Database'
			  api -> db 'reads'
			}

			views {
			  view index {
			    title 'Architecture'
			    include *
			  }
			}
			""",
			cancellationToken
		);
	}

	// Tests

	[Test]
	public async Task ExportAsync_InteractiveRenderer_WritesSvgAndPngThroughD2(CancellationToken cancellationToken)
	{
		// The default renderer has no headless path of its own, so this is the case that proves the d2
		// source is written and rendered rather than the export silently doing nothing.
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = ErdRenderer.Interactive };

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			NullLogger.Instance,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(2);
		await Assert.That(File.Exists(Path.Combine(_tempDir, "schema.gen.d2"))).IsTrue();

		var svg = Path.Combine(_tempDir, "schema.gen.svg");
		var png = Path.Combine(_tempDir, "schema.gen.png");

		await Assert.That(File.Exists(svg)).IsTrue();
		await Assert.That(File.Exists(png)).IsTrue();

		// Enough to prove d2 rendered rather than wrote an empty file or an error page.
		var svgContent = await File.ReadAllTextAsync(svg, cancellationToken);

		await Assert.That(svgContent).Contains("<svg");
		await Assert.That(svgContent).Contains("Orders");

		await Assert.That(IsPng(await File.ReadAllBytesAsync(png, cancellationToken))).IsTrue();
	}

	[Test]
	public async Task ExportAsync_SvgOnly_WritesOnlyTheSvg(CancellationToken cancellationToken)
	{
		// Arrange
		var options = new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Svg);

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			NullLogger.Instance,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(1);
		await Assert.That(File.Exists(Path.Combine(_tempDir, "schema.gen.svg"))).IsTrue();
		await Assert.That(File.Exists(Path.Combine(_tempDir, "schema.gen.png"))).IsFalse();
	}

	[Test]
	public async Task ExportAsync_NoFormats_WritesNothingAndRunsNoContainer(CancellationToken cancellationToken)
	{
		// Not merely "no images": the d2 source is written for export's benefit alone, so an export that
		// is off must not leave one behind either.
		// Arrange
		AspireC4EfDiagramOptions options = new();
		options.ImageFormats = [];

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			NullLogger.Instance,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEmpty();
		await Assert.That(Directory.EnumerateFileSystemEntries(_tempDir)).IsEmpty();
	}

	[Test]
	public async Task ExportAsync_LikeC4Views_WritesAPngPerView(CancellationToken cancellationToken)
	{
		// Covers the mount and working-directory handling against the real CLI: the export container sees
		// the project through a read-only /data mount and writes to a separate writable /out.
		// Arrange
		await WriteLikeC4ProjectAsync(cancellationToken);

		AspireC4DiagramOptions options = new();

		// Act
		var exported = await LikeC4ImageExporter.ExportAsync(
			options,
			_tempDir,
			_tempDir,
			localCLI: null,
			Lifecycle.IAspireC4LifecycleHookTelemetry.Mock(),
			cancellationToken
		);

		// Assert
		await Assert.That(exported).IsEqualTo(1);

		var images = Directory.GetFiles(Path.Combine(_tempDir, "images"), "*.png");

		await Assert.That(images).IsNotEmpty();
		await Assert.That(images.Select(Path.GetFileName)).Contains("index.png");
		await Assert.That(IsPng(await File.ReadAllBytesAsync(images[0], cancellationToken))).IsTrue();
	}

	static bool IsPng(byte[] content) =>
		content.Length > PngMagic.Length && content.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic);

	static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
}
