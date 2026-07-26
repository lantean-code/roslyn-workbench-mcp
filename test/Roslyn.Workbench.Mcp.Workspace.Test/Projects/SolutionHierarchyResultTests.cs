namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects;

public sealed class SolutionHierarchyResultTests
{
    [Fact]
    public void GIVEN_NullError_WHEN_CreatingFailedResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => SolutionHierarchyResult.Failed(errorMessage: null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("errorMessage");
    }
}
