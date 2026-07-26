namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects;

public sealed class ProjectTargetFrameworksResultTests
{
    [Fact]
    public void GIVEN_NullError_WHEN_CreatingFailedResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => ProjectTargetFrameworksResult.Failed(errorMessage: null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("errorMessage");
    }
}
