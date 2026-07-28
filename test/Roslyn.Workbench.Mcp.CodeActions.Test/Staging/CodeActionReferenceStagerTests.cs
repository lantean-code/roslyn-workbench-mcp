using Microsoft.CodeAnalysis.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class CodeActionReferenceStagerTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionResolver> _resolver;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly CodeActionReferenceStager _target;

    public CodeActionReferenceStagerTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _resolver = new Mock<ICodeActionResolver>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _context = new Mock<ICodeActionExecutionContext>();
        _composition.SetupGet(item => item.Status).Returns(new CodeActionCompositionStatus
        {
            IsAvailable = true,
        });

        _target = new CodeActionReferenceStager(
            _composition.Object,
            _resolver.Object,
            _evaluator.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingReference_THEN_ShouldRejectWithoutResolvingAction(
        bool stageCodeFix)
    {
        _composition.SetupGet(item => item.Status).Returns(new CodeActionCompositionStatus
        {
            IsAvailable = false,
        });

        CodeActionExecutionResult<WorkspaceMutationCandidate> result;
        if (stageCodeFix)
        {
            result = await _target.StageCodeFixAsync(
                new StageCodeFixRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
                _context.Object,
                CancellationToken.None);
        }
        else
        {
            result = await _target.StageCodeActionAsync(
                new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
                _context.Object,
                CancellationToken.None);
        }

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _resolver.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<Guid>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<DiscoveredActionKind?>(),
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
                DiscoveredActionKind.Refactoring,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Replay));

        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(roslyn.Solution));

        var result = await _target.StageCodeActionAsync(
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
    public async Task GIVEN_ResolutionIsRejected_WHEN_StagingCodeFix_THEN_ShouldReturnResolutionRejection()
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
                DiscoveredActionKind.CodeFix,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution.Rejected(rejection));

        var result = await _target.StageCodeFixAsync(
            new StageCodeFixRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
        _evaluator.Verify(item => item.EvaluateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
                DiscoveredActionKind.Refactoring,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Parameterised));

        var result = await _target.StageCodeActionAsync(
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
                DiscoveredActionKind.Refactoring,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Replay));

        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.UnsupportedActionOperation,
                "Unsupported action operation."));

        var result = await _target.StageCodeActionAsync(
            new StageCodeActionRequest { ExpectedSnapshot = new SnapshotPrecondition(), ActionId = Guid.Empty },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
        result.Error.Message.Should().Be("Unsupported action operation.");
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
