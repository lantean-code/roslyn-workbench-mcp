namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionExecutionContextFactoryTests
{
    [Fact]
    public void GIVEN_AcquiredWorkspaceQuery_WHEN_CreatingCodeActionContext_THEN_ShouldAdaptNarrowContextAndWorkflow()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var queryWorkflow = new Mock<ICodeActionQueryWorkflow>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var request = new TestRequest();
        workspaceFactory
            .Setup(item => item.CreateQueryContext(null, CancellationToken.None))
            .Returns(WorkspaceExecutionContextLease.Acquired(workspaceContext));
        var target = new CodeActionExecutionContextFactory(
            workspaceFactory.Object,
            queryWorkflow.Object,
            Mock.Of<ICodeActionMutationWorkflow>());

        var result = target.CreateQueryContext(request, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceMutation_WHEN_CreatingCodeActionContext_THEN_ShouldMapFailureWithoutContext()
    {
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        workspaceFactory
            .Setup(item => item.CreateMutationContext(null, CancellationToken.None))
            .Returns(WorkspaceMutationExecutionLease.Rejected(new WorkspaceExecutionFailure
            {
                Status = WorkspaceOperationStatus.Conflict,
                Error = new WorkspaceOperationError
                {
                    Code = "Code",
                    Message = "Message",
                },
            }));
        var target = new CodeActionExecutionContextFactory(
            workspaceFactory.Object,
            Mock.Of<ICodeActionQueryWorkflow>(),
            Mock.Of<ICodeActionMutationWorkflow>());

        var result = target.CreateMutationContext(new TestRequest(), CancellationToken.None);

        result.Context.Should().BeNull();
        result.Failure!.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Failure.Error.Code.Should().Be("Code");
    }

    public sealed record TestRequest : WorkspaceBoundRequest;

    private static WorkspaceExecutionContext CreateWorkspaceContext(Solution solution)
    {
        return new WorkspaceExecutionContext(
            solution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 1,
            },
            transactionRevision: null,
            defaultMaxResults: 100,
            Mock.Of<IWorkspaceResolver>());
    }
}
