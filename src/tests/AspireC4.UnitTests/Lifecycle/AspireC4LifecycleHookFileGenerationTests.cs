using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4.Lifecycle;

/// <summary>
/// Covers what <see cref="AspireC4LifecycleHook.WriteC4FileAsync"/> reports while writing the model.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="AspireC4LifecycleHookTests"/>, which is already at the analyzers' class-coupling
/// limit.
/// </remarks>
public sealed class AspireC4LifecycleHookFileGenerationTests
{
	[Test]
	public async Task WriteC4FileAsync_WithAMissingAdditionalDSLFile_ReportsIt(CancellationToken cancellationToken)
	{
		// A typo in the path used to be skipped in silence, which looks exactly like a file that LikeC4
		// read and found nothing in.
		// Arrange
		var directory = CreateTempDirectory();
		var missing = Path.Combine(directory, "does-not-exist.c4");
		var telemetry = IAspireC4LifecycleHookTelemetry.Mock();
		using var sut = CreateLifecycleHookSut(
			diagramOptions: CreateWriteOptions(directory, additionalDSLFiles: [missing]),
			workspaceOptions: new ContainerWorkspaceOptions { AppHostDirectory = directory },
			telemetry: telemetry.Object
		);

		try
		{
			// Act
			await sut.WriteC4FileAsync(CreateEmptyModel(), cancellationToken);

			// Assert
			telemetry.AdditionalDSLFileMissing(Arg.Is(missing)).WasCalled(Times.Once);
			await Assert.That(File.Exists(Path.Combine(directory, "model.gen.c4"))).IsTrue();
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Test]
	public async Task WriteC4FileAsync_WhenTheModelIsUnchanged_DoesNotReportAWrite(CancellationToken cancellationToken)
	{
		// "Model written" used to be logged on every pass, including the many that changed nothing.
		// Arrange
		var directory = CreateTempDirectory();
		var telemetry = IAspireC4LifecycleHookTelemetry.Mock();
		using var sut = CreateLifecycleHookSut(
			diagramOptions: CreateWriteOptions(directory),
			workspaceOptions: new ContainerWorkspaceOptions { AppHostDirectory = directory },
			telemetry: telemetry.Object
		);
		var model = CreateEmptyModel();

		try
		{
			// Act
			await sut.WriteC4FileAsync(model, cancellationToken);
			await sut.WriteC4FileAsync(model, cancellationToken);

			// Assert
			telemetry.LikeC4ModelWritten(Arg.Any<string>()).WasCalled(Times.Once);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	// Helpers

	static AspireC4DiagramOptions CreateWriteOptions(string outputDirectory, string[]? additionalDSLFiles = null)
	{
		AspireC4DiagramOptions options = new()
		{
			OutputDirectory = outputDirectory,
			FormatGeneratedFile = false,
			GenerateGitIgnoreFile = false,
			GenerateConfigFile = false,
			ExportImages = false,
		};

		foreach (var file in additionalDSLFiles ?? [])
		{
			options.AdditionalDSLFiles.Add(file);
		}

		return options;
	}

	static DistributedApplicationModel CreateEmptyModel() => new(Array.Empty<IResource>());

	static AspireC4LifecycleHook CreateLifecycleHookSut(
		AspireC4DiagramOptions diagramOptions,
		ContainerWorkspaceOptions workspaceOptions,
		IAspireC4LifecycleHookTelemetry telemetry
	) => new(Options.Create(diagramOptions), Options.Create(workspaceOptions), null!, null!, telemetry, null!);

	static string CreateTempDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"aspirec4-write-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);

		return directory;
	}
}
