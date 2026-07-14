using System.IO.Abstractions;
using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginPackageDiscoveryTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IPluginAssemblyMetadataReader> _metadataReader;
    private readonly Mock<IPluginPackagePathPolicy> _packagePathPolicy;

    public PluginPackageDiscoveryTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _metadataReader = new Mock<IPluginAssemblyMetadataReader>();
        _packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        _fileSystem.SetupGet(static value => value.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(static value => value.Path).Returns(_path.Object);
        _packagePathPolicy.SetupGet(static value => value.Comparer).Returns(StringComparer.Ordinal);
    }

    [Fact]
    public void GIVEN_OverlappingRoots_WHEN_Discovering_THEN_ShouldCanonicaliseAndInspectEachImmediatePackageOnce()
    {
        _directory.Setup(static value => value.Exists("root-one")).Returns(true);
        _directory.Setup(static value => value.Exists("root-two")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root-one")).Returns(["package"]);
        _directory.Setup(static value => value.EnumerateDirectories("root-two")).Returns(["package"]);
        var containedPackageDirectory = "/packages/package";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("root-one", "package", out containedPackageDirectory))
            .Returns(true);
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("root-two", "package", out containedPackageDirectory))
            .Returns(true);
        var containedAssemblyPath = "plugin.dll";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("/packages/package", "plugin.dll", out containedAssemblyPath))
            .Returns(true);
        _path.Setup(static value => value.GetFileName("/packages/package")).Returns("package");
        _directory.Setup(static value => value.EnumerateFiles("/packages/package", "*.dll", SearchOption.TopDirectoryOnly)).Returns(["plugin.dll"]);
        _metadataReader.Setup(static value => value.Inspect("plugin.dll")).Returns(CreateInspection("PluginId"));
        var target = CreateTarget();

        var result = target.Discover(["root-one", "root-two", "missing"]);

        result.Should().ContainSingle();
        result.Single().Candidate.Should().NotBeNull();
        result.Single().Candidate!.EntryAssemblyPath.Should().Be("plugin.dll");
        _directory.Verify(static value => value.EnumerateFiles("/packages/package", "*.dll", SearchOption.TopDirectoryOnly), Times.Once);
        _directory.Verify(static value => value.Exists("missing"), Times.Once);
    }

    [Theory]
    [InlineData(0, "does not contain")]
    [InlineData(2, "multiple")]
    public void GIVEN_InvalidMarkerCardinality_WHEN_Discovering_THEN_ShouldDisablePackage(int markerCount, string message)
    {
        ConfigureSinglePackage();
        var entryPoints = Enumerable.Range(0, markerCount)
            .Select(index => CreateEntryPoint($"PluginId{index}"))
            .ToArray();
        _metadataReader.Setup(static value => value.Inspect("plugin.dll")).Returns(new PluginAssemblyInspection
        {
            IsManagedAssembly = true,
            EntryPoints = entryPoints,
        });
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Should().ContainSingle();
        result.Single().Candidate.Should().BeNull();
        result.Single().Error.Should().Contain(message);
        result.Single().FallbackIdentity.Should().Be("package");
    }

    [Fact]
    public void GIVEN_MalformedDependencyMetadata_WHEN_Discovering_THEN_ShouldDisablePackage()
    {
        ConfigureSinglePackage();
        _metadataReader.Setup(static value => value.Inspect("plugin.dll")).Returns(new PluginAssemblyInspection
        {
            Error = "Malformed",
        });
        _path.Setup(static value => value.GetFileName("plugin.dll")).Returns("plugin.dll");
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Single().Candidate.Should().BeNull();
        result.Single().Error.Should().Contain("plugin.dll").And.Contain("Malformed");
    }

    [Fact]
    public void GIVEN_SearchRootCannotBeEnumerated_WHEN_Discovering_THEN_ShouldReturnDiagnostic()
    {
        _directory.Setup(static value => value.Exists("root")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root")).Throws(new IOException("Unavailable"));
        _path.Setup(static value => value.GetFileName("root")).Returns("root");
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Should().ContainSingle();
        result.Single().FallbackIdentity.Should().Be("root");
        result.Single().Error.Should().Contain(nameof(IOException)).And.NotContain("Unavailable");
    }

    [Fact]
    public void GIVEN_PackageCannotBeEnumerated_WHEN_Discovering_THEN_ShouldDisablePackage()
    {
        _directory.Setup(static value => value.Exists("root")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root")).Returns(["package"]);
        var containedPackageDirectory = "package";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("root", "package", out containedPackageDirectory))
            .Returns(true);
        _path.Setup(static value => value.GetFileName("package")).Returns("package");
        _directory
            .Setup(static value => value.EnumerateFiles("package", "*.dll", SearchOption.TopDirectoryOnly))
            .Throws(new UnauthorizedAccessException("Denied"));
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Should().ContainSingle();
        result.Single().FallbackIdentity.Should().Be("package");
        result.Single().Error.Should().Contain(nameof(UnauthorizedAccessException)).And.NotContain("Denied");
    }

    [Fact]
    public void GIVEN_SearchRootThrowsUnexpectedException_WHEN_Discovering_THEN_ShouldNotHideProgrammingFailure()
    {
        _directory.Setup(static value => value.Exists("root")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root")).Throws(new InvalidOperationException("Unexpected"));
        var target = CreateTarget();

        var action = () => target.Discover(["root"]);

        action.Should().Throw<InvalidOperationException>().WithMessage("Unexpected");
    }

    [Fact]
    public void GIVEN_PackageDirectoryOutsideSearchRoot_WHEN_Discovering_THEN_ShouldDisableWithoutInspectingPackage()
    {
        _directory.Setup(static value => value.Exists("root")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root")).Returns(["outside-package"]);
        _path.Setup(static value => value.GetFileName("outside-package")).Returns("outside-package");
        var rejectedPath = string.Empty;
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("root", "outside-package", out rejectedPath))
            .Returns(false);
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Should().ContainSingle(discovery =>
            discovery.FallbackIdentity == "outside-package"
            && discovery.Candidate == null
            && discovery.Error != null
            && discovery.Error.Contains("outside", StringComparison.Ordinal));
        _directory.Verify(
            static value => value.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_AssemblyOutsidePackageDirectory_WHEN_Discovering_THEN_ShouldDisableWithoutReadingMetadata()
    {
        ConfigureSinglePackage();
        var rejectedPath = string.Empty;
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("package", "plugin.dll", out rejectedPath))
            .Returns(false);
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Should().ContainSingle(discovery =>
            discovery.Candidate == null
            && discovery.Error != null
            && discovery.Error.Contains("outside", StringComparison.Ordinal));
        _metadataReader.Verify(static value => value.Inspect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_AssembliesReturnedOutOfOrder_WHEN_Discovering_THEN_ShouldInspectByOrdinalPath()
    {
        _directory.Setup(static value => value.Exists("root")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root")).Returns(["package"]);
        var containedPackageDirectory = "package";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("root", "package", out containedPackageDirectory))
            .Returns(true);
        _path.Setup(static value => value.GetFileName("package")).Returns("package");
        _directory
            .Setup(static value => value.EnumerateFiles("package", "*.dll", SearchOption.TopDirectoryOnly))
            .Returns(["z.dll", "a.dll"]);
        var containedFirstAssemblyPath = "a.dll";
        var containedSecondAssemblyPath = "z.dll";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("package", "a.dll", out containedFirstAssemblyPath))
            .Returns(true);
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("package", "z.dll", out containedSecondAssemblyPath))
            .Returns(true);
        var sequence = new MockSequence();
        _metadataReader.InSequence(sequence).Setup(static value => value.Inspect("a.dll")).Returns(new PluginAssemblyInspection());
        _metadataReader.InSequence(sequence).Setup(static value => value.Inspect("z.dll")).Returns(new PluginAssemblyInspection());
        var target = CreateTarget();

        var result = target.Discover(["root"]);

        result.Should().ContainSingle(discovery =>
            discovery.Candidate == null
            && discovery.Error != null
            && discovery.Error.Contains("does not contain", StringComparison.Ordinal));
        _metadataReader.Verify(static value => value.Inspect("a.dll"), Times.Once);
        _metadataReader.Verify(static value => value.Inspect("z.dll"), Times.Once);
    }

    private PluginPackageDiscovery CreateTarget()
    {
        return new PluginPackageDiscovery(_fileSystem.Object, _metadataReader.Object, _packagePathPolicy.Object);
    }

    private void ConfigureSinglePackage()
    {
        _directory.Setup(static value => value.Exists("root")).Returns(true);
        _directory.Setup(static value => value.EnumerateDirectories("root")).Returns(["package"]);
        var containedPackageDirectory = "package";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("root", "package", out containedPackageDirectory))
            .Returns(true);
        _path.Setup(static value => value.GetFileName("package")).Returns("package");
        _directory.Setup(static value => value.EnumerateFiles("package", "*.dll", SearchOption.TopDirectoryOnly)).Returns(["plugin.dll"]);
        var containedAssemblyPath = "plugin.dll";
        _packagePathPolicy
            .Setup(value => value.TryGetContainedPath("package", "plugin.dll", out containedAssemblyPath))
            .Returns(true);
    }

    private static PluginAssemblyInspection CreateInspection(string pluginId)
    {
        return new PluginAssemblyInspection
        {
            IsManagedAssembly = true,
            EntryPoints = [CreateEntryPoint(pluginId)],
        };
    }

    private static PluginEntryPointMetadata CreateEntryPoint(string pluginId)
    {
        return new PluginEntryPointMetadata
        {
            PluginId = pluginId,
            DisplayName = "DisplayName",
            SupportedApiVersion = PluginApiVersions.V1,
            Version = "1.0.0",
            EntryTypeName = "EntryTypeName",
        };
    }
}
