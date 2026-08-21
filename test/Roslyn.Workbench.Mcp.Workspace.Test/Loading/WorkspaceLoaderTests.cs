using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceLoaderTests
{
    private readonly Mock<IWorkspaceProjectCompatibilityInspector> _compatibilityInspector;
    private readonly Mock<IMsBuildWorkspaceFactory> _workspaceFactory;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly Mock<IWorkspacePathNormalizer> _pathNormalizer;
    private readonly WorkspaceLoader _target;

    public WorkspaceLoaderTests()
    {
        _compatibilityInspector = new Mock<IWorkspaceProjectCompatibilityInspector>();
        _workspaceFactory = new Mock<IMsBuildWorkspaceFactory>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathNormalizer = new Mock<IWorkspacePathNormalizer>();
        _pathNormalizer
            .Setup(item => item.TryGetFullPath(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string path, out string fullPath) =>
            {
                try
                {
                    fullPath = Path.GetFullPath(path);
                    return true;
                }
                catch (ArgumentException)
                {
                    fullPath = string.Empty;
                    return false;
                }
            });

        _target = new WorkspaceLoader(
            _workspaceFactory.Object,
            _compatibilityInspector.Object,
            _pathComparison.Object,
            _pathNormalizer.Object);
    }

    [Theory]
    [InlineData("Solution.sln")]
    [InlineData("Solution.slnx")]
    [InlineData("Project.csproj")]
    [InlineData("Solution.SLN")]
    [InlineData("Solution.SLNX")]
    [InlineData("Project.CSPROJ")]
    public void GIVEN_RootedSupportedPath_WHEN_NormalisingOpenPath_THEN_ShouldReturnFullPath(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkspaceLoaderTests", fileName);

        var result = _target.NormalizeOpenPath(path);

        result.Should().Be(Path.GetFullPath(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/Project.csproj")]
    public void GIVEN_BlankOrRelativePath_WHEN_NormalisingOpenPath_THEN_ShouldReturnNull(string path)
    {
        var result = _target.NormalizeOpenPath(path);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RootedUnsupportedPath_WHEN_NormalisingOpenPath_THEN_ShouldReturnNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkspaceLoaderTests", "Document.cs");

        var result = _target.NormalizeOpenPath(path);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RootedMalformedPath_WHEN_NormalisingOpenPath_THEN_ShouldReturnNull()
    {
        var path = Path.GetPathRoot(Path.GetTempPath()) + "\0Project.csproj";

        var result = _target.NormalizeOpenPath(path);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" Alias ", "Alias")]
    public void GIVEN_Alias_WHEN_NormalisingAlias_THEN_ShouldReturnExpectedValue(string? alias, string? expected)
    {
        var result = _target.NormalizeAlias(alias);

        result.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_ProjectPath_WHEN_InspectingCompatibility_THEN_ShouldReturnInspectorResult()
    {
        var expected = (IsSdkStyle: true, Diagnostics: (IReadOnlyList<DiagnosticInfo>)Array.Empty<DiagnosticInfo>());
        var properties = new WorkspaceMsBuildProperties
        {
            Configuration = "Release",
        };

        _compatibilityInspector.Setup(item => item.Inspect("ProjectPath", properties)).Returns(expected);

        var result = _target.InspectCompatibility("ProjectPath", properties);

        result.Should().BeEquivalentTo(expected);
        _compatibilityInspector.Verify(item => item.Inspect("ProjectPath", properties), Times.Once);
    }
}
