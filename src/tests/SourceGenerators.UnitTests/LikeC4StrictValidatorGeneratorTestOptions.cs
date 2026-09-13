using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

namespace Aspire.Hosting.AspireC4.SourceGenerators;

/// <summary>
/// Options used by every strict-validator test. Seeds the fluent call-site extension stubs so the
/// sampled sources (<c>.WithTag(...)</c>, <c>.WithKind(...)</c>, ...) compile, and pins the
/// generator-disable property name.
/// </summary>
public sealed record LikeC4StrictValidatorGeneratorTestOptions : SourceGeneratorTestOptions
{
	public LikeC4StrictValidatorGeneratorTestOptions()
	{
		AdditionalSources = AdditionalSources.Add(CallSiteExtensionsSource);
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableSourceGenerator;
	}

	/// <summary>
	/// Minimal fluent call-site surface so the generator's syntax probe has real invocations to find.
	/// The generator only inspects the invoked member name and the first argument's constant value, so
	/// these stubs do not need to model the real <c>AspireC4.Hosting</c> annotation builders.
	/// </summary>
	internal const string CallSiteExtensionsSource = """
		namespace Aspire.Hosting.AspireC4;

		public static class TestCallSiteExtensions
		{
			public static object WithTag(this object source, string tag) => source;
			public static object WithKind(this object source, string kind) => source;
			public static object WithLikeC4Group(this object source, string group) => source;
			public static object WithMetadata(this object source, string key, string value) => source;
		}
		""";
}
