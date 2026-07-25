using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginPackagePathPolicyTests
{
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly Mock<IPhysicalPathContainment> _pathContainment = new();
    private readonly PluginPackagePathPolicy _target;

    public PluginPackagePathPolicyTests()
    {
        _pathComparison.SetupGet(item => item.Comparer).Returns(StringComparer.Ordinal);
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
        _target.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }
}
