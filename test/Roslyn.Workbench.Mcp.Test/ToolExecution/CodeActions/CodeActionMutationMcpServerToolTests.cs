using System.Text.Json;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Test.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.CodeActions;

public sealed class CodeActionMutationMcpServerToolTests
{
    private readonly Mock<IMcpToolProtocolFactory> _protocolFactory;

    public CodeActionMutationMcpServerToolTests()
    {
        _protocolFactory = McpToolProtocolFactoryMockFactory.Create();
    }

    [Fact]
    public async Task GIVEN_ContextAcquisitionFailure_WHEN_InvokingMutation_THEN_ShouldPublishFailureWithoutCallingHandlerAndDisposeLease()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
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
        var failure = CodeActionMcpServerToolTestData.CreateExecutionFailure(CodeActionExecutionOutcome.Rejected, "WorkspaceBusy");
        contextFactory
            .Setup(item => item.CreateMutationContext(
                It.Is<TestMutationRequest>(request => request.Name == "Name"),
                CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Rejected(workspaceLease, failure));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceBusy");
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestMutationRequest>(),
            It.IsAny<ICodeActionMutationContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Conflict")]
    [InlineData("Faulted")]
    public async Task GIVEN_HandlerErrorOutcome_WHEN_InvokingMutation_THEN_ShouldPublishFailureWithoutStaging(
        string outcomeName)
    {
        var outcome = Enum.Parse<CodeActionExecutionOutcome>(outcomeName);
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new CodeActionExecutionResult<WorkspaceMutationCandidate>
            {
                Outcome = outcome,
                Error = new CodeActionExecutionError
                {
                    Code = outcomeName,
                    Message = "Message",
                },
                RequiredAction = RequiredAction.Retry,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be(outcomeName);
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_HandlerFailureWithoutError_WHEN_InvokingMutation_THEN_ShouldPropagateFailure()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new CodeActionExecutionResult<WorkspaceMutationCandidate>
            {
                Outcome = CodeActionExecutionOutcome.Faulted,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_HandlerNoChange_WHEN_InvokingMutation_THEN_ShouldPublishUnstagedSuccess()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.NoChange());
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
    public async Task GIVEN_HandlerSuccessWithoutProposal_WHEN_InvokingMutation_THEN_ShouldPublishUnstagedSuccess()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new CodeActionExecutionResult<WorkspaceMutationCandidate>
            {
                Outcome = CodeActionExecutionOutcome.Succeeded,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("staged").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_HandlerProposalAndSuccessfulStaging_WHEN_InvokingMutation_THEN_ShouldStageProposalAndPublishSuccess()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var diagnostic = new DiagnosticInfo
        {
            Id = "Id",
            Message = "Message",
        };
        var warning = new WarningInfo
        {
            Code = "Warning",
            Message = "Message",
        };
        var proposal = new WorkspaceMutationCandidate
        {
            CandidateSolution = MutationCandidateTestData.Solution,
            Summary = "Summary",
        };
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(
                proposal,
                diagnostics: [diagnostic],
                warnings: [warning]));
        stager
            .Setup(item => item.StageAsync(
                "test-code-action-mutation",
                proposal,
                It.Is<IReadOnlyList<DiagnosticInfo>>(diagnostics => diagnostics.SequenceEqual(new[] { diagnostic })),
                It.Is<IReadOnlyList<WarningInfo>>(warnings => warnings.SequenceEqual(new[] { warning })),
                CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<MutationStagingOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new MutationStagingOutcome
                {
                    Operation = "test-code-action-mutation",
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
        result.StructuredContent!.Value.GetProperty("staged").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("summary").GetString().Should().Be("StagedSummary");
        result.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        stager.Verify(item => item.StageAsync(
            "test-code-action-mutation",
            proposal,
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            CancellationToken.None), Times.Once);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerProposalAndRejectedStaging_WHEN_InvokingMutation_THEN_ShouldPublishStagingFailure()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(new WorkspaceMutationCandidate
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
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("RevisionCapacityReached");
    }

    [Fact]
    public async Task GIVEN_HandlerThrows_WHEN_InvokingMutation_THEN_ShouldPropagateFailureAndDisposeLease()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .Returns(ValueTask.FromException<CodeActionExecutionResult<WorkspaceMutationCandidate>>(new InvalidOperationException("Message")));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerCancellation_WHEN_InvokingMutation_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .Returns(ValueTask.FromCanceled<CodeActionExecutionResult<WorkspaceMutationCandidate>>(cancellationSource.Token));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StagerThrows_WHEN_InvokingMutation_THEN_ShouldPropagateFailureAndDisposeLease()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate()));
        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .Returns(ValueTask.FromException<WorkspaceOperationResult<MutationStagingOutcome>>(new InvalidOperationException("Message")));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StagerCancellation_WHEN_InvokingMutation_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate()));
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
    public async Task GIVEN_MalformedArguments_WHEN_InvokingMutation_THEN_ShouldPropagateFailureWithoutAcquiringContext()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(42),
        }, CancellationToken.None);

        await action.Should().ThrowAsync<JsonException>();
        contextFactory.Verify(item => item.CreateMutationContext(
            It.IsAny<WorkspaceBoundRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private CodeActionMutationMcpServerTool<ICodeActionMutationToolHandler<TestMutationRequest>, TestMutationRequest> CreateTarget(
        ICodeActionMutationToolHandler<TestMutationRequest> handler,
        ICodeActionExecutionContextFactory contextFactory)
    {
        var metadata = new CodeActionToolMetadata
        {
            Name = "test-code-action-mutation",
            Title = "Test Code Action Mutation",
            Description = "Description",
        };

        return new CodeActionMutationMcpServerTool<ICodeActionMutationToolHandler<TestMutationRequest>, TestMutationRequest>(
            new CodeActionMutationRegistration<ICodeActionMutationToolHandler<TestMutationRequest>, TestMutationRequest>(metadata),
            handler,
            contextFactory,
            _protocolFactory.Object,
            Options.Create(new StartupOptions()));
    }

    public sealed record TestMutationRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }
}
