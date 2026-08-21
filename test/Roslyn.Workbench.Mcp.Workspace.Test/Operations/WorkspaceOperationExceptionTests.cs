namespace Roslyn.Workbench.Mcp.Workspace.Test.Operations;

public sealed class WorkspaceOperationExceptionTests
{
    [Fact]
    public void GIVEN_WorkspaceFailureContext_WHEN_CreatingException_THEN_ShouldRetainContextAndInnerException()
    {
        var context = WorkspaceSnapshotTestFactory.CreateFailureContext(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var failure = new InvalidOperationException("Failure");
        var result = new WorkspaceOperationException(context, failure);

        result.Context.Should().BeSameAs(context);
        result.InnerException.Should().BeSameAs(failure);
        result.Message.Should().Be("A Workspace operation failed after resolving its target.");
    }
}
