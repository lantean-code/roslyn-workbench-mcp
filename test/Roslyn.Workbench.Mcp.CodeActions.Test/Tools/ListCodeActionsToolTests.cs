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

        _workspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

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
            new ListCodeActionsRequest
            {
                Location = new LocationSelector(),
            },
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
                Location = new LocationSelector(),
                ExpectedSnapshot = expectedSnapshot,
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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

    [Fact]
    public async Task GIVEN_VisibleActionCannotBeEncoded_WHEN_Executing_THEN_ShouldOmitAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var descriptor = new CodeActionDescriptorEntry
        {
            IsVisible = true,
        };

        var action = CreateDiscoveredAction(
            roslyn.Solution,
            "Title",
            "ProviderId",
            equivalenceKey: null,
            actionPath: [],
            DiscoveredActionKind.Refactoring,
            descriptor);

        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                location.SourceSpan,
                CancellationToken.None))
            .ReturnsAsync([action]);

        _infoFactory
            .Setup(item => item.TryCreate(
                action,
                _context.Object,
                roslyn.Document,
                It.IsAny<ResolvedLocation>(),
                descriptor,
                out It.Ref<CodeActionInfo?>.IsAny))
            .Returns(false);

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                IncludeCodeFixes = false,
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Actions.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_BroadLocationContainsEquivalentActions_WHEN_Executing_THEN_ShouldProjectEachPreciseTargetLocation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { int Value; }");
        var selector = new LocationSelector();
        var requestedLocation = await CreateLocationAsync(roslyn.Document, new TextSpan(0, 10));
        var provider = new Mock<CodeRefactoringProvider>();
        var descriptor = new CodeActionDescriptorEntry
        {
            IsVisible = true,
        };

        var firstAction = CreateDiscoveredAction(
            roslyn.Solution,
            "Title",
            "ProviderId",
            "EquivalenceKey",
            [0],
            DiscoveredActionKind.Refactoring,
            descriptor,
            new TextSpan(2, 1));

        var secondAction = CreateDiscoveredAction(
            roslyn.Solution,
            "Title",
            "ProviderId",
            "EquivalenceKey",
            [0],
            DiscoveredActionKind.Refactoring,
            descriptor,
            new TextSpan(5, 2));

        var middleAction = secondAction with
        {
            TargetSpan = new TextSpan(5, 1),
        };

        var firstInfo = CreateInfo(firstAction, "11111111-1111-1111-1111-111111111111");
        var middleInfo = CreateInfo(middleAction, "22222222-2222-2222-2222-222222222222");
        var secondInfo = CreateInfo(secondAction, "33333333-3333-3333-3333-333333333333");
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(requestedLocation));

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                requestedLocation.SourceSpan,
                CancellationToken.None))
            .ReturnsAsync([secondAction, firstAction, middleAction]);

        _infoFactory
            .Setup(item => item.TryCreate(
                firstAction,
                _context.Object,
                roslyn.Document,
                It.Is<ResolvedLocation>(location => HasSpan(location, 2, 1)),
                descriptor,
                out firstInfo))
            .Returns(true);

        _infoFactory
            .Setup(item => item.TryCreate(
                middleAction,
                _context.Object,
                roslyn.Document,
                It.Is<ResolvedLocation>(location => HasSpan(location, 5, 1)),
                descriptor,
                out middleInfo))
            .Returns(true);

        _infoFactory
            .Setup(item => item.TryCreate(
                secondAction,
                _context.Object,
                roslyn.Document,
                It.Is<ResolvedLocation>(location => HasSpan(location, 5, 2)),
                descriptor,
                out secondInfo))
            .Returns(true);

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                IncludeCodeFixes = false,
            },
            _context.Object,
            CancellationToken.None);

        result.Data!.Actions.Should().Equal(firstInfo, middleInfo, secondInfo);
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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
        Guid[] actionIds =
        [
            new("11111111-1111-1111-1111-111111111111"),
            new("22222222-2222-2222-2222-222222222222"),
            new("33333333-3333-3333-3333-333333333333"),
            new("44444444-4444-4444-4444-444444444444"),
            new("55555555-5555-5555-5555-555555555555"),
            new("66666666-6666-6666-6666-666666666666"),
        ];

        for (var index = 0; index < orderedActions.Length; index++)
        {
            var action = orderedActions[index];
            var info = new CodeActionInfo
            {
                ActionId = actionIds[index],
                Title = action.Title,
                ProviderId = action.ProviderId,
                ExpiresAt = "2000-01-01T00:00:00.0000000+00:00",
                Location = SelectorTestFactory.CreateResolvedLocation("Code.cs", action.TargetSpan.Start, action.TargetSpan.Length),
            };

            _infoFactory
                .Setup(item => item.TryCreate(
                    action,
                    _context.Object,
                    roslyn.Document,
                    It.IsAny<ResolvedLocation>(),
                    visibleDescriptor,
                    out info))
                .Returns(true);
        }

        var result = await _target.ExecuteAsync(
            new ListCodeActionsRequest
            {
                Location = selector,
                DiagnosticIds = useNullFilter ? null : [],
            },
            _context.Object,
            CancellationToken.None);

        result.Data!.Actions.Select(item => item.ActionId).Should().Equal(actionIds);

        _infoFactory.Verify(item => item.TryCreate(
            hidden,
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<Document>(),
            It.IsAny<ResolvedLocation>(),
            It.IsAny<CodeActionDescriptorEntry>(),
            out It.Ref<CodeActionInfo?>.IsAny), Times.Never);
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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
        CodeActionDescriptorEntry descriptor,
        TextSpan? targetSpan = null)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create(title, _ => Task.FromResult(solution), equivalenceKey),
            Kind = kind,
            ProviderId = providerId,
            Title = title,
            Descriptor = descriptor,
            TargetSpan = targetSpan ?? new TextSpan(0, 1),
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        };
    }

    private static CodeActionInfo CreateInfo(DiscoveredCodeAction action, string actionId)
    {
        return new CodeActionInfo
        {
            ActionId = new Guid(actionId),
            Title = action.Title,
            ProviderId = action.ProviderId,
            Location = SelectorTestFactory.CreateResolvedLocation(
                "Code.cs",
                action.TargetSpan.Start,
                action.TargetSpan.Length),
        };
    }

    private static bool HasSpan(ResolvedLocation location, int start, int length)
    {
        return location.Span is not null
            && location.Span.Start == start
            && location.Span.Length == length;
    }

    private static async Task<Location> CreateLocationAsync(Document document, TextSpan? span = null)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        return syntaxTree!.GetLocation(span ?? new TextSpan(0, 1));
    }
}
