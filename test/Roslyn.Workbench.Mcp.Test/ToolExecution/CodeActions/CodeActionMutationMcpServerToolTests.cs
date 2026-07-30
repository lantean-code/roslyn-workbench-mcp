using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.CodeActions;

public sealed class CodeActionMutationMcpServerToolTests
{
    private readonly Mock<IMcpToolProtocolFactory> _protocolFactory;
    private readonly Mock<ICodeActionReferenceStore> _referenceStore;

    public CodeActionMutationMcpServerToolTests()
    {
        _protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        _referenceStore = new Mock<ICodeActionReferenceStore>();
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

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

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
            .ReturnsAsync(CreateFailure(outcomeName));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be(outcomeName);
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
        result.StructuredContent.Value.GetProperty("diagnostics")[0].GetProperty("id").GetString().Should().Be("Id");
        result.StructuredContent.Value.GetProperty("warnings")[0].GetProperty("code").GetString().Should().Be("Code");
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
            .ReturnsAsync(CodeActionExecutionResult.NoChange<WorkspaceMutationCandidate>());

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("staged").GetBoolean().Should().BeFalse();
        stager.Verify(item => item.StageAsync(
            It.IsAny<string>(),
            It.IsAny<WorkspaceMutationCandidate>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            .ReturnsAsync(CodeActionExecutionResult.Success(
                proposal,
                diagnostics: [diagnostic],
                warnings: [warning]));

        var stagingOutcome = new MutationStagingOutcome
        {
            Operation = "test-code-action-mutation",
            Summary = "StagedSummary",
            Transaction = new TransactionInfo
            {
                Revision = 2,
            },
        };

        stager
            .Setup(item => item.StageAsync(
                "test-code-action-mutation",
                proposal,
                It.Is<IReadOnlyList<DiagnosticInfo>>(diagnostics => diagnostics.SequenceEqual(new[] { diagnostic })),
                It.Is<IReadOnlyList<WarningInfo>>(warnings => warnings.SequenceEqual(new[] { warning })),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(stagingOutcome));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("staged").GetBoolean().Should().BeTrue();
        data.GetProperty("summary").GetString().Should().Be("StagedSummary");
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        stager.Verify(item => item.StageAsync(
            "test-code-action-mutation",
            proposal,
            It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
            It.IsAny<IReadOnlyList<WarningInfo>>(),
            CancellationToken.None), Times.Once);

        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CodeActionReferenceAndSuccessfulStaging_WHEN_InvokingMutation_THEN_ShouldConsumeReference()
    {
        var actionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new Mock<ICodeActionMutationToolHandler<TestReferencedMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestReferencedMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult.Success(
                MutationCandidateTestData.CreateWorkspaceCandidate()));

        var stagingOutcome = new MutationStagingOutcome
        {
            Operation = "test-code-action-mutation",
            Summary = "Summary",
            Transaction = new TransactionInfo(),
        };

        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(stagingOutcome));

        var target = CreateTarget(handler.Object, contextFactory.Object);
        var arguments = McpServerToolTestData.CreateMutationArguments();
        arguments["actionId"] = JsonSerializer.SerializeToElement(actionId);

        var result = await target.InvokeArgumentsAsync(arguments, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _referenceStore.Verify(item => item.Remove(actionId), Times.Once);
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

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = MutationCandidateTestData.Solution,
            Summary = "Summary",
        };

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult.Success(candidate));

        var stagingError = new WorkspaceOperationError
        {
            Code = "RevisionCapacityReached",
            Message = "Message",
            RequiredAction = RequiredAction.CommitOrRollback,
        };

        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Rejected<MutationStagingOutcome>(stagingError));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("RevisionCapacityReached");
    }

    [Fact]
    public async Task GIVEN_CodeActionReferenceAndRejectedStaging_WHEN_InvokingMutation_THEN_ShouldRetainReference()
    {
        var actionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new Mock<ICodeActionMutationToolHandler<TestReferencedMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestReferencedMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult.Success(
                MutationCandidateTestData.CreateWorkspaceCandidate()));

