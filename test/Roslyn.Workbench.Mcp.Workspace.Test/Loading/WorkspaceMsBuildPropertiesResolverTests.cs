using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceMsBuildPropertiesResolverTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IPath> _path;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IWorkspacePathNormalizer> _pathNormalizer;
    private readonly WorkspaceMsBuildPropertiesResolver _target;

    public WorkspaceMsBuildPropertiesResolverTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _path = new Mock<IPath>();
        _directory = new Mock<IDirectory>();
        _pathNormalizer = new Mock<IWorkspacePathNormalizer>();
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);

        _target = new WorkspaceMsBuildPropertiesResolver(
            _fileSystem.Object,
            _pathNormalizer.Object);
    }

    [Fact]
    public void GIVEN_NoProperties_WHEN_Resolving_THEN_ShouldReturnNoEffectiveProperties()
    {
        var result = _target.Resolve(properties: null);

        result.HasError.Should().BeFalse();
        result.Properties.Should().BeNull();
    }

    [Fact]
    public void GIVEN_EmptyProperties_WHEN_Resolving_THEN_ShouldReturnNoEffectiveProperties()
    {
        var result = _target.Resolve(new WorkspaceMsBuildProperties());

        result.HasError.Should().BeFalse();
        result.Properties.Should().BeNull();
    }

    [Fact]
    public void GIVEN_AllowlistedProperties_WHEN_Resolving_THEN_ShouldNormaliseAndRetainThem()
    {
        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = "/Artifacts",
            Configuration = " Configuration ",
            Platform = " Platform ",
            RuntimeIdentifier = " RuntimeIdentifier ",
            TargetFramework = " TargetFramework ",
        };

        _path.Setup(item => item.IsPathFullyQualified("/Artifacts")).Returns(true);
        _pathNormalizer
            .Setup(item => item.TryGetFullPath("/Artifacts", out It.Ref<string>.IsAny))
            .Returns((string _, out string path) =>
            {
                path = "/NormalisedArtifacts";
                return true;
            });

        _directory.Setup(item => item.Exists("/NormalisedArtifacts")).Returns(true);

        var result = _target.Resolve(properties);

        result.HasError.Should().BeFalse();
        result.Properties.Should().Be(new WorkspaceMsBuildProperties
        {
            ArtifactsPath = "/NormalisedArtifacts",
            Configuration = "Configuration",
            Platform = "Platform",
            RuntimeIdentifier = "RuntimeIdentifier",
            TargetFramework = "TargetFramework",
        });

        result.Properties!.ToGlobalProperties().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["ArtifactsPath"] = "/NormalisedArtifacts",
            ["Configuration"] = "Configuration",
            ["Platform"] = "Platform",
            ["RuntimeIdentifier"] = "RuntimeIdentifier",
            ["TargetFramework"] = "TargetFramework",
        });
    }

    [Theory]
    [InlineData("Configuration")]
    [InlineData("Platform")]
    [InlineData("RuntimeIdentifier")]
    [InlineData("TargetFramework")]
    public void GIVEN_WhitespaceProperty_WHEN_Resolving_THEN_ShouldRejectIt(string propertyName)
    {
        var properties = new WorkspaceMsBuildProperties
        {
            Configuration = propertyName == "Configuration" ? " " : null,
            Platform = propertyName == "Platform" ? " " : null,
            RuntimeIdentifier = propertyName == "RuntimeIdentifier" ? " " : null,
            TargetFramework = propertyName == "TargetFramework" ? " " : null,
        };

        var result = _target.Resolve(properties);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceMsBuildPropertiesInvalid");
    }

    [Theory]
    [InlineData(" ", false, true, true)]
    [InlineData("RelativeArtifacts", false, true, true)]
    [InlineData("/Artifacts", true, false, true)]
    [InlineData("/Artifacts", true, true, false)]
    public void GIVEN_InvalidArtifactsPath_WHEN_Resolving_THEN_ShouldRejectIt(
        string artifactsPath,
        bool isFullyQualified,
        bool canNormalise,
        bool exists)
    {
        _path.Setup(item => item.IsPathFullyQualified(artifactsPath)).Returns(isFullyQualified);
        _pathNormalizer
            .Setup(item => item.TryGetFullPath(artifactsPath, out It.Ref<string>.IsAny))
            .Returns((string _, out string path) =>
            {
                path = "/NormalisedArtifacts";
                return canNormalise;
            });

        _directory.Setup(item => item.Exists("/NormalisedArtifacts")).Returns(exists);
        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = artifactsPath,
        };

        var result = _target.Resolve(properties);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceMsBuildPropertiesInvalid");
    }
}
