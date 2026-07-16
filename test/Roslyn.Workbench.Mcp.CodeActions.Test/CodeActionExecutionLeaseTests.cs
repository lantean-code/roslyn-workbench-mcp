using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionExecutionLeaseTests
{
    [Fact]
    public async Task GIVEN_QueryLeaseOwnsWorkspaceOperationLease_WHEN_Disposing_THEN_ShouldDisposeWorkspaceOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            operationLease.Object);
        var target = new CodeActionQueryExecutionLease(workspaceLease, new Mock<ICodeActionQueryContext>().Object, null);

        await target.DisposeAsync();

        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationLeaseOwnsWorkspaceOperationLease_WHEN_Disposing_THEN_ShouldDisposeWorkspaceOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            new Mock<IWorkspaceMutationStager>().Object,
            operationLease.Object);
        var target = new CodeActionMutationExecutionLease(workspaceLease, new Mock<ICodeActionMutationContext>().Object, null);

        await target.DisposeAsync();

        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationStagerReturnsResult_WHEN_StagingCandidate_THEN_ShouldDelegateAndMapResult()
    {
        var candidate = MutationCandidateTestData.CreateWorkspaceCandidate();
        var diagnostics = new[] { new DiagnosticInfo { Id = "DiagnosticId", Message = "Message" } };
        var warnings = new[] { new WarningInfo { Code = "WarningCode", Message = "WarningMessage" } };
        var stager = new Mock<IWorkspaceMutationStager>();
        stager
            .Setup(item => item.StageAsync(
                "OperationName",
                candidate,
                diagnostics,
                warnings,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceOperationResult<MutationStagingOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new MutationStagingOutcome
                {
                    Operation = "Operation",
                    Summary = "Summary",
                },
            });
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object);
        var target = new CodeActionMutationExecutionLease(workspaceLease, new Mock<ICodeActionMutationContext>().Object, null);

        var result = await target.StageAsync(
            "OperationName",
            candidate,
            diagnostics,
            warnings,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Operation.Should().Be("Operation");
        stager.Verify(item => item.StageAsync(
            "OperationName",
            candidate,
            diagnostics,
            warnings,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationLeaseHasNoStager_WHEN_StagingCandidate_THEN_ShouldThrowInvalidOperationException()
    {
        var workspaceLease = WorkspaceMutationExecutionLease.Rejected(new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Rejected,
            Error = new WorkspaceOperationError
            {
                Code = "Code",
                Message = "Message",
            },
        });
        var target = new CodeActionMutationExecutionLease(workspaceLease, null, null);

        Func<Task> action = async () => await target.StageAsync(
            "OperationName",
            MutationCandidateTestData.CreateWorkspaceCandidate(),
            [],
            [],
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }
}
