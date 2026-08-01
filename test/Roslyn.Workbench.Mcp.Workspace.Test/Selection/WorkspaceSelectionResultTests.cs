using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Selection;

public sealed class WorkspaceSelectionResultTests
{
    [Fact]
    public void GIVEN_WorkspaceSelection_WHEN_CreatingSuccessResult_THEN_ShouldExposeSelectionWithoutError()
    {
        var selection = new WorkspaceSelection
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Session = null!,
        };

        var target = WorkspaceSelectionResult.Success(selection);

        target.HasError.Should().BeFalse();
        target.Selection.Should().BeSameAs(selection);
        target.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_WorkspaceOperationError_WHEN_CreatingFailureResult_THEN_ShouldExposeErrorWithoutSelection()
    {
        var error = new WorkspaceOperationError
        {
            Code = "Code",
            Message = "Message",
            RequiredAction = RequiredAction.ResolveTargetAgain,
        };

        var target = WorkspaceSelectionResult.Failure(error);

        target.HasError.Should().BeTrue();
        target.Error.Should().BeSameAs(error);
        target.Selection.Should().BeNull();
    }
}
