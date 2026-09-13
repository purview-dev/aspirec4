namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

[GenerateTypeLibrary]
static partial class TypeLibraryGenerator
{
	const string AspireC4Namespace = "Aspire.Hosting.AspireC4";

	[TypeRef(AspireC4Namespace, generateFullNameConst: true)]
	static readonly TypeIdentity LikeC4RegistryAttribute = default;

	[TypeRef(AspireC4Namespace, generateFullNameConst: true)]
	static readonly TypeIdentity SeverityAttribute = default;

	[TypeRef(AspireC4Namespace, generateFullNameConst: true)]
	static readonly TypeIdentity KnownTypeAttribute = default;

	[TypeRef(AspireC4Namespace, generateFullNameConst: true)]
	static readonly TypeIdentity LikeC4RegistryType = default;

	[TypeRef(AspireC4Namespace, generateFullNameConst: true)]
	static readonly TypeIdentity LikeC4Severity = default;

	[TypeRef(AspireC4Namespace)]
	static readonly TypeIdentity LikeC4StrictValidatorGenerator = default;
}
