namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Contexts;

public sealed class CodeActionExecutionContextFactoryTests
{
    [Fact]
    public void GIVEN_AcquiredWorkspaceQuery_WHEN_CreatingCodeActionContext_THEN_ShouldAdaptNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var resolver = new Mock<IWorkspaceResolver>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, resolver.Object);
        var request = new TestRequest();
        workspaceFactory
            .Setup(item => item.CreateQueryContext(null, CancellationToken.None))
            .Returns(WorkspaceExecutionContextLease.Acquired(workspaceContext));

        var target = new CodeActionExecutionContextFactory(workspaceFactory.Object);

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

        var target = new CodeActionExecutionContextFactory(workspaceFactory.Object);

        var result = target.CreateMutationContext(new TestRequest(), CancellationToken.None);

        result.Context.Should().BeNull();
        result.Failure!.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Failure.Error.Code.Should().Be("Code");
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceQuery_WHEN_CreatingCodeActionContext_THEN_ShouldMapFailureWithoutContext()
    {
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        workspaceFactory
            .Setup(item => item.CreateQueryContext(null, CancellationToken.None))
            .Returns(WorkspaceExecutionContextLease.Rejected(new WorkspaceExecutionFailure
            {
                Status = WorkspaceOperationStatus.Rejected,
                Error = new WorkspaceOperationError
                {
                    Code = "Code",
                    Message = "Message",
                },
            }));

        var target = new CodeActionExecutionContextFactory(workspaceFactory.Object);

        var result = target.CreateQueryContext(new TestRequest(), CancellationToken.None);

        result.Context.Should().BeNull();
        result.Failure!.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Failure.Error.Code.Should().Be("Code");
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceQueryWithContext_WHEN_CreatingCodeActionContext_THEN_ShouldRetainNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, new Mock<IWorkspaceResolver>().Object);
        workspaceFactory
            .Setup(item => item.CreateQueryContext(null, CancellationToken.None))
            .Returns(WorkspaceExecutionContextLease.Rejected(
                new WorkspaceExecutionFailure
                {
                    Status = WorkspaceOperationStatus.Rejected,
                    Error = new WorkspaceOperationError
                    {
                        Code = "Code",
                        Message = "Message",
                    },
                },
                workspaceContext));

        var target = new CodeActionExecutionContextFactory(workspaceFactory.Object);

        var result = target.CreateQueryContext(new TestRequest(), CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public void GIVEN_AcquiredWorkspaceMutation_WHEN_CreatingCodeActionContext_THEN_ShouldAdaptNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var resolver = new Mock<IWorkspaceResolver>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, resolver.Object);
        workspaceFactory
            .Setup(item => item.CreateMutationContext(null, CancellationToken.None))
            .Returns(WorkspaceMutationExecutionLease.Acquired(
                workspaceContext,
                new Mock<IWorkspaceMutationStager>().Object));

        var target = new CodeActionExecutionContextFactory(workspaceFactory.Object);

        var result = target.CreateMutationContext(new TestRequest(), CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceMutationWithContext_WHEN_CreatingCodeActionContext_THEN_ShouldRetainNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, new Mock<IWorkspaceResolver>().Object);
        workspaceFactory
            .Setup(item => item.CreateMutationContext(null, CancellationToken.None))
            .Returns(WorkspaceMutationExecutionLease.Rejected(
                new WorkspaceExecutionFailure
                {
                    Status = WorkspaceOperationStatus.Conflict,
                    Error = new WorkspaceOperationError
                    {
                        Code = "Code",
                        Message = "Message",
                    },
                },
                workspaceContext));

        var target = new CodeActionExecutionContextFactory(workspaceFactory.Object);

        var result = target.CreateMutationContext(new TestRequest(), CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Failure.Should().NotBeNull();
    }

    private sealed record TestRequest : WorkspaceBoundRequest;

    private static WorkspaceExecutionContext CreateWorkspaceContext(
        Solution solution,
        IWorkspaceResolver resolver)
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
            resolver);
    }
}
