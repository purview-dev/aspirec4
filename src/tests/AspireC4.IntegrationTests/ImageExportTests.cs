using Aspire.Hosting.AspireC4.EntityFrameworkCore;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.Logging;

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
/// <para>
/// Serialised with <c>[NotInParallel]</c>: every test here starts a container, and running several at
/// once alongside the rest of the suite loads the container runtime to the point where it starts
/// rejecting otherwise-valid invocations.
/// </para>
/// </remarks>
[NotInParallel]
public sealed class ImageExportTests
{
	string _tempDir = string.Empty;

	readonly CapturingLogger logger = new();

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

	/// <summary>
	/// The shared folder both diagrams export into, relative to the temp directory standing in for the
	/// AppHost directory.
	/// </summary>
	string ImagesDirectory => Path.Combine(_tempDir, ".aspirec4", "images");

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
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(2).Because(logger.Messages);

		// The d2 source stays with the generated diagram; only the images move to the shared folder.
		await Assert.That(File.Exists(Path.Combine(_tempDir, "schema.gen.d2"))).IsTrue();

		var svg = Path.Combine(ImagesDirectory, "schema.svg");
		var png = Path.Combine(ImagesDirectory, "schema.png");

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
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(1).Because(logger.Messages);
		await Assert.That(File.Exists(Path.Combine(ImagesDirectory, "schema.svg"))).IsTrue();
		await Assert.That(File.Exists(Path.Combine(ImagesDirectory, "schema.png"))).IsFalse();
	}

	[Test]
	public async Task ExportAsync_RunTwiceOverAnUnchangedModel_RendersOnlyOnce(CancellationToken cancellationToken)
	{
		// The AppHost's own change detection lives in memory and resets on every restart, so without the
		// on-disk stamp a restart re-renders images that are already correct. On the viewer path that
		// would mean pulling and starting a multi-gigabyte browser to reproduce the same bytes.
		// Arrange
		var options = new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Svg);
		var svg = Path.Combine(ImagesDirectory, "schema.svg");

		await ErdImageExporter.ExportAsync(CreateModel(), options, _tempDir, _tempDir, logger, cancellationToken);

		var first = File.GetLastWriteTimeUtc(svg);

		// Act
		var second = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(second).IsEmpty();
		await Assert.That(File.GetLastWriteTimeUtc(svg)).IsEqualTo(first);
	}

	[Test]
	public async Task ExportAsync_AfterTheImageIsDeleted_RendersAgain(CancellationToken cancellationToken)
	{
		// A matching stamp alone would skip the export forever once someone cleared the folder out.
		// Arrange
		var options = new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Svg);
		var svg = Path.Combine(ImagesDirectory, "schema.svg");

		await ErdImageExporter.ExportAsync(CreateModel(), options, _tempDir, _tempDir, logger, cancellationToken);

		File.Delete(svg);

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(1).Because(logger.Messages);
		await Assert.That(File.Exists(svg)).IsTrue();
	}

	[Test]
	public async Task ExportAsync_WhenAFormatIsAdded_RendersAgain(CancellationToken cancellationToken)
	{
		// The stamp covers the requested formats, not just the model: asking for a PNG as well has to
		// render even though the model behind it has not moved.
		// Arrange
		await ErdImageExporter.ExportAsync(
			CreateModel(),
			new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Svg),
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Svg, ErdImageFormat.Png),
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(2).Because(logger.Messages);
	}

	[Test]
	public async Task ExportAsync_CustomImageDirectory_WritesThere(CancellationToken cancellationToken)
	{
		// Arrange
		var options = new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Svg);
		options.ImageExportDirectory = "docs/diagrams";

		// Act
		await ErdImageExporter.ExportAsync(CreateModel(), options, _tempDir, _tempDir, logger, cancellationToken);

		// Assert
		await Assert
			.That(File.Exists(Path.Combine(_tempDir, "docs", "diagrams", "schema.svg")))
			.IsTrue()
			.Because(logger.Messages);
	}

	[Test]
	public async Task ExportAsync_NoFormats_WritesNothingAndRunsNoContainer(CancellationToken cancellationToken)
	{
		// Not merely "no images": the d2 source is written for export's benefit alone, and the images
		// folder is created by the export, so neither should be left behind when export is off.
		// Arrange
		AspireC4EfDiagramOptions options = new();
		options.ImageFormats = [];

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written).IsEmpty();
		await Assert.That(Directory.EnumerateFileSystemEntries(_tempDir)).IsEmpty();
	}

	[Test]
	public async Task ExportAsync_ViewerFidelity_RendersThroughTheViewerItself(CancellationToken cancellationToken)
	{
		// The point of the opt-in path: the file is the one the viewer's own Save as SVG control
		// produces, not a d2 render of the same model. Asserted on the markup rather than on the file
		// merely existing, because a d2 render would also produce a plausible .svg.
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = ErdRenderer.Interactive, UseViewerFidelityExport = true };

		options.ImageFormats = [ErdImageFormat.Svg];

		// The viewer and its dependencies have to be on disk for a browser to load them.
		foreach (var file in ErdOutput.Create(CreateModel(), options))
		{
			await File.WriteAllTextAsync(Path.Combine(_tempDir, file.FileName), file.Content, cancellationToken);
		}

		await InteractiveViewer.WriteVendorAssetsAsync(_tempDir, ErdRenderer.Interactive, cancellationToken);

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(1).Because(logger.Messages);

		var svg = await File.ReadAllTextAsync(Path.Combine(ImagesDirectory, "schema.svg"), cancellationToken);

		await Assert.That(svg).StartsWith("<svg");
		// React Flow's own markup, which only the viewer produces — d2 emits none of it.
		await Assert.That(svg).Contains("react-flow");
		await Assert.That(svg).Contains("Orders");
	}

	[Test]
	public async Task ExportAsync_ViewerFidelityOnANonInteractiveRenderer_FallsBackToD2(
		CancellationToken cancellationToken
	)
	{
		// Only the interactive renderer writes a viewer, so there is nothing to drive otherwise. The
		// export still happens rather than being abandoned, and says why in the log.
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = ErdRenderer.Dbml, UseViewerFidelityExport = true };

		options.ImageFormats = [ErdImageFormat.Svg];

		// Act
		var written = await ErdImageExporter.ExportAsync(
			CreateModel(),
			options,
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		// Assert
		await Assert.That(written.Count).IsEqualTo(1).Because(logger.Messages);
		await Assert.That(logger.Messages).Contains("Dbml");
	}

	[Test]
	public async Task ExportAsync_LikeC4Views_WritesAPrefixedPngPerViewIntoTheSharedFolder(
		CancellationToken cancellationToken
	)
	{
		// Covers the mount and working-directory handling against the real CLI, and the rename that
		// follows: the CLI names each file after its view id, so a bare "index.png" would say nothing
		// about which diagram it came from in a folder shared with the ER diagram.
		// Arrange
		await WriteLikeC4ProjectAsync(cancellationToken);

		AspireC4DiagramOptions options = new();

		// Act
		var exported = await LikeC4ImageExporter.ExportAsync(
			options,
			_tempDir,
			_tempDir,
			_tempDir,
			modelBody: "stamp",
			localCLI: null,
			Lifecycle.IAspireC4LifecycleHookTelemetry.Mock(),
			cancellationToken
		);

		// Assert
		await Assert.That(exported).IsEqualTo(1);

		var images = Directory.GetFiles(ImagesDirectory, "*.png");

		await Assert.That(images).IsNotEmpty();
		await Assert.That(images.Select(Path.GetFileName)).Contains("architecture-index.png");
		await Assert.That(IsPng(await File.ReadAllBytesAsync(images[0], cancellationToken))).IsTrue();
	}

	[Test]
	public async Task ExportAsync_BothDiagrams_LandInTheSameFolder(CancellationToken cancellationToken)
	{
		// The whole point of the shared location: one folder to look in, with names that say which
		// diagram each image came from.
		// Arrange
		await WriteLikeC4ProjectAsync(cancellationToken);

		// Act
		await ErdImageExporter.ExportAsync(
			CreateModel(),
			new AspireC4EfDiagramOptions().WithImageExport(ErdImageFormat.Png),
			_tempDir,
			_tempDir,
			logger,
			cancellationToken
		);

		await LikeC4ImageExporter.ExportAsync(
			new AspireC4DiagramOptions(),
			_tempDir,
			_tempDir,
			_tempDir,
			modelBody: "stamp",
			localCLI: null,
			Lifecycle.IAspireC4LifecycleHookTelemetry.Mock(),
			cancellationToken
		);

		// Assert
		var images = Directory.GetFiles(ImagesDirectory, "*.png").Select(Path.GetFileName).ToArray();

		await Assert.That(images).Contains("schema.png").Because(logger.Messages);
		await Assert.That(images).Contains("architecture-index.png");
	}

	/// <summary>
	/// Collects what the exporter logged, so a failed export explains itself in the assertion message.
	/// </summary>
	/// <remarks>
	/// Export is best-effort by design: it reports a failure and returns, so the return value alone
	/// reduces every cause — a missing runtime, a bad mount, a d2 parse error — to the same count.
	/// </remarks>
	sealed class CapturingLogger : ILogger
	{
		readonly List<string> _messages = [];

		public string Messages
		{
			get
			{
				lock (_messages)
				{
					return _messages.Count == 0 ? "(nothing logged)" : string.Join(" / ", _messages);
				}
			}
		}

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter
		)
		{
			lock (_messages)
			{
				_messages.Add(formatter(state, exception));
			}
		}
	}

	static bool IsPng(byte[] content) =>
		content.Length > PngMagic.Length && content.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic);

	static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
}
