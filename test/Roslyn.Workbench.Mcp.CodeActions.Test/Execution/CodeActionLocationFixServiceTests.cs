using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

public sealed class CodeActionLocationFixServiceTests
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionOperationService> _operationService;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly CodeActionLocationFixService _target;

    public CodeActionLocationFixServiceTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _operationService = new Mock<ICodeActionOperationService>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });

        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new CodeActionLocationFixService(
            _providerCatalog.Object,
            _discoveryService.Object,
            _operationService.Object,
            _diagnosticService.Object);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_StagingLocationFix_THEN_ShouldThrowBeforeCheckingAvailability()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest(),
            _context.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _providerCatalog.VerifyGet(item => item.Status, Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingLocationFix_THEN_ShouldRejectWithoutResolvingLocation()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest(),
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
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_LocationIsMissing_WHEN_StagingLocationFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest
            {
                DiagnosticIds = ["DiagnosticId"],
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_DiagnosticIdsAreEmpty_WHEN_StagingLocationFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageLocationCodeFixAsync(
            new LocationCodeFixRequest
            {
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
            .ReturnsAsync(new SelectorResolveResult<Location>
            {
                Status = status,
            });

        var result = await _target.StageLocationCodeFixAsync(
            CreateRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_ResolvedStatusHasNoLocation_WHEN_StagingLocationFix_THEN_ShouldRejectLocation()
    {
        var selector = new LocationSelector();
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(new SelectorResolveResult<Location>
            {
                Status = SelectorResolveStatus.Resolved,
            });

        var result = await _target.StageLocationCodeFixAsync(
            CreateRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("LocationNotFound");
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
            It.IsAny<ImmutableArray<Diagnostic>>(),
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
        var diagnostics = ImmutableArray.Create(diagnostic);
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["DiagnosticId"]);
        var request = filter == LocationFixFilter.Title
            ? CreateRequest(selector) with { Title = "OtherTitle" }
            : CreateRequest(selector) with { EquivalenceKey = "OtherEquivalenceKey" };
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
        var diagnostics = ImmutableArray.Create(CreateDiagnostic(location));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
    public async Task GIVEN_MultipleDistinctActionsMatch_WHEN_StagingLocationFix_THEN_ShouldRejectAmbiguousSelection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var diagnostics = ImmutableArray.Create(CreateDiagnostic(location));
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "FirstTitle", "FirstEquivalenceKey", [1], ["DiagnosticId"]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "SecondTitle", "SecondEquivalenceKey", [2], ["DiagnosticId"]);
        var request = CreateRequest(selector) with
        {
            Title = null,
            EquivalenceKey = null,
        };

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
    [InlineData(CodeActionExecutionMode.Replay)]
    [InlineData(CodeActionExecutionMode.Parameterised)]
    public async Task GIVEN_MatchingActionCanExecute_WHEN_StagingLocationFix_THEN_ShouldCreateMutationCandidate(CodeActionExecutionMode executionMode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var expectedSnapshot = new SnapshotPrecondition();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var diagnostics = ImmutableArray.Create(CreateDiagnostic(location));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(action.Action, "Title", _context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate));

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Data.Should().BeSameAs(candidate);
        _workspaceResolver.Verify(item => item.ValidateSnapshot(expectedSnapshot), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SelectedActionIsUnsupported_WHEN_StagingLocationFix_THEN_ShouldRejectCodeFix()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var diagnostics = ImmutableArray.Create(CreateDiagnostic(location));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<string>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DuplicateCandidatesHaveReorderedDiagnosticIds_WHEN_StagingLocationFix_THEN_ShouldExecuteOneCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var diagnostics = ImmutableArray.Create(CreateDiagnostic(location));
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["FirstDiagnostic", "SecondDiagnostic"]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1], ["SecondDiagnostic", "FirstDiagnostic"]);
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = roslyn.Solution,
        };

        var request = CreateRequest(selector);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
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
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(firstAction.Action, "Title", _context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate));

        var result = await _target.StageLocationCodeFixAsync(request, _context.Object, CancellationToken.None);

        result.Data.Should().BeSameAs(candidate);
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            firstAction.Action,
            "Title",
            _context.Object,
            CancellationToken.None), Times.Once);
    }

    private static LocationCodeFixRequest CreateRequest(LocationSelector selector)
    {
        return new LocationCodeFixRequest
        {
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
