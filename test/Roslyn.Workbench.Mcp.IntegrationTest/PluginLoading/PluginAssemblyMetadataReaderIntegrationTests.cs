using System.Buffers.Binary;
using System.Composition;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginAssemblyMetadataReaderIntegrationTests
{
    private const long _largeAssemblyLength = 64L * 1024 * 1024;
    private const long _maximumInspectionAllocation = 8L * 1024 * 1024;

    private readonly PluginAssemblyMetadataReader _target;

    public PluginAssemblyMetadataReaderIntegrationTests()
    {
        var fileSystem = new FileSystem();
        _target = new PluginAssemblyMetadataReader(fileSystem);
    }

    [Fact]
    public void GIVEN_SingleMarkedAssembly_WHEN_InspectingMetadata_THEN_ShouldReadIdentityAndInformationalVersionWithoutLoadingPlugin()
    {
        var assemblyPath = typeof(BundledCorePlugin).Assembly.Location;

        var result = _target.Inspect(assemblyPath);

        result.Succeeded.Should().BeTrue();
        result.WasSkipped.Should().BeFalse();
        result.Failed.Should().BeFalse();
        result.Error.Should().BeNull();
        var entryPoints = GetEntryPoints(result);
        var entryPoint = entryPoints.Should().ContainSingle().Subject;
        entryPoint.PluginId.Should().Be("roslyn.workbench.core");
        entryPoint.DisplayName.Should().Be("Roslyn Workbench Core");
        entryPoint.SupportedApiVersion.Should().Be(PluginApiVersions.V1);
        entryPoint.Version.Should().NotBeNullOrWhiteSpace();
        entryPoint.EntryTypeName.Should().Be(typeof(BundledCorePlugin).FullName);
    }

    [Fact]
    public void GIVEN_AssemblyWithMultipleMarkers_WHEN_InspectingMetadata_THEN_ShouldReturnEveryEntryPoint()
    {
        var assemblyPath = typeof(ValidQueryTestPlugin).Assembly.Location;

        var result = _target.Inspect(assemblyPath);

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        var entryPoints = GetEntryPoints(result);
        entryPoints.Should().HaveCountGreaterThan(1);
        entryPoints.Should().Contain(static entryPoint => entryPoint.PluginId == "test.valid.query");
    }

    [Fact]
    public void GIVEN_ManagedAssemblyWithoutMarker_WHEN_InspectingMetadata_THEN_ShouldSkipAssembly()
    {
        var assemblyPath = typeof(PluginCatalogLoader).Assembly.Location;

        var result = _target.Inspect(assemblyPath);

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
        result.Failed.Should().BeFalse();
        result.Error.Should().BeNull();
        result.EntryPoints.Should().BeNull();
    }

    [Fact]
    public void GIVEN_MalformedAssembly_WHEN_InspectingMetadata_THEN_ShouldReturnDiagnosticInsteadOfThrowing()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WriteAssembly(directory, [1, 2, 3]);

        var result = _target.Inspect(assemblyPath);

        result.Failed.Should().BeTrue();
        result.WasSkipped.Should().BeFalse();
        result.EntryPoints.Should().BeNull();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GIVEN_MetadataReadFails_WHEN_InspectingMetadata_THEN_ShouldNotPublishExceptionDetails()
    {
        var fileSystem = new Mock<IFileSystem>();
        var file = new Mock<IFile>();
        fileSystem.SetupGet(static value => value.File).Returns(file.Object);
        file.Setup(static value => value.OpenRead("plugin.dll"))
            .Throws(new IOException("Sensitive path"));

        var target = new PluginAssemblyMetadataReader(fileSystem.Object);

        var result = target.Inspect("plugin.dll");

        result.Failed.Should().BeTrue();
        result.WasSkipped.Should().BeFalse();
        result.EntryPoints.Should().BeNull();
        result.Error.Should().Contain(nameof(IOException)).And.NotContain("Sensitive path");
    }

    [Fact]
    public void GIVEN_MarkedAssemblyWithoutInformationalVersion_WHEN_InspectingMetadata_THEN_ShouldReturnEmptyVersion()
    {
        var assemblyBytes = CompileAssembly("""
            using Roslyn.Workbench.Mcp.Plugins;

            [RoslynPlugin("PluginId", "DisplayName", PluginApiVersions.V1)]
            public sealed class Plugin : IRoslynPlugin
            {
                public void Configure(IPluginConfiguration configuration)
                {
                }
            }
            """, [typeof(IRoslynPlugin).Assembly.Location, typeof(ExportAttribute).Assembly.Location]);

        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WriteAssembly(directory, assemblyBytes);

        var result = _target.Inspect(assemblyPath);

        result.Succeeded.Should().BeTrue();
        var entryPoints = GetEntryPoints(result);
        entryPoints.Should().ContainSingle().Which.Version.Should().BeEmpty();
        entryPoints.Single().EntryTypeName.Should().Be("Plugin");
    }

    [Fact]
    public void GIVEN_MarkerAttributeDefinedInEntryAssembly_WHEN_InspectingMetadata_THEN_ShouldReadMethodDefinitionConstructor()
    {
        var assemblyBytes = CompileAssembly("""
            using System;

            namespace Roslyn.Workbench.Mcp.Plugins
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class RoslynPluginAttribute : Attribute
                {
                    public RoslynPluginAttribute(string pluginId, string displayName, string supportedApiVersion)
                    {
                    }
                }
            }

            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("PluginId", "DisplayName", "1.0")]
            public sealed class Plugin
            {
            }
            """, []);

        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WriteAssembly(directory, assemblyBytes);

        var result = _target.Inspect(assemblyPath);

        result.Succeeded.Should().BeTrue();
        var entryPoints = GetEntryPoints(result);
        entryPoints.Should().ContainSingle(entryPoint =>
            entryPoint.PluginId == "PluginId"
            && entryPoint.DisplayName == "DisplayName"
            && entryPoint.SupportedApiVersion == "1.0");
    }

    [Fact]
    public void GIVEN_InformationalVersionWithInvalidPrologue_WHEN_InspectingMetadata_THEN_ShouldReturnValidationError()
    {
        var assemblyBytes = CompileAssembly("""
            using System.Reflection;

            [assembly: AssemblyInformationalVersion("VersionMarker")]
            public sealed class Plugin
            {
            }
            """, []);

        CorruptCustomAttributePrologue(assemblyBytes, "VersionMarker");
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WriteAssembly(directory, assemblyBytes);

        var result = _target.Inspect(assemblyPath);

        result.Failed.Should().BeTrue();
        result.WasSkipped.Should().BeFalse();
        result.EntryPoints.Should().BeNull();
        result.Error.Should().Be("Custom attribute metadata has an invalid prologue.");
    }

    [Fact]
    public void GIVEN_PluginMarkerWithNullIdentity_WHEN_InspectingMetadata_THEN_ShouldReturnValidationError()
    {
        var assemblyBytes = CompileAssembly("""
            using Roslyn.Workbench.Mcp.Plugins;

            [RoslynPlugin(null, "DisplayName", PluginApiVersions.V1)]
            public sealed class Plugin : IRoslynPlugin
            {
                public void Configure(IPluginConfiguration configuration)
                {
                }
            }
            """, [typeof(IRoslynPlugin).Assembly.Location, typeof(ExportAttribute).Assembly.Location]);

        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WriteAssembly(directory, assemblyBytes);

        var result = _target.Inspect(assemblyPath);

        result.Failed.Should().BeTrue();
        result.WasSkipped.Should().BeFalse();
        result.EntryPoints.Should().BeNull();
        result.Error.Should().Be("Custom attribute metadata contains a null identity value.");
    }

    [Fact]
    public void GIVEN_LargeManagedAssembly_WHEN_InspectingMetadata_THEN_ShouldNotAllocateTheCompleteFile()
    {
        var assemblyBytes = CompileAssembly("public sealed class Plugin { }", []);
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WritePaddedAssembly(directory, assemblyBytes);

        _target.Inspect(typeof(PluginCatalogLoader).Assembly.Location);
        var allocationBeforeInspection = GC.GetAllocatedBytesForCurrentThread();

        var result = _target.Inspect(assemblyPath);
        var inspectionAllocation = GC.GetAllocatedBytesForCurrentThread() - allocationBeforeInspection;

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
        result.Failed.Should().BeFalse();
        result.Error.Should().BeNull();
        result.EntryPoints.Should().BeNull();
        inspectionAllocation.Should().BeLessThan(_maximumInspectionAllocation);
    }

    [Fact]
    public void GIVEN_LargePeWithoutManagedMetadata_WHEN_InspectingMetadata_THEN_ShouldNotAllocateTheCompleteFile()
    {
        var assemblyBytes = CompileAssembly("public sealed class Plugin { }", []);
        RemoveCorHeaderDirectory(assemblyBytes);
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-metadata-tests");
        var assemblyPath = WritePaddedAssembly(directory, assemblyBytes);

        _target.Inspect(typeof(PluginCatalogLoader).Assembly.Location);
        var allocationBeforeInspection = GC.GetAllocatedBytesForCurrentThread();

        var result = _target.Inspect(assemblyPath);
        var inspectionAllocation = GC.GetAllocatedBytesForCurrentThread() - allocationBeforeInspection;

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
        result.Failed.Should().BeFalse();
        result.Error.Should().BeNull();
        result.EntryPoints.Should().BeNull();
        inspectionAllocation.Should().BeLessThan(_maximumInspectionAllocation);
    }

    private static IReadOnlyList<PluginEntryPointMetadata> GetEntryPoints(PluginAssemblyInspectionResult result)
    {
        return result.EntryPoints
            ?? throw new InvalidOperationException("A successful assembly inspection did not provide its entry points.");
    }

    private static void CorruptCustomAttributePrologue(byte[] assemblyBytes, string serializedValue)
    {
        var valueBytes = Encoding.UTF8.GetBytes(serializedValue);
        var attributePrefix = new byte[valueBytes.Length + 3];
        attributePrefix[0] = 1;
        attributePrefix[2] = checked((byte)valueBytes.Length);
        valueBytes.CopyTo(attributePrefix, 3);
        var prefixOffset = assemblyBytes.AsSpan().IndexOf(attributePrefix);
        if (prefixOffset < 0)
        {
            throw new InvalidOperationException("The custom attribute blob was not found in the test assembly.");
        }

        assemblyBytes[prefixOffset] = 2;
    }

    private static void RemoveCorHeaderDirectory(byte[] assemblyBytes)
    {
        const int peHeaderPointerOffset = 0x3c;
        const int peSignatureAndCoffHeaderLength = 24;
        const int pe32DataDirectoriesOffset = 96;
        const int pe32PlusDataDirectoriesOffset = 112;
        const int corHeaderDirectoryIndex = 14;
        const int dataDirectoryLength = 8;

        var peHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(assemblyBytes.AsSpan(peHeaderPointerOffset));
        var optionalHeaderOffset = peHeaderOffset + peSignatureAndCoffHeaderLength;
        var optionalHeaderMagic = BinaryPrimitives.ReadUInt16LittleEndian(assemblyBytes.AsSpan(optionalHeaderOffset));
        var dataDirectoriesOffset = optionalHeaderMagic switch
        {
            0x10b => pe32DataDirectoriesOffset,
            0x20b => pe32PlusDataDirectoriesOffset,
            _ => throw new InvalidOperationException("The test assembly does not contain a supported PE optional header."),
        };

        var corHeaderDirectoryOffset = optionalHeaderOffset
            + dataDirectoriesOffset
            + (corHeaderDirectoryIndex * dataDirectoryLength);

        assemblyBytes.AsSpan(corHeaderDirectoryOffset, dataDirectoryLength).Clear();
    }

    private static string WriteAssembly(TemporaryDirectory directory, byte[] assemblyBytes)
    {
        var assemblyPath = Path.Combine(directory.DirectoryPath, "plugin.dll");
        File.WriteAllBytes(assemblyPath, assemblyBytes);
        return assemblyPath;
    }

    private static string WritePaddedAssembly(TemporaryDirectory directory, byte[] assemblyBytes)
    {
        var assemblyPath = Path.Combine(directory.DirectoryPath, "large-plugin.dll");
        using var stream = new FileStream(assemblyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(assemblyBytes);
        stream.SetLength(_largeAssemblyLength);
        return assemblyPath;
    }

    private static byte[] CompileAssembly(string source, IReadOnlyList<string> additionalReferencePaths)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");

        var referencePaths = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(additionalReferencePaths);

        var references = referencePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "PluginFixture",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();

        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        return stream.ToArray();
    }
}
