namespace Roslyn.Workbench.Mcp.Plugins.Test.Execution;

public sealed class PluginExecutionContextFactoryTests
{
    [Fact]
    public void GIVEN_AcquiredWorkspaceQuery_WHEN_CreatingPluginContext_THEN_ShouldAdaptNarrowContextAndServices()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var services = new Mock<IToolExecutionServices>();
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

        var target = new PluginExecutionContextFactory(workspaceFactory.Object, services.Object);

        var result = target.CreateQueryContext(request, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.ToolExecutionServices.Should().BeSameAs(services.Object);
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

        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, toolExecutionServices.Object);

        var result = target.CreateMutationContext(request, CancellationToken.None);

        result.Context.Should().BeNull();
        result.Failure!.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Failure.Error.Code.Should().Be("Code");
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceQuery_WHEN_CreatingPluginContext_THEN_ShouldMapFailureWithoutContext()
    {
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var request = new TestRequest();
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

        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, toolExecutionServices.Object);

        var result = target.CreateQueryContext(request, CancellationToken.None);

        result.Context.Should().BeNull();
        result.ShortCircuitResult!.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.ShortCircuitResult.Error.Code.Should().Be("Code");
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceQueryWithContext_WHEN_CreatingPluginContext_THEN_ShouldRetainNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var request = new TestRequest();
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

        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, toolExecutionServices.Object);

        var result = target.CreateQueryContext(request, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Context.ToolExecutionServices.Should().BeSameAs(toolExecutionServices.Object);
        result.ShortCircuitResult.Should().NotBeNull();
    }

    [Fact]
    public void GIVEN_AcquiredWorkspaceMutation_WHEN_CreatingPluginContext_THEN_ShouldAdaptNarrowContextAndServices()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var request = new TestRequest();
        workspaceFactory
            .Setup(item => item.CreateMutationContext(null, CancellationToken.None))
            .Returns(WorkspaceMutationExecutionLease.Acquired(
                workspaceContext,
                new Mock<IWorkspaceMutationStager>().Object));

        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, toolExecutionServices.Object);

        var result = target.CreateMutationContext(request, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Context.ToolExecutionServices.Should().BeSameAs(toolExecutionServices.Object);
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RejectedWorkspaceMutationWithContext_WHEN_CreatingPluginContext_THEN_ShouldRetainNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var request = new TestRequest();
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

        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = new PluginExecutionContextFactory(workspaceFactory.Object, toolExecutionServices.Object);

        var result = target.CreateMutationContext(request, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        result.Context.ToolExecutionServices.Should().BeSameAs(toolExecutionServices.Object);
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public void GIVEN_UnexpectedLiveWorkspaceChange_WHEN_DetectingAfterInvocation_THEN_ShouldMapWorkspaceConflict()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var context = new PluginQueryContext(
            CreateWorkspaceContext(roslyn.Solution),
            new Mock<IToolExecutionServices>().Object);
        workspaceFactory
            .Setup(item => item.DetectUnexpectedWorkspaceChange("WorkspaceId"))
            .Returns(new WorkspaceExecutionFailure
            {
                Status = WorkspaceOperationStatus.Conflict,
                Error = new WorkspaceOperationError
                {
                    Code = "WorkspaceOutOfDate",
                    Message = "Message",
                    RequiredAction = RequiredAction.ReloadWorkspace,
                },
            });

        var target = new PluginExecutionContextFactory(
            workspaceFactory.Object,
            new Mock<IToolExecutionServices>().Object);

        var result = target.DetectUnexpectedWorkspaceChange(context);

        result!.Outcome.Should().Be(PluginExecutionOutcome.Conflict);
        result.Error.Code.Should().Be("WorkspaceOutOfDate");
        result.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
    }

    [Fact]
    public void GIVEN_UnchangedLiveWorkspace_WHEN_DetectingAfterInvocation_THEN_ShouldReturnNull()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFactory = new Mock<IWorkspaceExecutionContextFactory>();
        var context = new PluginQueryContext(
            CreateWorkspaceContext(roslyn.Solution),
            new Mock<IToolExecutionServices>().Object);

        var target = new PluginExecutionContextFactory(
            workspaceFactory.Object,
            new Mock<IToolExecutionServices>().Object);

        var result = target.DetectUnexpectedWorkspaceChange(context);

        result.Should().BeNull();
    }

    private sealed record TestRequest : WorkspaceBoundRequest;

    private static WorkspaceExecutionContext CreateWorkspaceContext(Microsoft.CodeAnalysis.Solution solution)
    {
        return new WorkspaceExecutionContext(
            solution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 1,
            },
            new WorkspaceSnapshotIdentity(
                "WorkspaceId",
                1,
                new WorkspaceSnapshotId(1),
                transactionId: null),
            transactionRevision: null,
            defaultMaxResults: 100,
            new Mock<IWorkspaceResolver>().Object);
    }
}
