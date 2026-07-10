namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginExecutionContextFactoryTests
{
    [Fact]
    public void GIVEN_AcquiredWorkspaceQuery_WHEN_CreatingPluginContext_THEN_ShouldAdaptNarrowContextAndServices()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var services = Mock.Of<IToolExecutionServices>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var request = new TestRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = "WorkspaceId",
            },
        };
        workspaceFactory
            .Setup(item => item.CreateQueryContext(request.Workspace, CancellationToken.None))
            .Returns(WorkspaceExecutionContextLease.Acquired(workspaceContext));
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, services);

        var result = target.CreateQueryContext(request, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.ToolExecutionServices.Should().BeSameAs(services);
        result.Context.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.ShortCircuitResult.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceMutation_WHEN_CreatingPluginContext_THEN_ShouldMapFailureWithoutContext()
    {
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var request = new TestRequest();
        workspaceFactory
            .Setup(item => item.CreateMutationContext(null, CancellationToken.None))
            .Returns(WorkspaceMutationExecutionLease.Rejected(new WorkspaceExecutionFailure
            {
                Status = WorkspaceOperationStatus.Rejected,
                Error = new WorkspaceOperationError
                {
                    Code = "Code",
                    Message = "Message",
                },
            }));
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, Mock.Of<IToolExecutionServices>());

        var result = target.CreateMutationContext(request, CancellationToken.None);

        result.Context.Should().BeNull();
        result.Failure!.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Failure.Error.Code.Should().Be("Code");
    }

    public sealed record TestRequest : WorkspaceBoundRequest;

    private static WorkspaceExecutionContext CreateWorkspaceContext(Microsoft.CodeAnalysis.Solution solution)
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
