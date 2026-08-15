using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputPathPolicyTests
{
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();

    public WorkspaceInputPathPolicyTests()
    {
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));
    }

    [Fact]
    public void GIVEN_ExcludedDirectoryRoots_WHEN_CheckingPaths_THEN_ShouldExcludeOnlyThoseRoots()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var customBinRoot = Path.Combine(projectRoot, "custom-bin");
        var customObjRoot = Path.Combine(projectRoot, "custom-obj");
        var projectPath = Path.Combine(projectRoot, "Project.csproj");
        var target = WorkspaceInputPathPolicy.Create(
            [customBinRoot + Path.DirectorySeparatorChar, customObjRoot],
            [projectPath],
            _pathComparison.Object);

        target.ExcludedDirectoryRoots.Should().BeEquivalentTo(
            customBinRoot,
            customObjRoot);

        target.ShouldMonitor(customBinRoot).Should().BeFalse();
        target.ShouldMonitor(Path.Combine(customObjRoot, "Debug", "Generated.cs")).Should().BeFalse();
        target.ShouldMonitor(Path.Combine(projectRoot, "bin", "Source.cs")).Should().BeTrue();
        target.ShouldMonitor(Path.Combine(projectRoot, "obj", "Source.cs")).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ExcludedDirectoryRootContainsProtectedInput_WHEN_CreatingPolicy_THEN_ShouldRetainProtectedTree()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var outputRoot = Path.Combine(projectRoot, "output");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var projectPath = Path.Combine(projectRoot, "Project.csproj");
        var target = WorkspaceInputPathPolicy.Create(
            [workspaceRoot, projectRoot, outputRoot],
            [solutionPath, projectPath],
            _pathComparison.Object);

        target.ExcludedDirectoryRoots.Should().ContainSingle().Which.Should().Be(outputRoot);
        target.ShouldMonitor(projectPath).Should().BeTrue();
        target.ShouldMonitor(Path.Combine(outputRoot, "Generated.cs")).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CaseInsensitivePolicy_WHEN_RootsDifferOnlyByCase_THEN_ShouldDeduplicateAndMatchPaths()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var outputRoot = Path.Combine(projectRoot, "Output");
        var upperCaseOutputRoot = outputRoot.ToUpperInvariant();
        var projectPath = Path.Combine(projectRoot, "Project.csproj");
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: false));

        var target = WorkspaceInputPathPolicy.Create(
            [outputRoot, upperCaseOutputRoot],
            [projectPath],
            _pathComparison.Object);

        target.ExcludedDirectoryRoots.Should().ContainSingle();
        target.ShouldMonitor(Path.Combine(outputRoot.ToUpperInvariant(), "Generated.cs")).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\0")]
    public void GIVEN_UnusablePath_WHEN_CheckingPath_THEN_ShouldMonitorConservatively(string? path)
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var target = WorkspaceInputPathPolicy.Create(
            [Path.Combine(projectRoot, "output"), "\0"],
            [Path.Combine(projectRoot, "Project.csproj")],
            _pathComparison.Object);

        target.ShouldMonitor(path).Should().BeTrue();
    }
}
