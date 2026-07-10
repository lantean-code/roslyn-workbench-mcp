namespace Roslyn.Workbench.Mcp.Workspace.Test.ExecutionContexts;

public sealed class WorkspaceExecutionLeaseTests
{
    [Fact]
    public async Task GIVEN_AcquiredQueryLease_WHEN_Disposed_THEN_ShouldDisposeOperationLease()
    {
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var target = WorkspaceExecutionContextLease.Acquired(
            Mock.Of<IWorkspaceExecutionContext>(),
            operationLease.Object);

        await target.DisposeAsync();

        target.Context.Should().NotBeNull();
        target.Failure.Should().BeNull();
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void GIVEN_AcquiredMutationLease_WHEN_InspectingCapabilities_THEN_ShouldKeepStagerSeparateFromContext()
    {
        var context = Mock.Of<IWorkspaceExecutionContext>();
        var stager = Mock.Of<IWorkspaceMutationStager>();

        var target = WorkspaceMutationExecutionLease.Acquired(context, stager);

        target.Context.Should().BeSameAs(context);
        target.Stager.Should().BeSameAs(stager);
        target.Failure.Should().BeNull();
        ((object)context).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    [Fact]
    public async Task GIVEN_RejectedMutationLeaseWithOperationLease_WHEN_Disposed_THEN_ShouldDisposeOperationLease()
    {
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var failure = new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Rejected,
        };
        var target = WorkspaceMutationExecutionLease.Rejected(failure, lease: operationLease.Object);

        await target.DisposeAsync();

        target.Context.Should().BeNull();
        target.Stager.Should().BeNull();
        target.Failure.Should().BeSameAs(failure);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }
}
