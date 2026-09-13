using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4.Generators;

/// <summary>
/// Validates generated LikeC4 DSL output using the real <c>likec4 validate</c> CLI
/// (<c>npx likec4 validate --json --no-layout</c>).  Each test generates a DSL string,
/// writes it to a temporary project directory, runs the validator and asserts that the
/// number of validation errors in the generated file is zero.
/// </summary>
public sealed class LikeC4DSLValidationTests
{
	static readonly AspireC4DiagramOptions DefaultOptions = new()
	{
		Title = "Test Architecture",
		OutputDirectory = "./likec4",
		FileName = "model.gen",
	};

	string _tempDir = string.Empty;
	string _dslFile = string.Empty;

	[Before(Test)]
	public void SetUp()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"aspirec4-dslval-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
		// Minimal LikeC4 project config — `name` is required by the CLI.
		File.WriteAllText(
			Path.Combine(_tempDir, "likec4.config.json"), /*lang=json,strict*/
			"""{"name":"aspirec4-test"}"""
		);
		_dslFile = Path.Combine(_tempDir, "model.c4");
	}

	[After(Test)]
	public void TearDown()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// Helper

	readonly record struct ValidationResult(bool Valid, int FilteredErrors, int TotalErrors, string RawOutput);

	async Task<ValidationResult> RunValidateAsync(
		string dsl,
		CancellationToken cancellationToken,
		(string FileName, string Content)[]? additionalFiles = null
	)
	{
		await File.WriteAllTextAsync(_dslFile, dsl);

		List<string> filesToValidate = [_dslFile];

		if (additionalFiles != null)
		{
			foreach (var (fileName, content) in additionalFiles)
			{
				var path = Path.Combine(_tempDir, fileName);
				await File.WriteAllTextAsync(path, content);
				filesToValidate.Add(path);
			}
		}

		var useDotFlag = await Helpers.IsDotAvailableAsync(cancellationToken) ? " --use-dot" : "";

		// npx on Windows is a .cmd wrapper, so it must be invoked through the shell.
		string shellFile,
			shellArgs;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var fileArgs = string.Join(" ", filesToValidate.Select(f => $"--file \"{f}\""));
			shellFile = "cmd.exe";
			shellArgs = $"/c npx --yes likec4 validate --json --no-layout{useDotFlag} {fileArgs} \"{_tempDir}\"";
		}
		else
		{
			var fileArgs = string.Join(" ", filesToValidate.Select(f => $"--file '{f}'"));
			shellFile = "/bin/sh";
			shellArgs = $"-c \"npx --yes likec4 validate --json --no-layout{useDotFlag} {fileArgs} '{_tempDir}'\"";
		}

		using Process process = new()
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = shellFile,
				Arguments = shellArgs,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = _tempDir,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdout = await process.StandardOutput.ReadToEndAsync();
		var stderr = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		// Extract JSON object from stdout — npx may prefix with a download progress line.
		var jsonStart = stdout.IndexOf('{', StringComparison.Ordinal);
		var jsonEnd = stdout.LastIndexOf('}');
		if (jsonStart < 0 || jsonEnd < 0)
		{
			var rawOutput = $"stdout:\n{stdout}\nstderr:\n{stderr}";
			return new ValidationResult(Valid: false, FilteredErrors: -1, TotalErrors: -1, RawOutput: rawOutput);
		}

		var json = stdout[jsonStart..(jsonEnd + 1)];
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var valid = root.GetProperty("valid").GetBoolean();
		var stats = root.GetProperty("stats");
		var filteredErrors = stats.GetProperty("filteredErrors").GetInt32();
		var totalErrors = stats.GetProperty("totalErrors").GetInt32();
		var rawOut = $"stdout:\n{stdout}\nstderr:\n{stderr}";
		return new ValidationResult(
			Valid: valid,
			FilteredErrors: filteredErrors,
			TotalErrors: totalErrors,
			RawOutput: rawOut
		);
	}

	static void AssertNoValidationErrors(ValidationResult result, string dsl)
	{
		if (result.FilteredErrors != 0)
		{
			throw new InvalidOperationException(
				$"Expected 0 LikeC4 DSL validation errors but got {result.FilteredErrors}.\n\n"
					+ $"Validator output:\n{result.RawOutput}\n\n"
					+ $"Generated DSL:\n{dsl}"
			);
		}
	}

	// Tests

	[Test]
	public async Task Generate_EmptyModel_ProducesNoValidationErrors(CancellationToken cancellationToken)
	{
		// Arrange

		// Act
		var dsl = LikeC4DSLGenerator.Generate(LikeC4Model.Empty, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_SingleElement_ProducesNoValidationErrors(CancellationToken cancellationToken)
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
				},
			],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	[MethodDataSource(nameof(AllResourceStates))]
	public async Task Generate_EachResourceState_ProducesNoValidationErrors(
		string? state,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "svc",
					Label = "Service",
					Kind = LikeC4ElementKind.Component,
					State = state,
				},
			],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	public static IEnumerable<string?> AllResourceStates() =>
		[
			null,
			KnownResourceStates.Starting,
			KnownResourceStates.Waiting,
			KnownResourceStates.Running,
			KnownResourceStates.Stopping,
			KnownResourceStates.Exited,
			KnownResourceStates.Finished,
			KnownResourceStates.FailedToStart,
			KnownResourceStates.RuntimeUnhealthy,
		];

	[Test]
	public async Task Generate_ElementWithMetadataTagsLinks_ProducesNoValidationErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
					Technology = "ASP.NET Core",
					Description = "Handles HTTP requests",
					Tags = ["backend", "critical"],
					Metadata = [new LikeC4Metadata("version", "1.0.0"), new LikeC4Metadata("owner", "platform-team")],
					Links = [new LikeC4Link("https://example.com/api", "Docs")],
				},
			],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_RelationshipWithKindAndLabel_ProducesNoValidationErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "frontend",
					Label = "Frontend",
					Kind = LikeC4ElementKind.Component,
				},
				new LikeC4Element
				{
					Name = "backend",
					Label = "Backend",
					Kind = LikeC4ElementKind.Component,
				},
			],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "frontend",
					TargetName = "backend",
					Label = "calls",
					Kind = "async",
				},
			],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_NestedElements_ProducesNoValidationErrors(CancellationToken cancellationToken)
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "cloud",
					Label = "Cloud",
					Kind = LikeC4ElementKind.System,
				},
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
					ParentName = "cloud",
				},
				new LikeC4Element
				{
					Name = "db",
					Label = "Database",
					Kind = LikeC4ElementKind.Database,
					ParentName = "cloud",
				},
			],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "api",
					TargetName = "db",
					Label = "reads",
				},
			],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_ElementKindSpecWithStyleAndNotation_ProducesNoValidationErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		AspireC4DiagramOptions options = new()
		{
			Title = "Styled Architecture",
			OutputDirectory = "./likec4",
			FileName = "model.gen",
			ElementKindSpecs =
			[
				new LikeC4ElementKindSpec(LikeC4ElementKind.Component)
				{
					Notation = "Service",
					Technology = "ASP.NET Core",
					Style = new LikeC4ElementKindStyle(null, null, null, null, null)
					{
						Shape = "component",
						Color = "primary",
					},
				},
			],
		};
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
				},
			],
			Relationships = [],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, options);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_MultipleElementsAllStatesSimultaneously_ProducesNoValidationErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// Stress-test: all state variants co-existing in one diagram.
		var elements = AllResourceStates()
			.Select(
				(state, idx) =>
					new LikeC4Element
					{
						Name = $"svc_{idx}",
						Label = $"Service {idx}",
						Kind = LikeC4ElementKind.Component,
						State = state,
					}
			)
			.ToList();
		LikeC4Model model = new() { Elements = elements, Relationships = [] };

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_WithAdditionalExtensionFile_ProducesNoValidationErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		LikeC4Model model = new()
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
				},
				new LikeC4Element
				{
					Name = "db",
					Label = "Database",
					Kind = LikeC4ElementKind.Database,
				},
			],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "api",
					TargetName = "db",
					Label = "reads",
				},
			],
		};

		// Act
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Validate the generated DSL alongside a hand-authored extension file that
		// extends the model with extra metadata and adds custom views.
		const string extensionDsl = """
			model {
			  extend api {
			    link https://example.com/api-docs 'API Docs'
			    metadata { team 'Backend' }
			  }
			}

			views {
			  view api_detail {
			    title 'API Detail'
			    include api
			    include -> api ->
			  }
			}
			""";

		// Assert
		var result = await RunValidateAsync(dsl, cancellationToken, [("extensions.c4", extensionDsl)]);
		AssertNoValidationErrors(result, $"main:\n{dsl}\nextension:\n{extensionDsl}");
		await Assert.That(result.TotalErrors).IsEqualTo(0);
	}
}
