using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginPackagePathPolicyTests
{
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly Mock<IPhysicalPathContainment> _pathContainment = new();
    private readonly PluginPackagePathPolicy _target;

    public PluginPackagePathPolicyTests()
    {
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        _target = new PluginPackagePathPolicy(
            _pathComparison.Object,
            _pathContainment.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_CandidatePath_WHEN_Validating_THEN_ShouldUseSharedPhysicalContainment(
        bool isContained)
    {
        const string packageDirectory = "/packages/plugin";
        const string candidatePath = "/packages/plugin/lib/tool.dll";
        var expectedPath = isContained ? candidatePath : string.Empty;
        _pathContainment
            .Setup(item => item.TryGetContainedPath(
                packageDirectory,
                candidatePath,
                out It.Ref<string>.IsAny))
            .Returns((string _, string _, out string containedPath) =>
            {
                containedPath = expectedPath;
                return isContained;
            });

        var result = _target.TryGetContainedPath(
            packageDirectory,
            candidatePath,
            out var containedPath);

        result.Should().Be(isContained);
        containedPath.Should().Be(expectedPath);
    }

    [Fact]
    public void GIVEN_Path_WHEN_CreatingKey_THEN_ShouldUseSharedPathComparison()
    {
        const string path = "/packages/plugin";
        var expected = new FileSystemPathKey(path, isCaseSensitive: true);

        var result = _target.CreateKey(path);

        result.Should().Be(expected);
        _pathComparison.Verify(item => item.CreateKey(path), Times.Once);
    }
}