        var stagingError = new WorkspaceOperationError
        {
            Code = "RevisionCapacityReached",
            Message = "Message",
            RequiredAction = RequiredAction.CommitOrRollback,
        };

        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Rejected<MutationStagingOutcome>(stagingError));

        var target = CreateTarget(handler.Object, contextFactory.Object);
        var arguments = McpServerToolTestData.CreateMutationArguments();
        arguments["actionId"] = JsonSerializer.SerializeToElement(actionId);

        var result = await target.InvokeArgumentsAsync(arguments, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionReferenceAndNoChangeStaging_WHEN_InvokingMutation_THEN_ShouldRetainReference()
    {
        var actionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new Mock<ICodeActionMutationToolHandler<TestReferencedMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            stager.Object);

        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestReferencedMutationRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult.Success(
                MutationCandidateTestData.CreateWorkspaceCandidate()));

        stager
            .Setup(item => item.StageAsync(
                It.IsAny<string>(),
                It.IsAny<WorkspaceMutationCandidate>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.NoChange<MutationStagingOutcome>());

        var target = CreateTarget(handler.Object, contextFactory.Object);
        var arguments = McpServerToolTestData.CreateMutationArguments();
        arguments["actionId"] = JsonSerializer.SerializeToElement(actionId);

        var result = await target.InvokeArgumentsAsync(arguments, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
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
            .Returns(() => ValueTask.FromException<CodeActionExecutionResult<WorkspaceMutationCandidate>>(new InvalidOperationException("Message")));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), CancellationToken.None);

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
        await cancellationSource.CancelAsync();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .Returns(() => ValueTask.FromCanceled<CodeActionExecutionResult<WorkspaceMutationCandidate>>(cancellationSource.Token));

        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateMutationArguments(), cancellationSource.Token);

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
            .ReturnsAsync(CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate()));

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
        await cancellationSource.CancelAsync();
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, stager.Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(CodeActionMutationExecutionLease.Acquired(workspaceLease, context.Object));

        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestMutationRequest>(), context.Object, cancellationSource.Token))
            .ReturnsAsync(CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate()));

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

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingMutation_THEN_ShouldPublishInvalidRequestWithoutAcquiringContext()
    {
        var handler = new Mock<ICodeActionMutationToolHandler<TestMutationRequest>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(42),
        }, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("InvalidRequest");
        contextFactory.Verify(item => item.CreateMutationContext(
            It.IsAny<WorkspaceBoundRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private CodeActionMutationMcpServerTool<ICodeActionMutationToolHandler<TRequest>, TRequest> CreateTarget<TRequest>(
        ICodeActionMutationToolHandler<TRequest> handler,
        ICodeActionExecutionContextFactory contextFactory)
        where TRequest : WorkspaceMutationRequest
    {
        var metadata = new CodeActionToolMetadata
        {
            Name = "test-code-action-mutation",
            Title = "Test Code Action Mutation",
            Description = "Description",
        };

        var registration = new CodeActionMutationRegistration<ICodeActionMutationToolHandler<TRequest>, TRequest>(metadata);
        var target = new CodeActionMutationMcpServerTool<ICodeActionMutationToolHandler<TRequest>, TRequest>(
            registration,
            handler,
            contextFactory,
            _referenceStore.Object,
            _protocolFactory.Object,
            Options.Create(new StartupOptions()));

        return target;
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> CreateFailure(string outcomeName)
    {
        var error = new CodeActionExecutionError
        {
            Code = outcomeName,
            Message = "Message",
        };
        var diagnostics = new[]
        {
            new DiagnosticInfo
            {
                Id = "Id",
                Message = "Message",
            },
        };
        var warnings = new[]
        {
            new WarningInfo
            {
                Code = "Code",
                Message = "Message",
            },
        };

        return outcomeName switch
        {
            "Rejected" => CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(
                error,
                RequiredAction.Retry,
                diagnostics,
                warnings),
            "Conflict" => CodeActionExecutionResult.Conflict<WorkspaceMutationCandidate>(
                error,
                RequiredAction.Retry,
                diagnostics,
                warnings),
            "Faulted" => CodeActionExecutionResult.Faulted<WorkspaceMutationCandidate>(
                error,
                RequiredAction.Retry,
                diagnostics,
                warnings),
            _ => throw new InvalidOperationException($"Outcome '{outcomeName}' is not a failure outcome."),
        };
    }

#pragma warning disable CA1515 // Moq's dynamic proxy must access this closed-generic handler contract.
    public sealed record TestMutationRequest : WorkspaceMutationRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record TestReferencedMutationRequest : WorkspaceMutationRequest, ICodeActionReferenceRequest
    {
        public required Guid ActionId { get; init; }

        public string Name { get; init; } = string.Empty;
    }
#pragma warning restore CA1515
}
