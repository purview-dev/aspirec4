using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// Reads the AspireC4 EF descriptor out of a compiled assembly's metadata.
/// </summary>
/// <remarks>
/// This is a metadata-only read: the assembly is never loaded into an <c>AssemblyLoadContext</c> and no
/// code in it executes. That keeps the AppHost immune to version conflicts and to anything the service
/// assembly might do at module-initialisation time.
/// </remarks>
public static class EfModelReader
{
	/// <summary>The attribute the source generator emits into the analysed assembly.</summary>
	public const string AttributeName = "AspireC4EfModelAttribute";

	/// <summary>Attempts to read the descriptor JSON from an assembly on disk.</summary>
	/// <param name="assemblyPath">Full path to the assembly.</param>
	/// <param name="descriptor">The descriptor JSON, when present.</param>
	/// <returns><see langword="true"/> when the assembly carries a descriptor.</returns>
	public static bool TryRead(string assemblyPath, out string? descriptor)
	{
		descriptor = null;

		if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
		{
			return false;
		}

		try
		{
			using FileStream stream = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using PEReader peReader = new(stream);

			if (!peReader.HasMetadata)
			{
				return false;
			}

			var reader = peReader.GetMetadataReader();

			foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
			{
				if (!IsModelAttribute(reader, reader.GetCustomAttribute(handle), out var attribute))
				{
					continue;
				}

				descriptor = ReadSingleStringArgument(reader, attribute);

				if (descriptor is not null)
				{
					return true;
				}
			}
		}
		catch (BadImageFormatException)
		{
			// Not a managed assembly, or a native/ref-only image — nothing to read.
			return false;
		}
		catch (IOException)
		{
			// The file is being rewritten by a concurrent build; the caller retries on the next pass.
			return false;
		}

		return false;
	}

	static bool IsModelAttribute(MetadataReader reader, CustomAttribute attribute, out CustomAttribute matched)
	{
		matched = attribute;

		var typeName = attribute.Constructor.Kind switch
		{
			HandleKind.MemberReference => GetTypeName(
				reader,
				reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent
			),
			HandleKind.MethodDefinition => reader.GetString(
				reader
					.GetTypeDefinition(
						reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()
					)
					.Name
			),
			_ => null,
		};

		return typeName == AttributeName;
	}

	static string? GetTypeName(MetadataReader reader, EntityHandle handle) =>
		handle.Kind switch
		{
			HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)handle).Name),
			HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
			_ => null,
		};

	/// <summary>
	/// Decodes the single string constructor argument from a custom attribute blob: a two-byte prolog
	/// followed by a length-prefixed UTF-8 string.
	/// </summary>
	static string? ReadSingleStringArgument(MetadataReader reader, CustomAttribute attribute)
	{
		var blob = reader.GetBlobReader(attribute.Value);

		if (blob.Length < 2 || blob.ReadUInt16() != 1)
		{
			return null;
		}

		return blob.ReadSerializedString();
	}
}
