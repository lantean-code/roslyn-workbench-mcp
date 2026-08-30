using System.Text.Json;
using Microsoft.CodeAnalysis;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.Plugins;

public sealed class PluginMutationMcpServerToolTests : IDisposable
{
    private readonly AdhocWorkspace _roslynWorkspace;
    private readonly Mock<IToolRequestBinder> _requestBinder;

    public PluginMutationMcpServerToolTests()
    {
        _roslynWorkspace = new AdhocWorkspace();
        _requestBinder = new Mock<IToolRequestBinder>();
        var request = new TestMutationRequest
        {
            Name = "Name",
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
        };
        string? errorMessage = null;
        _requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(true);
    }

    public void Dispose()
    {
        _roslynWorkspace.Dispose();
    }

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

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

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
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(
                It.Is<TestMutationRequest>(request => request.Name == "Name"),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult.Rejected<MutationCandidate>(
                new PluginExecutionError
                {
                    Code = "Rejected",
                    Message = "Message",
                },
                RequiredAction.Retry));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("Rejected");
        result.StructuredContent.Value.GetProperty("continuation").GetProperty("kind").GetString().Should().Be("RetryRequest");
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
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult.NoChange<MutationCandidate>());

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("data").GetProperty("staged").GetBoolean().Should().BeFalse();
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandlerChangesLiveWorkspace_WHEN_InvokingMutation_THEN_ShouldPublishContainmentFailureWithoutStaging()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object);
        var failure = PluginMcpServerToolTestData.CreateExecutionFailure(
            PluginExecutionOutcome.Conflict,
            "TransactionConflicted");
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));
        contextFactory
            .Setup(item => item.DetectUnexpectedWorkspaceChange(context.Object))
            .Returns(failure);

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult.NoChange<MutationCandidate>());

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("TransactionConflicted");
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
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
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
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult.Success(
                proposal,
                diagnostics: [diagnostic],
                warnings: [handlerWarning]));

        var stagingOutcome = new MutationStagingOutcome
        {
            Operation = "test-mutation",
            Summary = "StagedSummary",
            Transaction = new TransactionInfo
            {
                Revision = 2,
            },
            Preview = new MutationPreview
            {
                Summary = "StagedSummary",
            },
        };
        var stagingContext = WorkspaceSnapshotTestFactory.CreateContext(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            workspaceEpoch: 3,
            snapshotId: 2,
            transactionRevision: 2);

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
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(stagingOutcome, stagingContext));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        var data = result.StructuredContent.Value.GetProperty("data");
        data.GetProperty("staged").GetBoolean().Should().BeTrue();
        data.GetProperty("summary").GetString().Should().Be("StagedSummary");
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        result.StructuredContent.Value.GetProperty("snapshot").GetProperty("snapshotId").GetGuid()
            .Should().Be(WorkspaceSnapshotTestFactory.CreateGuid(2));
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
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult.Success(new MutationCandidate
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
            .ReturnsAsync(WorkspaceOperationResult.Rejected<MutationStagingOutcome>(new WorkspaceOperationError
            {
                Code = "RevisionCapacityReached",
                Message = "Message",
                RequiredAction = RequiredAction.CommitOrRollback,
            }));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("RevisionCapacityReached");
        var continuation = result.StructuredContent.Value.GetProperty("continuation");
        continuation.GetProperty("kind").GetString().Should().Be("ChooseTool");
        continuation.GetProperty("tools").EnumerateArray().Select(static item => item.GetString()).Should().Equal(
            "transaction-commit",
            "transaction-rollback");
    }

    [Fact]
    public async Task GIVEN_HandlerThrows_WHEN_InvokingMutation_THEN_ShouldPropagateFailureAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .Returns(() => ValueTask.FromException<PluginExecutionResult<MutationCandidate>>(new InvalidOperationException("Message")));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        var assertion = await action.Should().ThrowAsync<WorkspaceAttributedToolException>();
        assertion.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        contextFactory.Verify(item => item.DetectUnexpectedWorkspaceChange(context.Object), Times.Once);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerCancellation_WHEN_InvokingMutation_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), cancellationSource.Token))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .Returns(() => ValueTask.FromCanceled<PluginExecutionResult<MutationCandidate>>(cancellationSource.Token));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), cancellationSource.Token);

        var assertion = await action.Should().ThrowAsync<WorkspaceAttributedToolException>();
        assertion.Which.InnerException.Should().BeAssignableTo<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StagerThrows_WHEN_InvokingMutation_THEN_ShouldPropagateFailureAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), CancellationToken.None))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult.Success(new MutationCandidate
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
            .Returns(() => ValueTask.FromException<WorkspaceOperationResult<MutationStagingOutcome>>(new InvalidOperationException("Message")));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        var assertion = await action.Should().ThrowAsync<WorkspaceAttributedToolException>();
        assertion.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StagerCancellation_WHEN_InvokingMutation_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IMutationContext>();
        ToolExecutionContextMockHelper.ConfigurePluginContext(context, _roslynWorkspace.CurrentSolution);
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceMutationRequest>(), cancellationSource.Token))
            .Returns(PluginMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .ReturnsAsync(PluginExecutionResult.Success(new MutationCandidate
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
            .Returns(() => ValueTask.FromCanceled<WorkspaceOperationResult<MutationStagingOutcome>>(cancellationSource.Token));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), cancellationSource.Token);

        var assertion = await action.Should().ThrowAsync<WorkspaceAttributedToolException>();
        assertion.Which.InnerException.Should().BeAssignableTo<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingMutation_THEN_ShouldPublishInvalidRequestWithoutAcquiringContext()
    {
        var handler = new Mock<IMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        TestMutationRequest? request = null;
        var errorMessage = "The tool arguments did not match the request contract.";
        _requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(false);
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(42),
        }, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("InvalidRequest");
        contextFactory.Verify(item => item.CreateMutationContext(
            It.IsAny<WorkspaceMutationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);

        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestMutationRequest>(),
            It.IsAny<IMutationContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private PluginMutationMcpServerTool<TestMutationRequest> CreateTarget(
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
            _requestBinder.Object,
            McpServerToolTestData.CreateOptions());
    }

#pragma warning disable CA1515 // Moq's dynamic proxy must access this closed-generic handler contract.
    public sealed record TestMutationRequest : WorkspaceMutationRequest
    {
        public string Name { get; init; } = string.Empty;
    }
#pragma warning restore CA1515
}
