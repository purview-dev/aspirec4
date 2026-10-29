using System.Diagnostics;
using Aspire.Hosting.AspireC4.EntityFrameworkCore;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Mermaid;
using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;
using Purview.Containers.Environment;

namespace Aspire.Hosting.AspireC4.Mermaid;

/// <summary>
/// Validates emitted Mermaid against the real Mermaid 12 renderer via <c>mermaid-cli</c>.
/// </summary>
/// <remarks>
/// String assertions prove the emitter produces what was intended; only running the real renderer
/// proves Mermaid accepts it. The grammar is narrower than it looks — attribute types permit
/// parentheses but not commas — so this is the test that catches an unparseable diagram.
/// </remarks>
public sealed class MermaidValidationTests
{
	const string Image = "ghcr.io/mermaid-js/mermaid-cli/mermaid-cli:12.0.0";

	string _tempDir = string.Empty;

	[Before(Test)]
	public void SetUp()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"aspirec4-mmd-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
	}

	[After(Test)]
	public void TearDown()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, recursive: true);
	}

	// Helpers

	static AspireC4EfDiagramOptions CreateOptions() => new() { Renderer = ErdRenderer.Mermaid };

	static EfColumn CreateColumn(
		string name,
		string storeType = "int",
		bool isNullable = false,
		bool isPrimaryKey = false,
		bool isForeignKey = false,
		bool isUnique = false,
		int? maxLength = null
	) =>
		new()
		{
			Name = name,
			StoreType = storeType,
			IsNullable = isNullable,
			IsPrimaryKey = isPrimaryKey,
			IsForeignKey = isForeignKey,
			IsUnique = isUnique,
			MaxLength = maxLength,
		};

	/// <summary>
	/// Two contexts, so subgraphs and class styling are exercised, with the awkward cases: a
	/// precision type containing a comma, a composite key that is both primary and foreign, a nullable
	/// column, and a relationship spanning the two contexts' entities.
	/// </summary>
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
								CreateColumn("Id", isPrimaryKey: true),
								CreateColumn("Total", "decimal(18,2)") with
								{
									Comment = "Gross, including tax",
								},
								CreateColumn("ShippedAt", "datetimeoffset", isNullable: true),
							],
						},
						new EfTable
						{
							Name = "OrderLines",
							ClrTypeName = "Sample.OrderLine",
							Columns =
							[
								CreateColumn("OrderId", isPrimaryKey: true, isForeignKey: true),
								CreateColumn("Sku", "varchar", maxLength: 64, isUnique: true),
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
				new EfContext
				{
					Name = "IdentityDbContext",
					TypeName = "Sample.IdentityDbContext",
					DefaultSchema = "identity",
					Tables =
					[
						new EfTable
						{
							Name = "Users",
							ClrTypeName = "Sample.User",
							Columns = [CreateColumn("Id", "guid", isPrimaryKey: true)],
						},
					],
				},
			],
		};

	async Task<(int ExitCode, string Output)> RenderAsync(string mermaid, CancellationToken cancellationToken)
	{
		var input = Path.Combine(_tempDir, "diagram.mmd");
		await File.WriteAllTextAsync(input, mermaid, cancellationToken);

		ProcessStartInfo startInfo = new()
		{
			FileName = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") ?? "docker",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		foreach (
			var arg in new[]
			{
				"run",
				"--rm",
				"--mount",
				new ContainerBindMount(_tempDir, "/data").ToString(),
				Image,
				"-i",
				"/data/diagram.mmd",
				"-o",
				"/data/diagram.svg",
			}
		)
		{
			startInfo.ArgumentList.Add(arg);
		}

		await ContainerEnvironmentResolver
			.ForRuntime(startInfo.FileName)
			.ResolveBindMountsAsync(startInfo, cancellationToken);

		using var process =
			Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the container runtime.");

		var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

		await process.WaitForExitAsync(cancellationToken);

		return (process.ExitCode, await stdout + await stderr);
	}

	// Tests

	[Test]
	public async Task Generate_RealWorldModel_IsAcceptedByMermaid(CancellationToken cancellationToken)
	{
		// Arrange
		var mermaid = MermaidGenerator.Generate(CreateModel(), CreateOptions());

		// Act
		var (exitCode, output) = await RenderAsync(mermaid, cancellationToken);

		// Assert
		await Assert.That(exitCode).IsEqualTo(0).Because(output);
		await Assert.That(File.Exists(Path.Combine(_tempDir, "diagram.svg"))).IsTrue();
	}

	[Test]
	public async Task Generate_WithSubgraphsDisabled_IsAcceptedByMermaid(CancellationToken cancellationToken)
	{
		// Flat output is the fallback for renderers pinned to a Mermaid older than 11.17.
		// Arrange
		AspireC4EfDiagramOptions options = new() { Renderer = ErdRenderer.Mermaid, UseSubgraphs = false };
		var mermaid = MermaidGenerator.Generate(CreateModel(), options);

		// Act
		var (exitCode, output) = await RenderAsync(mermaid, cancellationToken);

		// Assert
		await Assert.That(exitCode).IsEqualTo(0).Because(output);
	}

	[Test]
	public async Task Generate_ContextStyling_ReachesTheRenderedOutput(CancellationToken cancellationToken)
	{
		// A ':::' assignment with no matching classDef parses but silently does nothing, so this
		// asserts the fill actually lands in the SVG.
		// Arrange
		var mermaid = MermaidGenerator.Generate(CreateModel(), CreateOptions());

		// Act
		await RenderAsync(mermaid, cancellationToken);
		var svg = await File.ReadAllTextAsync(Path.Combine(_tempDir, "diagram.svg"), cancellationToken);

		// Assert
		await Assert.That(svg).Contains("dbeafe");
	}
}
