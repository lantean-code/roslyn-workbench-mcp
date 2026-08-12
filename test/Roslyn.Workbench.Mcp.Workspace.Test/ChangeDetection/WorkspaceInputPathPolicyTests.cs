using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputPathPolicyTests
{
    [Fact]
    public void GIVEN_ArtifactRoots_WHEN_CheckingPaths_THEN_ShouldExcludeOnlyThoseRoots()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var customBinRoot = Path.Combine(projectRoot, "custom-bin");
        var customObjRoot = Path.Combine(projectRoot, "custom-obj");
        var projectPath = Path.Combine(projectRoot, "Project.csproj");
        var target = WorkspaceInputPathPolicy.Create(
            [customBinRoot + Path.DirectorySeparatorChar, customObjRoot],
            [projectPath],
            StringComparison.Ordinal);

        target.ArtifactRoots.Should().BeEquivalentTo(
            customBinRoot,
            customObjRoot);

        target.ShouldTrack(customBinRoot).Should().BeFalse();
        target.ShouldTrack(Path.Combine(customObjRoot, "Debug", "Generated.cs")).Should().BeFalse();
        target.ShouldTrack(Path.Combine(projectRoot, "bin", "Source.cs")).Should().BeTrue();
        target.ShouldTrack(Path.Combine(projectRoot, "obj", "Source.cs")).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ArtifactRootContainsProtectedInput_WHEN_CreatingPolicy_THEN_ShouldRetainProtectedTree()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var outputRoot = Path.Combine(projectRoot, "output");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var projectPath = Path.Combine(projectRoot, "Project.csproj");
        var target = WorkspaceInputPathPolicy.Create(
            [workspaceRoot, projectRoot, outputRoot],
            [solutionPath, projectPath],
            StringComparison.Ordinal);

        target.ArtifactRoots.Should().ContainSingle().Which.Should().Be(outputRoot);
        target.ShouldTrack(projectPath).Should().BeTrue();
        target.ShouldTrack(Path.Combine(outputRoot, "Generated.cs")).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CaseInsensitivePolicy_WHEN_RootsDifferOnlyByCase_THEN_ShouldDeduplicateAndMatchPaths()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var outputRoot = Path.Combine(projectRoot, "Output");
        var upperCaseOutputRoot = outputRoot.ToUpperInvariant();
        var projectPath = Path.Combine(projectRoot, "Project.csproj");
        var target = WorkspaceInputPathPolicy.Create(
            [outputRoot, upperCaseOutputRoot],
            [projectPath],
            StringComparison.OrdinalIgnoreCase);

        target.ArtifactRoots.Should().ContainSingle();
        target.ShouldTrack(Path.Combine(outputRoot.ToUpperInvariant(), "Generated.cs")).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\0")]
    public void GIVEN_UnusablePath_WHEN_CheckingPath_THEN_ShouldTrackConservatively(string? path)
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectRoot = Path.Combine(workspaceRoot, "Project");
        var target = WorkspaceInputPathPolicy.Create(
            [Path.Combine(projectRoot, "output"), "\0"],
            [Path.Combine(projectRoot, "Project.csproj")],
            StringComparison.Ordinal);

        target.ShouldTrack(path).Should().BeTrue();
    }
}
