using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginExecutionContextTests
{
    [Fact]
    public async Task GIVEN_MutationLeaseOwnsWorkspaceOperationLease_WHEN_Disposing_THEN_ShouldDisposeWorkspaceOperationLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceContext = new Mock<IWorkspaceExecutionContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var context = new Mock<IMutationContext>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            workspaceContext.Object,
            stager.Object,
            operationLease.Object);
        var target = PluginMutationExecutionLease.Acquired(workspaceLease, context.Object);

        await target.DisposeAsync();

        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_WorkspaceContext_WHEN_AdaptingQueryContext_THEN_ShouldExposePluginServicesWithoutStagingCapability()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var services = new Mock<IToolExecutionServices>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);

        var target = new PluginQueryContext(workspaceContext, services.Object);

        target.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        target.WorkspaceIdentity.Should().BeSameAs(workspaceContext.WorkspaceIdentity);
        target.TransactionRevision.Should().Be(2);
        target.DefaultMaxResults.Should().Be(100);
        target.WorkspaceResolver.Should().BeSameAs(workspaceContext.WorkspaceResolver);
        target.ToolExecutionServices.Should().BeSameAs(services.Object);
        ((object)target).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    [Fact]
    public async Task GIVEN_MutationCandidate_WHEN_StagingThroughPluginLease_THEN_ShouldMapProposalAndSuccessfulResult()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var proposal = new MutationCandidate
        {
            CandidateSolution = roslyn.Solution,
            Summary = "Summary",
        };
        var outcome = new MutationStagingOutcome
        {
            Operation = "Operation",
            Summary = "Summary",
            Transaction = new TransactionInfo
            {
                Revision = 1,
            },
        };
        stager
            .Setup(item => item.StageAsync(
                "Operation",
                It.Is<WorkspaceMutationCandidate>(candidate => candidate.CandidateSolution == roslyn.Solution && candidate.Summary == "Summary"),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<MutationStagingOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = outcome,
            });
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(workspaceContext, stager.Object);
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var target = PluginMutationExecutionLease.Acquired(
            workspaceLease,
            new PluginMutationContext(workspaceContext, toolExecutionServices.Object));

        var result = await target.StageAsync("Operation", proposal, [], [], CancellationToken.None);

        target.HasFailure.Should().BeFalse();
        target.Context.Should().BeOfType<PluginMutationContext>();
        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Operation.Should().Be("Operation");
        result.Data.Transaction!.Revision.Should().Be(1);
        stager.Verify(item => item.StageAsync(
            "Operation",
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RejectedMutationLease_WHEN_StagingCandidate_THEN_ShouldRejectInvalidUse()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceFailure = new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Rejected,
            Error = new WorkspaceOperationError
            {
                Code = "Code",
                Message = "Message",
            },
        };
        var workspaceLease = WorkspaceMutationExecutionLease.Rejected(workspaceFailure);
        var failure = PluginWorkspaceResultMapper.MapFailure(workspaceFailure);
        var target = PluginMutationExecutionLease.Rejected(workspaceLease, failure);

        Func<Task> action = async () => await target.StageAsync(
            "Operation",
            new MutationCandidate
            {
                CandidateSolution = roslyn.Solution,
            },
            [],
            [],
            CancellationToken.None);

        target.HasFailure.Should().BeTrue();
        target.Failure.Should().BeSameAs(failure);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_WorkspaceFailure_WHEN_MappingForPlugin_THEN_ShouldPreserveFailureDetails()
    {
        var failure = new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Conflict,
            Error = new WorkspaceOperationError
            {
                Code = "Code",
                Message = "Message",
                RequiredAction = RequiredAction.Retry,
            },
        };

        var result = PluginWorkspaceResultMapper.MapFailure(failure);

        result!.Outcome.Should().Be(PluginExecutionOutcome.Conflict);
        result.Error.Code.Should().Be("Code");
        result.Error.Message.Should().Be("Message");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
    }

    private static WorkspaceExecutionContext CreateWorkspaceContext(Microsoft.CodeAnalysis.Solution solution)
    {
        return new WorkspaceExecutionContext(
            solution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 1,
            },
            transactionRevision: 2,
            defaultMaxResults: 100,
            new Mock<IWorkspaceResolver>().Object);
    }
}
