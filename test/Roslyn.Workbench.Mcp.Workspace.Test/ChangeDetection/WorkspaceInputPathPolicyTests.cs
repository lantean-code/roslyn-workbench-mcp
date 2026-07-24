using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputPathPolicyTests
{
    [Fact]
    public void GIVEN_ArtifactRoots_WHEN_CheckingPaths_THEN_ShouldExcludeOnlyThoseRoots()
    {
        var target = WorkspaceInputPathPolicy.Create(
            ["/Workspace/Project/custom-bin/", "/Workspace/Project/custom-obj"],
            ["/Workspace/Project/Project.csproj"],
            StringComparison.Ordinal);

        target.ArtifactRoots.Should().BeEquivalentTo(
            "/Workspace/Project/custom-bin",
            "/Workspace/Project/custom-obj");
        target.ShouldTrack("/Workspace/Project/custom-bin").Should().BeFalse();
        target.ShouldTrack("/Workspace/Project/custom-obj/Debug/Generated.cs").Should().BeFalse();
        target.ShouldTrack("/Workspace/Project/bin/Source.cs").Should().BeTrue();
        target.ShouldTrack("/Workspace/Project/obj/Source.cs").Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ArtifactRootContainsProtectedInput_WHEN_CreatingPolicy_THEN_ShouldRetainProtectedTree()
    {
        var target = WorkspaceInputPathPolicy.Create(
            ["/Workspace", "/Workspace/Project", "/Workspace/Project/output"],
            ["/Workspace/Workspace.sln", "/Workspace/Project/Project.csproj"],
            StringComparison.Ordinal);

        target.ArtifactRoots.Should().ContainSingle().Which.Should().Be("/Workspace/Project/output");
        target.ShouldTrack("/Workspace/Project/Project.csproj").Should().BeTrue();
        target.ShouldTrack("/Workspace/Project/output/Generated.cs").Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CaseInsensitivePolicy_WHEN_RootsDifferOnlyByCase_THEN_ShouldDeduplicateAndMatchPaths()
    {
        var target = WorkspaceInputPathPolicy.Create(
            ["/Workspace/Project/Output", "/workspace/project/output"],
            ["/Workspace/Project/Project.csproj"],
            StringComparison.OrdinalIgnoreCase);

        target.ArtifactRoots.Should().ContainSingle();
        target.ShouldTrack("/WORKSPACE/PROJECT/OUTPUT/Generated.cs").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\0")]
    public void GIVEN_UnusablePath_WHEN_CheckingPath_THEN_ShouldTrackConservatively(string? path)
    {
        var target = WorkspaceInputPathPolicy.Create(
            ["/Workspace/Project/output", "\0"],
            ["/Workspace/Project/Project.csproj"],
            StringComparison.Ordinal);

        target.ShouldTrack(path).Should().BeTrue();
    }
}
