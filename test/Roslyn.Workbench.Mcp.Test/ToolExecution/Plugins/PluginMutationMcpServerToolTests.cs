using System.Text.Json;

using Roslyn.Workbench.Mcp.Test.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.Plugins;

public sealed class PluginMutationMcpServerToolTests
{
    [Fact]
    public async Task GIVEN_ContextAcquisitionFailure_WHEN_InvokingMutation_THEN_ShouldPublishFailureWithoutCallingHandlerAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var failure = PluginMcpServerToolTestData.CreateExecutionFailure(PluginExecutionOutcome.Rejected, "WorkspaceBusy");
        var workspaceLease = WorkspaceMutationExecutionLease.Rejected(
            new WorkspaceExecutionFailure
            {
                Status = WorkspaceOperationStatus.Rejected,
                Error = new WorkspaceOperationError
                {
                    Code = "WorkspaceBusy",
                    Message = "Message",
                },
            },
            lease: operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(
                It.Is<TestMutationRequest>(request => request.Name == "Name"),
                CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Rejected(workspaceLease, failure));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceBusy");
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestMutationRequest>(),
            It.IsAny<IMutationContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerRejection_WHEN_InvokingMutation_THEN_ShouldPublishFailureWithoutStaging()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(
                It.Is<TestMutationRequest>(request => request.Name == "Name"),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<MutationCandidate>.Rejected(
                new PluginExecutionError
                {
                    Code = "Rejected",
                    Message = "Message",
                },
                RequiredAction.Retry));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("Rejected");
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandlerFailureWithoutError_WHEN_InvokingMutation_THEN_ShouldPublishUnhandledFailure()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new PluginExecutionResult<MutationCandidate>
            {
                Outcome = PluginExecutionOutcome.Faulted,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandlerNoChange_WHEN_InvokingMutation_THEN_ShouldPublishUnstagedSuccess()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<MutationCandidate>.NoChange());
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("staged").GetBoolean().Should().BeFalse();
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandlerSuccessWithoutProposal_WHEN_InvokingMutation_THEN_ShouldPublishUnstagedSuccess()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new PluginExecutionResult<MutationCandidate>
            {
                Outcome = PluginExecutionOutcome.Succeeded,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("staged").GetBoolean().Should().BeFalse();
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandlerProposalAndSuccessfulStaging_WHEN_InvokingMutation_THEN_ShouldStageMappedProposalAndPublishSuccess()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var diagnostic = new DiagnosticInfo
        {
            Id = "Id",
            Message = "Message",
        };
        var handlerWarning = new WarningInfo
        {
            Code = "HandlerWarning",
            Message = "Message",
        };
        var proposalWarning = new WarningInfo
        {
            Code = "ProposalWarning",
            Message = "Message",
        };
        var proposal = new MutationCandidate
        {
            CandidateSolution = MutationCandidateTestData.Solution,
            Summary = "Summary",
            Warnings = [proposalWarning],
        };
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<MutationCandidate>.Success(
                proposal,
                diagnostics: [diagnostic],
                warnings: [handlerWarning]));
        stager
            .Setup(item => item.StageAsync(
                "test-mutation",
                It.Is<WorkspaceMutationCandidate>(candidate =>
                    candidate.CandidateSolution == proposal.CandidateSolution
                    && candidate.Summary == "Summary"
                    && candidate.Warnings.SequenceEqual(new[] { proposalWarning })),
                It.Is<IReadOnlyList<DiagnosticInfo>>(diagnostics => diagnostics.SequenceEqual(new[] { diagnostic })),
                It.Is<IReadOnlyList<WarningInfo>>(warnings => warnings.SequenceEqual(new[] { handlerWarning })),
                CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<MutationStagingOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new MutationStagingOutcome
                {
                    Operation = "test-mutation",
                    Summary = "StagedSummary",
                    Transaction = new TransactionInfo
                    {
                        Revision = 2,
                    },
                },
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("staged").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("summary").GetString().Should().Be("StagedSummary");
        result.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        stager.Verify(item => item.StageAsync(
            "test-mutation",
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            CancellationToken.None), Times.Once);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerProposalAndRejectedStaging_WHEN_InvokingMutation_THEN_ShouldPublishStagingFailure()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
            {
                CandidateSolution = MutationCandidateTestData.Solution,
                Summary = "Summary",
            }));
        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<MutationStagingOutcome>
            {
                Status = WorkspaceOperationStatus.Rejected,
                Error = new WorkspaceOperationError
                {
                    Code = "RevisionCapacityReached",
                    Message = "Message",
                    RequiredAction = RequiredAction.CommitOrRollback,
                },
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("RevisionCapacityReached");
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("CommitOrRollback");
    }

    [Fact]
    public async Task GIVEN_HandlerThrows_WHEN_InvokingMutation_THEN_ShouldPublishUnhandledFailureAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .Returns(ValueTask.FromException<PluginExecutionResult<MutationCandidate>>(new InvalidOperationException("Message")));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerCancellation_WHEN_InvokingMutation_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .Returns(ValueTask.FromCanceled<PluginExecutionResult<MutationCandidate>>(cancellationSource.Token));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StagerThrows_WHEN_InvokingMutation_THEN_ShouldPublishUnhandledFailureAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
            {
                CandidateSolution = MutationCandidateTestData.Solution,
                Summary = "Summary",
            }));
        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .Returns(ValueTask.FromException<WorkspaceOperationResult<MutationStagingOutcome>>(new InvalidOperationException("Message")));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StagerCancellation_WHEN_InvokingMutation_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .ReturnsAsync(PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
            {
                CandidateSolution = MutationCandidateTestData.Solution,
                Summary = "Summary",
            }));
        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                cancellationSource.Token))
            .Returns(ValueTask.FromCanceled<WorkspaceOperationResult<MutationStagingOutcome>>(cancellationSource.Token));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingMutation_THEN_ShouldPublishUnhandledFailureWithoutAcquiringContext()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(42),
        }, CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        contextFactory.Verify(item => item.CreateMutationContext(
            It.IsAny<WorkspaceBoundRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestMutationRequest>(),
            It.IsAny<IMutationContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PluginMutationMcpServerTool<TestMutationRequest> CreateTarget(
        IMutationToolHandler<TestMutationRequest> handler,
        IToolExecutionContextFactory contextFactory)
    {
        var registration = McpServerToolTestData.CreatePluginMutationRegistration(handler, "test-mutation");
        var protocolFactory = McpServerToolTestData.CreateProtocolFactory(
            McpServerToolTestData.CreateProtocolTool("test-mutation"));
        return new PluginMutationMcpServerTool<TestMutationRequest>(
            registration,
            contextFactory,
            protocolFactory.Object,
            McpServerToolTestData.CreateOptions());
    }

    public sealed record TestMutationRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }
}
