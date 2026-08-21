using Microsoft.CodeAnalysis.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class CodeActionStagerTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionResolver> _resolver;
    private readonly Mock<IPreparedFixAllResolver> _preparedFixAllResolver;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<ICodeActionReferenceStore> _referenceStore;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly CodeActionStager _target;

    public CodeActionStagerTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _resolver = new Mock<ICodeActionResolver>();
        _preparedFixAllResolver = new Mock<IPreparedFixAllResolver>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _referenceStore = new Mock<ICodeActionReferenceStore>();
        _context = new Mock<ICodeActionExecutionContext>();
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());
        _target = new CodeActionStager(
            _composition.Object,
            _resolver.Object,
            _preparedFixAllResolver.Object,
            _evaluator.Object,
            _referenceStore.Object);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingReference_THEN_ShouldRejectWithoutResolvingAction()
    {
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Unavailable("Unavailable."));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")), ActionId = Guid.Empty },
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
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var action = CreateAction(roslyn.Solution);
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                expectedSnapshot,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document));

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
        result.Data.Precondition.Should().BeNull();
        result.Data.Summary.Should().Be("Title");
    }

    [Fact]
    public async Task GIVEN_PreparedFixAllReference_WHEN_StagingCodeAction_THEN_ShouldResolvePreparedAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var action = CreateAction(roslyn.Solution);
        var preparedFixAll = CodeActionExecutionTestFactory.CreatePreparedFixAllReplayData();
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _referenceStore.Setup(item => item.IsPreparedFixAll(Guid.Empty)).Returns(true);
        _preparedFixAllResolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                expectedSnapshot,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, preparedFixAll));

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
        result.Data.Precondition.Should().BeSameAs(preparedFixAll.CandidatePrecondition);
        _resolver.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<Guid>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            new StageCodeActionRequest { ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")), ActionId = Guid.Empty },
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
            new StageCodeActionRequest { ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(conflict);
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
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
            .ReturnsAsync(CreateResolution(action, roslyn.Document));

        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.UnsupportedActionOperation,
                "Unsupported action operation."));

        var result = await _target.StageAsync(
            new StageCodeActionRequest { ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
        result.Error.Message.Should().Be("Unsupported action operation.");
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsPreparedFixAll_WHEN_Staging_THEN_ShouldInvalidateReferenceAsChanged()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CreateAction(roslyn.Solution);
        var preparedFixAll = CodeActionExecutionTestFactory.CreatePreparedFixAllReplayData();
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _referenceStore.Setup(item => item.IsPreparedFixAll(Guid.Empty)).Returns(true);
        _preparedFixAllResolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                It.IsAny<SnapshotPrecondition>(),
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, preparedFixAll));

        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.UnsupportedActionOperation,
                "Unsupported action operation."));

        var result = await _target.StageAsync(
            new StageCodeActionRequest
            {
                ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                    Guid.Parse("11111111-1111-1111-1111-111111111111")),
                ActionId = Guid.Empty,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(WorkspaceErrorCodes.MutationCandidateChanged);
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        _referenceStore.Verify(item => item.Remove(Guid.Empty), Times.Once);
    }

    private static CodeActionResolution<WorkspaceMutationCandidate> CreateResolution(
        CodeAction action,
        Document document,
        PreparedFixAllReplayData? preparedFixAll = null)
    {
        var discoveredAction = new DiscoveredCodeAction
        {
            Action = action,
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = "Title",
            TargetSpan = default,
            EquivalenceKey = "EquivalenceKey",
        };

        var replayRecipe = CodeActionExecutionTestFactory.CreateReplayRecipe() with
        {
            PreparedFixAll = preparedFixAll,
        };
        var expiresAt = new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero);
        var reference = new CodeActionReference(Guid.Empty, replayRecipe, expiresAt);

        return CodeActionResolution.Resolved<WorkspaceMutationCandidate>(
            discoveredAction,
            document,
            default,
            reference);
    }

    private static CodeAction CreateAction(Solution solution)
    {
        return CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey");
    }
}
