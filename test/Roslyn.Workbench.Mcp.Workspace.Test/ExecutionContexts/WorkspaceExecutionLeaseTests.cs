namespace Roslyn.Workbench.Mcp.Workspace.Test.ExecutionContexts;

public sealed class WorkspaceExecutionLeaseTests
{
    [Fact]
    public async Task GIVEN_AcquiredQueryLease_WHEN_Disposed_THEN_ShouldDisposeOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var context = new Mock<IWorkspaceExecutionContext>();
        var target = WorkspaceExecutionContextLease.Acquired(
    context.Object,
    operationLease.Object);

        await target.DisposeAsync();

        target.Context.Should().NotBeNull();
        target.Failure.Should().BeNull();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AcquiredQueryLeaseWithoutOperationLease_WHEN_Disposed_THEN_ShouldComplete()
    {
        var context = new Mock<IWorkspaceExecutionContext>();
        var target = WorkspaceExecutionContextLease.Acquired(context.Object);

        await target.DisposeAsync();

        target.Context.Should().BeSameAs(context.Object);
        target.Failure.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_RejectedQueryLease_WHEN_Disposed_THEN_ShouldRetainFailureAndContext()
    {
        var context = new Mock<IWorkspaceExecutionContext>();
        var failure = new WorkspaceExecutionFailure { Status = WorkspaceOperationStatus.Rejected };
        var target = WorkspaceExecutionContextLease.Rejected(failure, context.Object);

        await target.DisposeAsync();

        target.Context.Should().BeSameAs(context.Object);
        target.Failure.Should().BeSameAs(failure);
    }

    [Fact]
    public void GIVEN_AcquiredMutationLease_WHEN_InspectingCapabilities_THEN_ShouldKeepStagerSeparateFromContext()
    {
        var context = new Mock<IWorkspaceExecutionContext>();
        var stager = new Mock<IWorkspaceMutationStager>();

        var target = WorkspaceMutationExecutionLease.Acquired(context.Object, stager.Object);

        target.Context.Should().BeSameAs(context.Object);
        target.Stager.Should().BeSameAs(stager.Object);
        target.Failure.Should().BeNull();
        target.HasFailure.Should().BeFalse();
        ((object)context.Object).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    [Fact]
    public async Task GIVEN_AcquiredMutationLeaseWithOperationLease_WHEN_Disposed_THEN_ShouldDisposeOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var context = new Mock<IWorkspaceExecutionContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var target = WorkspaceMutationExecutionLease.Acquired(context.Object, stager.Object, operationLease.Object);

        await target.DisposeAsync();

        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RejectedMutationLeaseWithoutOperationLease_WHEN_Disposed_THEN_ShouldComplete()
    {
        var failure = new WorkspaceExecutionFailure { Status = WorkspaceOperationStatus.Conflict };
        var target = WorkspaceMutationExecutionLease.Rejected(failure);

        await target.DisposeAsync();

        target.Failure.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task GIVEN_RejectedMutationLeaseWithOperationLease_WHEN_Disposed_THEN_ShouldDisposeOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var failure = new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Rejected,
        };
        var target = WorkspaceMutationExecutionLease.Rejected(failure, lease: operationLease.Object);

        await target.DisposeAsync();

        target.Context.Should().BeNull();
        target.Stager.Should().BeNull();
        target.Failure.Should().BeSameAs(failure);
        target.HasFailure.Should().BeTrue();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }
}
