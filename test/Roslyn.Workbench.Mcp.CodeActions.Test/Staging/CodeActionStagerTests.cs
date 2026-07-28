using Microsoft.CodeAnalysis.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class CodeActionStagerTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionResolver> _resolver;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<ICodeActionReferenceStore> _referenceStore;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly CodeActionStager _target;

    public CodeActionStagerTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _resolver = new Mock<ICodeActionResolver>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _referenceStore = new Mock<ICodeActionReferenceStore>();
        _context = new Mock<ICodeActionExecutionContext>();
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());
        _target = new CodeActionStager(
            _composition.Object,
            _resolver.Object,
            _evaluator.Object,
            _referenceStore.Object);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingReference_THEN_ShouldRejectWithoutResolvingAction()
    {
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Unavailable("Unavailable."));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _resolver.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<Guid>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedRefactoring_WHEN_StagingCodeAction_THEN_ShouldCreateMutationCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var expectedSnapshot = new SnapshotPrecondition();
        var action = CreateAction(roslyn.Solution);
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                expectedSnapshot,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Replay));

        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(roslyn.Solution));

        var result = await _target.StageAsync(
            new StageCodeActionRequest
            {
                ExpectedSnapshot = expectedSnapshot,
                ActionId = Guid.Empty,
            },
            _context.Object,
            CancellationToken.None);

        result.Data!.CandidateSolution.Should().BeSameAs(roslyn.Solution);
        result.Data.Summary.Should().Be("Title");
    }

    [Fact]
    public async Task GIVEN_ResolutionProvesReferenceInvalid_WHEN_StagingAction_THEN_ShouldRemoveReference()
    {
        var rejection = CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(new CodeActionExecutionError
        {
            Code = "ErrorCode",
            Message = "Message",
        });

        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                It.IsAny<SnapshotPrecondition>(),
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution.Rejected(
                rejection,
                CodeActionResolutionFailureKind.InvalidReference));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
        _referenceStore.Verify(item => item.Remove(Guid.Empty), Times.Once);
        _evaluator.Verify(item => item.EvaluateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolutionReturnsRetryableConflict_WHEN_StagingAction_THEN_ShouldRetainReference()
    {
        var conflict = CodeActionExecutionResult.Conflict<WorkspaceMutationCandidate>(new CodeActionExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "Message",
        });

        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                It.IsAny<SnapshotPrecondition>(),
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution.Rejected(conflict));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(conflict);
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedActionRequiresParameters_WHEN_StagingCodeAction_THEN_ShouldRejectWithoutEvaluation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CreateAction(roslyn.Solution);
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                It.IsAny<SnapshotPrecondition>(),
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Parameterised));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionRequiresParameters");
        _evaluator.Verify(item => item.EvaluateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsAction_WHEN_StagingCodeAction_THEN_ShouldReturnEvaluatorFailure()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CreateAction(roslyn.Solution);
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                It.IsAny<SnapshotPrecondition>(),
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Replay));

        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.UnsupportedActionOperation,
                "Unsupported action operation."));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
        result.Error.Message.Should().Be("Unsupported action operation.");
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
    }

    private static CodeActionResolution<WorkspaceMutationCandidate> CreateResolution(
        CodeAction action,
        Document document,
        CodeActionExecutionMode executionMode)
    {
        var discoveredAction = new DiscoveredCodeAction
        {
            Action = action,
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = "Title",
            Descriptor = new CodeActionDescriptorEntry
            {
                ExecutionMode = executionMode,
            },
            TargetSpan = default,
            EquivalenceKey = "EquivalenceKey",
        };

        return CodeActionResolution.Resolved<WorkspaceMutationCandidate>(
            discoveredAction,
            document,
            default,
            new CodeActionReference(
                Guid.Empty,
                new CodeActionReplayRecipe(),
                new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero)));
    }

    private static CodeAction CreateAction(Solution solution)
    {
        return CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey");
    }
}
