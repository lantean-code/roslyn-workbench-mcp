using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Contexts;

public sealed class CodeActionExecutionLeaseTests
{
    [Fact]
    public async Task GIVEN_QueryLeaseOwnsWorkspaceOperationLease_WHEN_Disposing_THEN_ShouldDisposeWorkspaceOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            operationLease.Object);
        var target = CodeActionQueryExecutionLease.Acquired(
            workspaceLease,
            new Mock<ICodeActionQueryContext>().Object);

        await target.DisposeAsync();

        target.HasFailure.Should().BeFalse();
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
        var target = CodeActionMutationExecutionLease.Acquired(workspaceLease, new Mock<ICodeActionMutationContext>().Object);

        await target.DisposeAsync();

        target.HasFailure.Should().BeFalse();
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
            .ReturnsAsync(WorkspaceOperationResult<MutationStagingOutcome>.Succeeded(new MutationStagingOutcome
            {
                Operation = "Operation",
                Summary = "Summary",
            }));
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object);
        var target = CodeActionMutationExecutionLease.Acquired(workspaceLease, new Mock<ICodeActionMutationContext>().Object);

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
        var target = CodeActionMutationExecutionLease.Rejected(
            workspaceLease,
            new CodeActionExecutionFailure
            {
                Outcome = CodeActionExecutionOutcome.Rejected,
                Error = new CodeActionExecutionError
                {
                    Code = "Code",
                    Message = "Message",
                },
            });

        Func<Task> action = async () => await target.StageAsync(
            "OperationName",
            MutationCandidateTestData.CreateWorkspaceCandidate(),
            [],
            [],
            TestContext.Current.CancellationToken);

        target.HasFailure.Should().BeTrue();
        await action.Should().ThrowAsync<InvalidOperationException>();
    }
}
