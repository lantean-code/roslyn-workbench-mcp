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
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            },
        };

        workspaceFactory
            .Setup(item => item.CreateQueryContext(request.Workspace, CancellationToken.None))
            .Returns(WorkspaceExecutionContextLease.Acquired(workspaceContext));

        var target = CreateTarget(workspaceFactory.Object, services.Object);

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
        var request = CreateMutationRequest();
        workspaceFactory
            .Setup(item => item.CreateMutationContext(
                null,
                request.ExpectedSnapshot,
                CancellationToken.None))
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
        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

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
        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

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
        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

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
        var request = CreateMutationRequest();
        workspaceFactory
            .Setup(item => item.CreateMutationContext(
                null,
                request.ExpectedSnapshot,
                CancellationToken.None))
            .Returns(WorkspaceMutationExecutionLease.Acquired(
                workspaceContext,
                new Mock<IWorkspaceMutationStager>().Object));

        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

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
        var request = CreateMutationRequest();
        workspaceFactory
            .Setup(item => item.CreateMutationContext(
                null,
                request.ExpectedSnapshot,
                CancellationToken.None))
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
        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

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
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var queryResultCache = new Mock<IQueryResultCache>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var context = new PluginQueryContext(
            workspaceContext,
            toolExecutionServices.Object,
            queryResultCache.Object);

        workspaceFactory
            .Setup(item => item.DetectUnexpectedWorkspaceChange(Guid.Parse("11111111-1111-1111-1111-111111111111")))
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

        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

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
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var queryResultCache = new Mock<IQueryResultCache>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var context = new PluginQueryContext(
            workspaceContext,
            toolExecutionServices.Object,
            queryResultCache.Object);

        var target = CreateTarget(workspaceFactory.Object, toolExecutionServices.Object);

        var result = target.DetectUnexpectedWorkspaceChange(context);

        result.Should().BeNull();
    }

    private sealed record TestRequest : WorkspaceBoundRequest;

    private sealed record MutationTestRequest : WorkspaceMutationRequest;

    private static MutationTestRequest CreateMutationRequest()
    {
        return new MutationTestRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
        };
    }

    private static PluginExecutionContextFactory CreateTarget(
        IWorkspaceExecutionContextFactory workspaceFactory,
        IToolExecutionServices services)
    {
        var store = new Mock<IPluginQueryCacheStore>();
        store
            .Setup(item => item.CreateScope(
                It.IsAny<WorkspaceSnapshotIdentity>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(CreateCacheScopeIdentity());

        var cacheScopeFactory = new QueryResultCacheScopeFactory(store.Object);
        var target = new PluginExecutionContextFactory(
            workspaceFactory,
            services,
            cacheScopeFactory);

        return target;
    }

    private static QueryCacheScopeIdentity CreateCacheScopeIdentity()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        var scopeIdentity = new QueryCacheScopeIdentity(generation, "Scope");
        return scopeIdentity;
    }

    private static WorkspaceExecutionContext CreateWorkspaceContext(Microsoft.CodeAnalysis.Solution solution)
    {
        var workspacePathService = new Mock<IWorkspacePathService>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 1,
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };
        var snapshotIdentity = new WorkspaceSnapshotIdentity(
            workspaceId,
            1,
            WorkspaceSnapshotTestFactory.CreateId(1),
            transactionId: null);
        var snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(snapshotIdentity, transactionRevision: null);

        return new WorkspaceExecutionContext(
            solution,
            workspaceIdentity,
            snapshotIdentity,
            snapshot,
            transactionRevision: null,
            defaultMaxResults: 100,
            workspacePathService.Object,
            workspaceResolver.Object);
    }
}
