using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class CodeActionSelectionStagerTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly CodeActionSelectionStager _target;

    public CodeActionSelectionStagerTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());

        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());

        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new CodeActionSelectionStager(
            _composition.Object,
            _discoveryService.Object,
            _evaluator.Object,
            new CodeActionToolRequestResolver(new CodeActionScopeResolver()));
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_ReplayingCodeAction_THEN_ShouldThrowBeforeCheckingAvailability()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = new LocationSelector(),
            },
            _context.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _composition.VerifyGet(item => item.Status, Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_ReplayingCodeAction_THEN_ShouldRejectBeforeValidatingSnapshot()
    {
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Unavailable("Unavailable."));

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = new LocationSelector(),
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_ReplayingCodeAction_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(expectedSnapshot))
            .Returns(SnapshotMatchResult.TransactionRevisionMismatch());

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = expectedSnapshot,
                Location = new LocationSelector(),
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "LocationNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "LocationAmbiguous")]
    public async Task GIVEN_LocationDoesNotResolve_WHEN_ReplayingCodeAction_THEN_ShouldReturnResolutionRejection(
        SelectorResolveStatus status,
        string expectedCode)
    {
        var selector = new LocationSelector();
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorTestFactory.CreateUnresolvedResult<Location>(status));

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_LocationIsNotOwnedByCurrentSolution_WHEN_ReplayingCodeAction_THEN_ShouldRejectLocation()
    {
        using var currentRoslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(otherRoslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(currentRoslyn.Solution);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("LocationNotFound");
        _discoveryService.Verify(item => item.GetMatchingRefactoringProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoProviderMatches_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([]);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionUnavailable");
    }

    [Theory]
    [InlineData(ReplayFilter.Title)]
    [InlineData(ReplayFilter.TitleStartsWith)]
    [InlineData(ReplayFilter.TitleDoesNotContain)]
    [InlineData(ReplayFilter.EquivalenceKey)]
    [InlineData(ReplayFilter.ActionPath)]
    public async Task GIVEN_ActionDoesNotSatisfyFilter_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction(ReplayFilter filter)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        var request = CreateMismatchingRequest(selector, filter);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageReplayCodeActionAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionUnavailable");
    }

    [Fact]
    public async Task GIVEN_MatchingActionIsHidden_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(
            roslyn.Solution,
            "Title",
            "EquivalenceKey",
            [],
            CodeActionExecutionMode.Unsupported,
            isVisible: false);

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders(null))
            .Returns([provider.Object]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionUnavailable");
    }

    [Fact]
    public async Task GIVEN_MultipleDistinctActionsMatch_WHEN_ReplayingCodeAction_THEN_ShouldRejectAmbiguousSelection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var firstProvider = new Mock<CodeRefactoringProvider>();
        var secondProvider = new Mock<CodeRefactoringProvider>();
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "FirstTitle", "FirstEquivalenceKey", [1]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "SecondTitle", "SecondEquivalenceKey", [2]);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders(null))
            .Returns([firstProvider.Object, secondProvider.Object]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(firstProvider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([firstAction]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(secondProvider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([secondAction]);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Theory]
    [InlineData((int)CodeActionExecutionMode.Parameterised, "ActionRequiresParameters")]
    [InlineData((int)CodeActionExecutionMode.Unsupported, "CodeActionUnavailable")]
    public async Task GIVEN_SelectedActionCannotBeReplayed_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction(
        int executionModeValue,
        string expectedCode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var executionMode = (CodeActionExecutionMode)executionModeValue;
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [], executionMode);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
        _evaluator.Verify(item => item.EvaluateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SelectionMatchesReplayableAction_WHEN_StagingSelection_THEN_ShouldCreateMutationCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var expectedSnapshot = new SnapshotPrecondition();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);

        _evaluator
            .Setup(item => item.EvaluateAsync(action.Action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(roslyn.Solution));

        var result = await _target.StageSelectionAsync(
            selector,
            expectedSnapshot,
            CancellationToken.None,
            _context.Object,
            "ProviderId",
            "Title",
            "Tit",
            "Excluded",
            "EquivalenceKey",
            [1]);

        result.Data!.CandidateSolution.Should().BeSameAs(roslyn.Solution);
        result.Data.Summary.Should().Be("Title");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(expectedSnapshot), Times.Once);
        _evaluator.Verify(item => item.EvaluateAsync(
            action.Action,
            roslyn.Solution,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DuplicateReplayCandidates_WHEN_ReplayingCodeAction_THEN_ShouldExecuteOneCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([firstAction, secondAction]);

        _evaluator
            .Setup(item => item.EvaluateAsync(firstAction.Action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(roslyn.Solution));

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
            },
            _context.Object,
            CancellationToken.None);

        result.Data!.CandidateSolution.Should().BeSameAs(roslyn.Solution);
        result.Data.Summary.Should().Be("Title");
        _evaluator.Verify(item => item.EvaluateAsync(
            firstAction.Action,
            roslyn.Solution,
            CancellationToken.None), Times.Once);
    }

    private static ReplayCodeActionRequest CreateMismatchingRequest(LocationSelector selector, ReplayFilter filter)
    {
        return filter switch
        {
            ReplayFilter.Title => new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
                Title = "OtherTitle",
            },
            ReplayFilter.TitleStartsWith => new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
                TitleStartsWith = "Other",
            },
            ReplayFilter.TitleDoesNotContain => new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
                TitleDoesNotContain = "Title",
            },
            ReplayFilter.EquivalenceKey => new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
                EquivalenceKey = "OtherEquivalenceKey",
            },
            _ => new ReplayCodeActionRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = selector,
                ProviderId = "ProviderId",
                ActionPath = [2],
            },
        };
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(
        Solution solution,
        string title,
        string equivalenceKey,
        IReadOnlyList<int> actionPath,
        CodeActionExecutionMode executionMode = CodeActionExecutionMode.Replay,
        bool isVisible = true)
    {
        return new DiscoveredCodeAction
        {
            Action = CreateAction(solution, title, equivalenceKey),
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = title,
            Descriptor = new CodeActionDescriptorEntry
            {
                IsVisible = isVisible,
                ExecutionMode = executionMode,
            },
            TargetSpan = new TextSpan(0, 1),
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        };
    }

    private static CodeAction CreateAction(Solution solution, string title, string equivalenceKey)
    {
        return CodeAction.Create(title, _ => Task.FromResult(solution), equivalenceKey);
    }

    private static async Task<Location> CreateLocationAsync(Document document)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var span = new TextSpan(0, 1);

        return syntaxTree!.GetLocation(span);
    }

#pragma warning disable CA1515 // The enum is part of a public xUnit theory method signature.
    public enum ReplayFilter
    {
        Title,
        TitleStartsWith,
        TitleDoesNotContain,
        EquivalenceKey,
        ActionPath,
    }
#pragma warning restore CA1515
}
