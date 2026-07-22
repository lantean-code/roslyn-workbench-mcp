using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class ListCodeActionsToolTests
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionInfoFactory> _infoFactory;
    private readonly Mock<ICodeActionQueryContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly ListCodeActionsTool _target;

    public ListCodeActionsToolTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _infoFactory = new Mock<ICodeActionInfoFactory>();
        _context = new Mock<ICodeActionQueryContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });

        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new ListCodeActionsTool(
            _providerCatalog.Object,
            _discoveryService.Object,
            _diagnosticService.Object,
            _infoFactory.Object,
            new CodeActionToolRequestResolver(new CodeActionScopeResolver()));
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_Executing_THEN_ShouldRejectBeforeValidatingSnapshot()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest(),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_Executing_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(expectedSnapshot))
            .Returns(SnapshotMatchResult.TransactionRevisionMismatch());

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                ExpectedSnapshot = expectedSnapshot,
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_LocationIsMissing_WHEN_Executing_THEN_ShouldRejectRequest()
    {
        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest(),
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
    public async Task GIVEN_LocationDoesNotResolve_WHEN_Executing_THEN_ShouldReturnResolutionRejection(
        SelectorResolveStatus status,
        string expectedCode)
    {
        var selector = new LocationSelector();
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorTestFactory.CreateUnresolvedResult<Location>(status));

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_LocationIsNotOwnedByCurrentSolution_WHEN_Executing_THEN_ShouldRejectLocation()
    {
        using var currentRoslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(otherRoslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(currentRoslyn.Solution);

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("LocationNotFound");
        _discoveryService.Verify(item => item.GetMatchingRefactoringProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AllActionFamiliesAreExcluded_WHEN_Executing_THEN_ShouldReturnEmptyListWithoutDiscovery()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                IncludeRefactorings = false,
                IncludeCodeFixes = false,
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Actions.Should().BeEmpty();
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoProvidersMatchIncludedFamilies_WHEN_Executing_THEN_ShouldReturnEmptyList()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var diagnosticIds = new[] { "DiagnosticId" };
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([]);
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders(null)).Returns([]);
        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                DiagnosticIds = diagnosticIds,
            },
            _context.Object,
            CancellationToken.None);

        result.Data!.Actions.Should().BeEmpty();
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            location.SourceSpan,
            diagnosticIds,
            CancellationToken.None), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_VisibleActionsFromBothFamilies_WHEN_ExecutingWithoutEffectiveDiagnosticFilter_THEN_ShouldClassifyOrderAndCreateInfo(
        bool useNullFilter)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var refactoringProvider = new Mock<CodeRefactoringProvider>();
        var codeFixProvider = new Mock<CodeFixProvider>();
        codeFixProvider
            .SetupGet(item => item.FixableDiagnosticIds)
            .Returns(["DiagnosticId"]);

        IReadOnlyList<Diagnostic> diagnostics = [];
        var visibleDescriptor = new CodeActionDescriptorEntry
        {
            IsVisible = true,
        };

        var hiddenDescriptor = new CodeActionDescriptorEntry
        {
            IsVisible = false,
        };

        var earlier = CreateDiscoveredAction(roslyn.Solution, "EarlierTitle", "SecondProvider", null, [], DiscoveredActionKind.CodeFix, visibleDescriptor);
        var firstPath = CreateDiscoveredAction(roslyn.Solution, "Title", "FirstProvider", null, [1], DiscoveredActionKind.CodeFix, visibleDescriptor);
        var nestedFirstPath = CreateDiscoveredAction(roslyn.Solution, "Title", "FirstProvider", null, [1, 0], DiscoveredActionKind.CodeFix, visibleDescriptor);
        var secondPath = CreateDiscoveredAction(roslyn.Solution, "Title", "FirstProvider", null, [2], DiscoveredActionKind.Refactoring, visibleDescriptor);
        var equivalence = CreateDiscoveredAction(roslyn.Solution, "Title", "FirstProvider", "EquivalenceKey", [], DiscoveredActionKind.CodeFix, visibleDescriptor);
        var laterProvider = CreateDiscoveredAction(roslyn.Solution, "Title", "SecondProvider", null, [], DiscoveredActionKind.Refactoring, visibleDescriptor);
        var hidden = CreateDiscoveredAction(roslyn.Solution, "HiddenTitle", "ProviderId", null, [], DiscoveredActionKind.Refactoring, hiddenDescriptor);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([refactoringProvider.Object]);
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders(null)).Returns([codeFixProvider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(refactoringProvider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([laterProvider, secondPath, hidden]);
        _diagnosticService
            .Setup(item => item.GetDocumentDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                It.Is<IReadOnlyList<string>>(diagnosticIds => diagnosticIds.Count == 1 && diagnosticIds[0] == "DiagnosticId"),
                CancellationToken.None))
            .ReturnsAsync(diagnostics);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(codeFixProvider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([equivalence, earlier, nestedFirstPath, firstPath]);

        var orderedActions = new[] { earlier, firstPath, nestedFirstPath, secondPath, equivalence, laterProvider };
        foreach (var action in orderedActions)
        {
            _infoFactory
                .Setup(item => item.Create(action, _context.Object, roslyn.Document, location.SourceSpan, visibleDescriptor))
                .Returns(new CodeActionInfo
                {
                    ActionId = $"{action.ProviderId}:{action.Title}:{action.EquivalenceKey}:{string.Join('.', action.ActionPath)}",
                    Title = action.Title,
                    ProviderId = action.ProviderId,
                    ExpiresAt = "2000-01-01T00:00:00.0000000+00:00",
                });
        }

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                DiagnosticIds = useNullFilter ? null : [],
            },
            _context.Object,
            CancellationToken.None);

        result.Data!.Actions.Select(item => item.ActionId).Should().Equal(
            "SecondProvider:EarlierTitle::",
            "FirstProvider:Title::1",
            "FirstProvider:Title::1.0",
            "FirstProvider:Title::2",
            "FirstProvider:Title:EquivalenceKey:",
            "SecondProvider:Title::");

        _infoFactory.Verify(item => item.Create(
            hidden,
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<CodeActionDescriptorEntry>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DiagnosticFilter_WHEN_Executing_THEN_ShouldCollectOnlyDistinctFixableDiagnosticsInFilter()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var firstProvider = new Mock<CodeFixProvider>();
        firstProvider
            .SetupGet(item => item.FixableDiagnosticIds)
            .Returns(["FirstId", "SharedId"]);
        var secondProvider = new Mock<CodeFixProvider>();
        secondProvider
            .SetupGet(item => item.FixableDiagnosticIds)
            .Returns(["SharedId", "SecondId"]);
        IReadOnlyList<Diagnostic> diagnostics = [];
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders(null))
            .Returns([firstProvider.Object, secondProvider.Object]);
        _diagnosticService
            .Setup(item => item.GetDocumentDiagnosticsAsync(
                roslyn.Document,
                location.SourceSpan,
                It.Is<IReadOnlyList<string>>(diagnosticIds =>
                    diagnosticIds.Count == 2
                    && diagnosticIds[0] == "SharedId"
                    && diagnosticIds[1] == "SecondId"),
                CancellationToken.None))
            .ReturnsAsync(diagnostics);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(firstProvider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([]);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(secondProvider.Object, roslyn.Document, diagnostics, CancellationToken.None))
            .ReturnsAsync([]);

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                IncludeRefactorings = false,
                DiagnosticIds = ["SharedId", "SecondId", "UnknownId"],
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            It.IsAny<CodeFixProvider>(),
            roslyn.Document,
            diagnostics,
            CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_NoFixableDiagnosticMatchesFilter_WHEN_Executing_THEN_ShouldSkipDiagnosticCollection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var codeFixProvider = new Mock<CodeFixProvider>();
        codeFixProvider
            .SetupGet(item => item.FixableDiagnosticIds)
            .Returns(["FixableId"]);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders(null)).Returns([codeFixProvider.Object]);

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                IncludeRefactorings = false,
                DiagnosticIds = ["OtherId"],
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Actions.Should().BeEmpty();
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(
        Solution solution,
        string title,
        string providerId,
        string? equivalenceKey,
        IReadOnlyList<int> actionPath,
        DiscoveredActionKind kind,
        CodeActionDescriptorEntry descriptor)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create(title, _ => Task.FromResult(solution), equivalenceKey),
            Kind = kind,
            ProviderId = providerId,
            Title = title,
            Descriptor = descriptor,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        };
    }

    private static async Task<Location> CreateLocationAsync(Document document)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        return syntaxTree!.GetLocation(new TextSpan(0, 1));
    }
}
