namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class WorkspaceProjectTargetFrameworkMapTests
{
    [Fact]
    public void GIVEN_MappedProject_WHEN_MatchingTargetFramework_THEN_ShouldUseCaseInsensitiveComparison()
    {
        var projectId = ProjectId.CreateNewId();
        var mappings = new Dictionary<ProjectId, string>
        {
            [projectId] = "net10.0",
        };
        var target = new WorkspaceProjectTargetFrameworkMap(mappings);

        var matching = target.Matches(projectId, "NET10.0");
        var different = target.Matches(projectId, "net8.0");
        var unknown = target.Matches(ProjectId.CreateNewId(), "net10.0");

        matching.Should().BeTrue();
        different.Should().BeFalse();
        unknown.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_SourceMappingChangesAfterConstruction_WHEN_Matching_THEN_ShouldRetainCapturedMapping()
    {
        var projectId = ProjectId.CreateNewId();
        var mappings = new Dictionary<ProjectId, string>
        {
            [projectId] = "net10.0",
        };
        var target = new WorkspaceProjectTargetFrameworkMap(mappings);

        mappings[projectId] = "net8.0";

        target.Matches(projectId, "net10.0").Should().BeTrue();
        target.Matches(projectId, "net8.0").Should().BeFalse();
    }
}
