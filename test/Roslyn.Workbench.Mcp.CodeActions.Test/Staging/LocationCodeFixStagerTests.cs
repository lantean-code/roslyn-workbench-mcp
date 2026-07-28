using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class LocationCodeFixStagerTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly LocationCodeFixStager _target;

    public LocationCodeFixStagerTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _composition.SetupGet(item => item.Status).Returns(new CodeActionCompositionStatus
        {
            IsAvailable = true,
        });

        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());

        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new LocationCodeFixStager(
            _composition.Object,
            _discoveryService.Object,
            _evaluator.Object,
            _diagnosticService.Object,
            new CodeActionToolRequestResolver(new CodeActionScopeResolver()));
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_StagingLocationFix_THEN_ShouldThrowBeforeCheckingAvailability()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest
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
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingLocationFix_THEN_ShouldRejectWithoutResolvingLocation()
    {
        _composition.SetupGet(item => item.Status).Returns(new CodeActionCompositionStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest
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
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_StagingLocationFix_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(expectedSnapshot))
            .Returns(SnapshotMatchResult.WorkspaceEpochMismatch());

        var result = await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest
            {
                ExpectedSnapshot = expectedSnapshot,
                Location = new LocationSelector(),
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_DiagnosticIdsAreEmpty_WHEN_StagingLocationFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Location = new LocationSelector(),
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
        _workspaceResolver.Verify(item => item.ResolveLocationAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "LocationNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "LocationAmbiguous")]
    public async Task GIVEN_LocationDoesNotResolve_WHEN_StagingLocationFix_THEN_ShouldReturnResolutionRejection(
        SelectorResolveStatus status,
        string expectedCode)
    {
        var selector = new LocationSelector();
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorTestFactory.CreateUnresolvedResult<Location>(status));

        var result = await _target.StageLocationCodeFixAsync(
            CreateRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_LocationIsNotOwnedByCurrentSolution_WHEN_StagingLocationFix_THEN_ShouldRejectLocation()
    {
        using var currentRoslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(otherRoslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(currentRoslyn.Solution);

        var result = await _target.StageLocationCodeFixAsync(
            CreateRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("LocationNotFound");
        _discoveryService.Verify(item => item.GetMatchingCodeFixProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoProviderMatches_WHEN_StagingLocationFix_THEN_ShouldRejectCodeFix()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([]);

        var result = await _target.StageLocationCodeFixAsync(
            CreateRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
        _diagnosticService.Verify(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoDiagnosticMatches_WHEN_StagingLocationFix_THEN_ShouldRejectCodeFix()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var request = CreateRequest(selector);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync([]);

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            It.IsAny<CodeFixProvider>(),
            It.IsAny<Document>(),
            It.IsAny<IReadOnlyList<Diagnostic>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(LocationFixFilter.Title)]
    [InlineData(LocationFixFilter.EquivalenceKey)]
    public async Task GIVEN_ActionDoesNotSatisfyFilter_WHEN_StagingLocationFix_THEN_ShouldRejectCodeFix(LocationFixFilter filter)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var diagnostic = CreateDiagnostic(location);
        IReadOnlyList<Diagnostic> diagnostics = [diagnostic];
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["DiagnosticId"]);
        var request = CreateRequest(selector);
        if (filter == LocationFixFilter.Title)
        {
            request = request with { Title = "OtherTitle" };
        }
        else
        {
            request = request with { EquivalenceKey = "OtherEquivalenceKey" };
        }

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(provider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
    }

    [Fact]
    public async Task GIVEN_MatchingActionIsHidden_WHEN_StagingLocationFix_THEN_ShouldRejectCodeFix()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        IReadOnlyList<Diagnostic> diagnostics = [CreateDiagnostic(location)];
        var action = CreateDiscoveredAction(
            roslyn.Solution,
            "Title",
            "EquivalenceKey",
            [],
            ["DiagnosticId"],
            CodeActionExecutionMode.Unsupported,
            isVisible: false);

        var request = CreateRequest(selector) with
        {
            ProviderId = null,
            Title = null,
            EquivalenceKey = null,
        };

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders(null))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(provider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
    }

    [Fact]
    public async Task GIVEN_EquivalentActionsTargetDifferentSpans_WHEN_StagingLocationFix_THEN_ShouldRejectAmbiguousSelection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        IReadOnlyList<Diagnostic> diagnostics = [CreateDiagnostic(location)];
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["DiagnosticId"]);
        var secondAction = firstAction with
        {
            TargetSpan = new TextSpan(2, 1),
        };
        var request = CreateRequest(selector) with
        {
            Title = null,
            EquivalenceKey = null,
        };

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(provider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([firstAction, secondAction]);

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
    }

    [Theory]
    [InlineData((int)CodeActionExecutionMode.Replay)]
    [InlineData((int)CodeActionExecutionMode.Parameterised)]
    public async Task GIVEN_MatchingActionCanExecute_WHEN_StagingLocationFix_THEN_ShouldCreateMutationCandidate(int executionModeValue)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var executionMode = (CodeActionExecutionMode)executionModeValue;
        var selector = new LocationSelector();
        var expectedSnapshot = new SnapshotPrecondition();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        IReadOnlyList<Diagnostic> diagnostics = [CreateDiagnostic(location)];
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["DiagnosticId"], executionMode);

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = roslyn.Solution,
        };

        var request = CreateRequest(selector) with
        {
            ExpectedSnapshot = expectedSnapshot,
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
        };

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(provider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([action]);

        _evaluator
            .Setup(item => item.EvaluateAsync(action.Action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(candidate.CandidateSolution));

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Data!.CandidateSolution.Should().BeSameAs(candidate.CandidateSolution);
        result.Data.Summary.Should().Be("Title");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(expectedSnapshot), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SelectedActionIsUnsupported_WHEN_StagingLocationFix_THEN_ShouldRejectCodeFix()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        IReadOnlyList<Diagnostic> diagnostics = [CreateDiagnostic(location)];
        var action = CreateDiscoveredAction(
            roslyn.Solution,
            "Title",
            "EquivalenceKey",
            [],
            ["DiagnosticId"],
            CodeActionExecutionMode.Unsupported);

        var request = CreateRequest(selector);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(provider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
        _evaluator.Verify(item => item.EvaluateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DuplicateCandidatesHaveReorderedDiagnosticIds_WHEN_StagingLocationFix_THEN_ShouldExecuteOneCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        IReadOnlyList<Diagnostic> diagnostics = [CreateDiagnostic(location)];
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["FirstDiagnostic", "SecondDiagnostic"]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["SecondDiagnostic", "FirstDiagnostic"]);
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = roslyn.Solution,
        };

        var request = CreateRequest(selector);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([provider.Object]);

        _diagnosticService
            .Setup(item => item.GetLocationScopedCodeFixDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                request.DiagnosticIds,
                "AnalyzerTypeName",
                "SyntheticDiagnosticId",
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(provider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([firstAction, secondAction]);

        _evaluator
            .Setup(item => item.EvaluateAsync(firstAction.Action, roslyn.Solution, CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(candidate.CandidateSolution));

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Data!.CandidateSolution.Should().BeSameAs(candidate.CandidateSolution);
        result.Data.Summary.Should().Be("Title");
        _evaluator.Verify(item => item.EvaluateAsync(
            firstAction.Action,
            roslyn.Solution,
            CancellationToken.None), Times.Once);
    }

    private static LocationCodeFixRequest CreateRequest(LocationSelector selector)
    {
        return new LocationCodeFixRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Location = selector,
            DiagnosticIds = ["DiagnosticId"],
            ProviderId = "ProviderId",
            AnalyzerTypeName = "AnalyzerTypeName",
            SyntheticDiagnosticId = "SyntheticDiagnosticId",
        };
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(
        Solution solution,
        string title,
        string equivalenceKey,
        IReadOnlyList<int> actionPath,
        IReadOnlyList<string> diagnosticIds,
        CodeActionExecutionMode executionMode = CodeActionExecutionMode.Replay,
        bool isVisible = true)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create(title, _ => Task.FromResult(solution), equivalenceKey),
            Kind = DiscoveredActionKind.CodeFix,
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
            DiagnosticIds = diagnosticIds,
        };
    }

    private static Diagnostic CreateDiagnostic(Location location)
    {
        return Diagnostic.Create(
            new DiagnosticDescriptor("DiagnosticId", "Title", "Message", "Category", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, true),
            location);
    }

    private static async Task<Location> CreateLocationAsync(Document document)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        return syntaxTree!.GetLocation(new TextSpan(0, 1));
    }

#pragma warning disable CA1515 // The enum is part of a public xUnit theory method signature.
    public enum LocationFixFilter
    {
        Title,
        EquivalenceKey,
    }
#pragma warning restore CA1515
}
