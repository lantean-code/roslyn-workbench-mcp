using System.Composition;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginAssemblyMetadataReaderIntegrationTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly PluginAssemblyMetadataReader _target;

    public PluginAssemblyMetadataReaderIntegrationTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _fileSystem.SetupGet(static value => value.File).Returns(_file.Object);
        _target = new PluginAssemblyMetadataReader(_fileSystem.Object);
    }

    [Fact]
    public void GIVEN_SingleMarkedAssembly_WHEN_InspectingMetadata_THEN_ShouldReadIdentityAndInformationalVersionWithoutLoadingPlugin()
    {
        var assemblyPath = typeof(BundledCorePlugin).Assembly.Location;
        _file.Setup(value => value.ReadAllBytes(assemblyPath)).Returns(() => File.ReadAllBytes(assemblyPath));

        var result = _target.Inspect(assemblyPath);

        result.Error.Should().BeNull();
        result.IsManagedAssembly.Should().BeTrue();
        var entryPoint = result.EntryPoints.Should().ContainSingle().Subject;
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
        _file.Setup(value => value.ReadAllBytes(assemblyPath)).Returns(() => File.ReadAllBytes(assemblyPath));

        var result = _target.Inspect(assemblyPath);

        result.Error.Should().BeNull();
        result.EntryPoints.Should().HaveCountGreaterThan(1);
        result.EntryPoints.Should().Contain(static entryPoint => entryPoint.PluginId == "test.valid.query");
    }

    [Fact]
    public void GIVEN_ManagedAssemblyWithoutMarker_WHEN_InspectingMetadata_THEN_ShouldReturnNoEntryPoints()
    {
        var assemblyPath = typeof(PluginCatalogLoader).Assembly.Location;
        _file.Setup(value => value.ReadAllBytes(assemblyPath)).Returns(() => File.ReadAllBytes(assemblyPath));

        var result = _target.Inspect(assemblyPath);

        result.Error.Should().BeNull();
        result.IsManagedAssembly.Should().BeTrue();
        result.EntryPoints.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_MalformedAssembly_WHEN_InspectingMetadata_THEN_ShouldReturnDiagnosticInsteadOfThrowing()
    {
        _file.Setup(static value => value.ReadAllBytes("plugin.dll")).Returns([1, 2, 3]);

        var result = _target.Inspect("plugin.dll");

        result.IsManagedAssembly.Should().BeFalse();
        result.EntryPoints.Should().BeEmpty();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GIVEN_MetadataReadFails_WHEN_InspectingMetadata_THEN_ShouldNotPublishExceptionDetails()
    {
        _file.Setup(static value => value.ReadAllBytes("plugin.dll"))
            .Throws(new IOException("Sensitive path"));

        var result = _target.Inspect("plugin.dll");

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

        _file.Setup(static value => value.ReadAllBytes("plugin.dll")).Returns(assemblyBytes);

        var result = _target.Inspect("plugin.dll");

        result.EntryPoints.Should().ContainSingle().Which.Version.Should().BeEmpty();
        result.EntryPoints.Single().EntryTypeName.Should().Be("Plugin");
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

        _file.Setup(static value => value.ReadAllBytes("plugin.dll")).Returns(assemblyBytes);

        var result = _target.Inspect("plugin.dll");

        result.EntryPoints.Should().ContainSingle(entryPoint =>
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
        _file.Setup(static value => value.ReadAllBytes("plugin.dll")).Returns(assemblyBytes);

        var result = _target.Inspect("plugin.dll");

        result.IsManagedAssembly.Should().BeTrue();
        result.EntryPoints.Should().BeEmpty();
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

        _file.Setup(static value => value.ReadAllBytes("plugin.dll")).Returns(assemblyBytes);

        var result = _target.Inspect("plugin.dll");

        result.IsManagedAssembly.Should().BeTrue();
        result.EntryPoints.Should().BeEmpty();
        result.Error.Should().Be("Custom attribute metadata contains a null identity value.");
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
