using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginAssemblyMetadataReader : IPluginAssemblyMetadataReader
{
    private const string _informationalVersionAttributeName = "System.Reflection.AssemblyInformationalVersionAttribute";
    private const string _pluginAttributeName = "Roslyn.Workbench.Mcp.Plugins.RoslynPluginAttribute";

    private readonly IFileSystem _fileSystem;

    public PluginAssemblyMetadataReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public PluginAssemblyInspectionResult Inspect(string assemblyPath)
    {
        try
        {
            using var assemblyStream = _fileSystem.File.OpenRead(assemblyPath);
            using var peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return PluginAssemblyInspectionResult.Skipped();
            }

            var reader = peReader.GetMetadataReader();
            if (!TryReadInformationalVersion(reader, out var version, out var versionError))
            {
                return PluginAssemblyInspectionResult.Failure(versionError);
            }

            if (!TryReadEntryPoints(reader, version, out var entryPoints, out var entryPointError))
            {
                return PluginAssemblyInspectionResult.Failure(entryPointError);
            }

            if (entryPoints.Count == 0)
            {
                return PluginAssemblyInspectionResult.Skipped();
            }

            return PluginAssemblyInspectionResult.Success(entryPoints);
        }
        catch (Exception exception) when (exception is BadImageFormatException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentOutOfRangeException)
        {
            var error = $"Assembly metadata could not be read because {exception.GetType().Name} was raised.";
            return PluginAssemblyInspectionResult.Failure(error);
        }
    }

    private static bool TryReadEntryPoints(
        MetadataReader reader,
        string version,
        out IReadOnlyList<PluginEntryPointMetadata> entryPoints,
        out string error)
    {
        var discoveredEntryPoints = new List<PluginEntryPointMetadata>();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(typeHandle);
            foreach (var attributeHandle in typeDefinition.GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(attributeHandle);
                if (!string.Equals(GetAttributeTypeName(reader, attribute), _pluginAttributeName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryReadFixedStringArguments(reader, attribute, 3, out var values, out error))
                {
                    entryPoints = [];
                    return false;
                }

                discoveredEntryPoints.Add(new PluginEntryPointMetadata
                {
                    PluginId = values[0],
                    DisplayName = values[1],
                    SupportedApiVersion = values[2],
                    Version = version,
                    EntryTypeName = GetTypeName(reader, typeHandle),
                });
            }
        }

        entryPoints = discoveredEntryPoints;
        error = string.Empty;
        return true;
    }

    private static bool TryReadInformationalVersion(MetadataReader reader, out string version, out string error)
    {
        var assembly = reader.GetAssemblyDefinition();
        foreach (var attributeHandle in assembly.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            if (string.Equals(GetAttributeTypeName(reader, attribute), _informationalVersionAttributeName, StringComparison.Ordinal))
            {
                if (!TryReadFixedStringArguments(reader, attribute, 1, out var values, out error))
                {
                    version = string.Empty;
                    return false;
                }

                version = values[0];
                return true;
            }
        }

        version = string.Empty;
        error = string.Empty;
        return true;
    }

    private static bool TryReadFixedStringArguments(
        MetadataReader reader,
        CustomAttribute attribute,
        int count,
        out IReadOnlyList<string> values,
        out string error)
    {
        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.ReadUInt16() != 1)
        {
            values = [];
            error = "Custom attribute metadata has an invalid prologue.";
            return false;
        }

        var parsedValues = new string[count];
        for (var index = 0; index < count; index++)
        {
            var value = blob.ReadSerializedString();
            if (value is null)
            {
                values = [];
                error = "Custom attribute metadata contains a null identity value.";
                return false;
            }

            parsedValues[index] = value;
        }

        values = parsedValues;
        error = string.Empty;
        return true;
    }

    private static string GetAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        return attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => GetMemberReferenceParentName(reader, reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor)),
            HandleKind.MethodDefinition => GetTypeName(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
            _ => string.Empty,
        };
    }

    private static string GetMemberReferenceParentName(MetadataReader reader, MemberReference memberReference)
    {
        return memberReference.Parent.Kind switch
        {
            HandleKind.TypeReference => GetTypeName(reader, (TypeReferenceHandle)memberReference.Parent),
            HandleKind.TypeDefinition => GetTypeName(reader, (TypeDefinitionHandle)memberReference.Parent),
            _ => string.Empty,
        };
    }

    private static string GetTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        return JoinTypeName(reader.GetString(definition.Namespace), reader.GetString(definition.Name));
    }

    private static string GetTypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var reference = reader.GetTypeReference(handle);
        return JoinTypeName(reader.GetString(reference.Namespace), reader.GetString(reference.Name));
    }

    private static string JoinTypeName(string typeNamespace, string name)
    {
        return string.IsNullOrEmpty(typeNamespace) ? name : $"{typeNamespace}.{name}";
    }
}
