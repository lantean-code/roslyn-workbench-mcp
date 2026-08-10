using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class ListCodeActionsToolTests
{
    private static readonly string[] _firstDiagnosticIds = ["DIAG001"];
    private static readonly string[] _secondDiagnosticIds = ["DIAG002"];

    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionInfoFactory> _infoFactory;
    private readonly Mock<ICodeActionReferenceStore> _referenceStore;
    private readonly Mock<ICodeActionQueryContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly ListCodeActionsTool _target;

    public ListCodeActionsToolTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _infoFactory = new Mock<ICodeActionInfoFactory>();
        _referenceStore = new Mock<ICodeActionReferenceStore>();
        _context = new Mock<ICodeActionQueryContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();

        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());
        _workspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);

        _target = new ListCodeActionsTool(
            _composition.Object,
            _discoveryService.Object,
            _diagnosticService.Object,
            _infoFactory.Object,
            _referenceStore.Object,
            new CodeActionToolRequestResolver(new CodeActionScopeResolver()));
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_Executing_THEN_ShouldRejectBeforeResolvingDocument()
    {
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Unavailable("Unavailable."));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Fact]
    public void GIVEN_LimitIsNull_WHEN_GettingEffectiveLimit_THEN_ShouldUsePublishedDefault()
    {
        var request = CreateRequest(limit: null);

        request.EffectiveLimit.Should().Be(50);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "DocumentNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "DocumentAmbiguous")]
    [InlineData(SelectorResolveStatus.Invalid, "DocumentSelectorInvalid")]
    public async Task GIVEN_DocumentDoesNotResolve_WHEN_Executing_THEN_ShouldReturnResolutionRejection(
        SelectorResolveStatus status,
        string expectedCode)
    {
        var selector = new DocumentSelector { Path = "Code.cs" };
        _workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Document>(status));

        var result = await _target.ExecuteAsync(
            CreateRequest(document: selector),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_RangeIsOutsideDocument_WHEN_Executing_THEN_ShouldRejectRange()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);

        var result = await _target.ExecuteAsync(
            CreateRequest(
                document: selector,
                range: new TextSpanRange { Start = 0, Length = 100 }),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("InvalidRange");
        _discoveryService.Verify(item => item.GetMatchingRefactoringProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentModeRequestsAllKinds_WHEN_Executing_THEN_ShouldUseCompleteDocumentAndUnscopedDiagnostics()
    {
        const string source = "class C { int value; }";
        using var roslyn = RoslynTestFactory.CreateDocument(source);
        var selector = SetupDocument(roslyn.Document);
        var refactoringProvider = new Mock<CodeRefactoringProvider>();
        var codeFixProvider = new Mock<CodeFixProvider>();
        codeFixProvider.SetupGet(item => item.FixableDiagnosticIds).Returns(["DIAG001"]);
        var refactoring = CreateAction(roslyn.Solution, "Refactoring", DiscoveredActionKind.Refactoring, new TextSpan(0, source.Length));
        var codeFix = CreateAction(roslyn.Solution, "Code fix", DiscoveredActionKind.CodeFix, new TextSpan(10, 5));
        var refactoringItem = CreateItem("Refactoring", CodeActionKind.Refactoring, 0, source.Length);
        var codeFixItem = CreateItem("Code fix", CodeActionKind.CodeFix, 10, 5);
        var diagnosticCollection = new CodeActionDiagnosticCollection([], []);

        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([refactoringProvider.Object]);
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders(null)).Returns([codeFixProvider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                refactoringProvider.Object,
                roslyn.Document,
                new TextSpan(0, source.Length),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([refactoring]);

        _diagnosticService
            .Setup(item => item.CollectDocumentDiagnosticsAsync(
                roslyn.Document,
                null,
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(_firstDiagnosticIds)),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(diagnosticCollection);

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                codeFixProvider.Object,
                roslyn.Document,
                diagnosticCollection.Diagnostics,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([codeFix]);

        SetupProjection(refactoring, roslyn.Document, refactoringItem);
        SetupProjection(codeFix, roslyn.Document, codeFixItem);

        var result = await _target.ExecuteAsync(
            CreateRequest(document: selector),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Data!.Actions.Items.Should().Equal(codeFixItem, refactoringItem);
        result.Data.Actions.HasMore.Should().BeFalse();
        result.Data.Actions.TotalCount.Should().Be(2);
        _referenceStore.Verify(item => item.Remove(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(3, 0)]
    public async Task GIVEN_ExplicitRange_WHEN_DiscoveringRefactorings_THEN_ShouldPreserveSelectionOrCaret(
        int start,
        int length)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var span = new TextSpan(start, length);
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                span,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([]);

        var result = await _target.ExecuteAsync(
            CreateRequest(
                CodeActionKindSelection.Refactorings,
                selector,
                new TextSpanRange { Start = start, Length = length }),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _discoveryService.Verify(item => item.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            span,
            TestContext.Current.CancellationToken), Times.Once);

        _diagnosticService.Verify(item => item.CollectDocumentDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan?>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SelectionModeRequestsCodeFixes_WHEN_Executing_THEN_ShouldScopeDiagnosticsToExactRange()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeFixProvider>();
        var span = new TextSpan(2, 3);
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["DIAG001", "DIAG002"]);
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders(null)).Returns([provider.Object]);
        _diagnosticService
            .Setup(item => item.CollectDocumentDiagnosticsAsync(
                roslyn.Document,
                span,
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(_secondDiagnosticIds)),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(new CodeActionDiagnosticCollection([], ["Warning"]));

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                provider.Object,
                roslyn.Document,
                It.IsAny<IReadOnlyList<Diagnostic>>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([]);

        var result = await _target.ExecuteAsync(
            CreateRequest(
                CodeActionKindSelection.CodeFixes,
                selector,
                new TextSpanRange { Start = 2, Length = 3 },
                diagnosticIds: ["DIAG002"]),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Data!.Actions.Items.Should().BeEmpty();
        result.Warnings.Should().ContainSingle().Which.Message.Should().Be("Warning");
        _discoveryService.Verify(item => item.GetMatchingRefactoringProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionsExceedLimit_WHEN_Executing_THEN_ShouldPublishBoundedMetadataAndCreateOnlyReturnedReferences()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var first = CreateAction(roslyn.Solution, "A", DiscoveredActionKind.Refactoring, new TextSpan(0, 1));
        var second = CreateAction(roslyn.Solution, "B", DiscoveredActionKind.Refactoring, new TextSpan(1, 1));
        var firstItem = CreateItem("A", CodeActionKind.Refactoring, 0, 1);
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                It.IsAny<TextSpan>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([second, first]);

        SetupProjection(first, roslyn.Document, firstItem);

        var result = await _target.ExecuteAsync(
            CreateRequest(CodeActionKindSelection.Refactorings, selector, limit: 1),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Data!.Actions.Items.Should().ContainSingle().Which.Should().BeSameAs(firstItem);
        result.Data.Actions.HasMore.Should().BeTrue();
        result.Data.Actions.TotalCount.Should().Be(2);
        _infoFactory.Verify(item => item.Create(
            second,
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<Document>(),
            It.IsAny<ResolvedLocation>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionLocationCannotBeProjected_WHEN_Executing_THEN_ShouldReturnFault()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateAction(roslyn.Solution, "Title", DiscoveredActionKind.Refactoring, new TextSpan(0, 1));
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                It.IsAny<TextSpan>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([action]);

        _workspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns((ResolvedLocation?)null);

        var result = await _target.ExecuteAsync(
            CreateRequest(CodeActionKindSelection.Refactorings, selector),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Faulted);
        result.Error!.Code.Should().Be("CodeActionLocationUnavailable");
        _infoFactory.Verify(item => item.Create(
            It.IsAny<DiscoveredCodeAction>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<Document>(),
            It.IsAny<ResolvedLocation>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionReferenceCapacityIsExceeded_WHEN_Executing_THEN_ShouldReturnActionableRejection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateAction(roslyn.Solution, "Title", DiscoveredActionKind.Refactoring, new TextSpan(0, 1));
        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                It.IsAny<TextSpan>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([action]);

        _infoFactory
            .Setup(item => item.Create(
                action,
                _context.Object,
                roslyn.Document,
                It.IsAny<ResolvedLocation>()))
            .Returns(CodeActionInfoCreationResult.ReferenceCapacityExceeded());

        var result = await _target.ExecuteAsync(
            CreateRequest(CodeActionKindSelection.Refactorings, selector),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("ActionReferenceCapacityExceeded");
        result.Error.Message.Should().Contain("--code-action-reference-cache-size-limit");
        _infoFactory.Verify(item => item.Create(
            action,
            _context.Object,
            roslyn.Document,
            It.IsAny<ResolvedLocation>()), Times.Once);
    }

    [Theory]
    [InlineData("LocationUnavailable", "CodeActionLocationUnavailable")]
    [InlineData("DocumentPathUnavailable", "CodeActionDocumentPathUnavailable")]
    public async Task GIVEN_ActionProjectionCannotBeCompleted_WHEN_Executing_THEN_ShouldReturnFault(
        string failure,
        string expectedCode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateAction(roslyn.Solution, "Title", DiscoveredActionKind.Refactoring, new TextSpan(0, 1));
        var creationResult = failure switch
        {
            "LocationUnavailable" => CodeActionInfoCreationResult.LocationUnavailable(),
            "DocumentPathUnavailable" => CodeActionInfoCreationResult.DocumentPathUnavailable(),
            _ => throw new InvalidOperationException("The test requires a projection failure status."),
        };

        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                It.IsAny<TextSpan>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([action]);

        _infoFactory
            .Setup(item => item.Create(
                action,
                _context.Object,
                roslyn.Document,
                It.IsAny<ResolvedLocation>()))
            .Returns(creationResult);

        var result = await _target.ExecuteAsync(
            CreateRequest(CodeActionKindSelection.Refactorings, selector),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Faulted);
        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_LaterActionExceedsReferenceCapacity_WHEN_Executing_THEN_ShouldRemoveEarlierRequestReferences()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = SetupDocument(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var first = CreateAction(roslyn.Solution, "A", DiscoveredActionKind.Refactoring, new TextSpan(0, 1));
        var second = CreateAction(roslyn.Solution, "B", DiscoveredActionKind.Refactoring, new TextSpan(1, 1));
        var firstItem = CreateItem("A", CodeActionKind.Refactoring, 0, 1);

        _discoveryService.Setup(item => item.GetMatchingRefactoringProviders(null)).Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                provider.Object,
                roslyn.Document,
                It.IsAny<TextSpan>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync([first, second]);

        SetupProjection(first, roslyn.Document, firstItem);
        _infoFactory
            .Setup(item => item.Create(
                second,
                _context.Object,
                roslyn.Document,
                It.IsAny<ResolvedLocation>()))
            .Returns(CodeActionInfoCreationResult.ReferenceCapacityExceeded());

        var result = await _target.ExecuteAsync(
            CreateRequest(CodeActionKindSelection.Refactorings, selector),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("ActionReferenceCapacityExceeded");
        _referenceStore.Verify(item => item.Remove(firstItem.ActionId), Times.Once);
    }

    private DocumentSelector SetupDocument(Document document)
    {
        var selector = new DocumentSelector { Path = "Code.cs" };
        _workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorResolveResult.Resolved(document));

        return selector;
    }

    private void SetupProjection(
        DiscoveredCodeAction action,
        Document document,
        CodeActionListItem item)
    {
        _infoFactory
            .Setup(factory => factory.Create(
                action,
                _context.Object,
                document,
                It.Is<ResolvedLocation>(location =>
                    location.Span!.Start == action.TargetSpan.Start
                    && location.Span.Length == action.TargetSpan.Length)))
            .Returns(CodeActionInfoCreationResult.Success(item));
    }

    private static ListCodeActionsRequest CreateRequest(
        CodeActionKindSelection kinds = CodeActionKindSelection.All,
        DocumentSelector? document = null,
        TextSpanRange? range = null,
        IReadOnlyList<string>? diagnosticIds = null,
        int? limit = 50)
    {
        document ??= new DocumentSelector { Path = "Code.cs" };

        return new ListCodeActionsRequest
        {
            Document = document,
            Range = range,
            Kinds = kinds,
            DiagnosticIds = diagnosticIds,
            Limit = limit,
        };
    }

    private static DiscoveredCodeAction CreateAction(
        Solution solution,
        string title,
        DiscoveredActionKind kind,
        TextSpan targetSpan)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create(title, _ => Task.FromResult(solution), title),
            Kind = kind,
            ProviderId = "ProviderId",
            Title = title,
            TargetSpan = targetSpan,
            EquivalenceKey = title,
        };
    }

    private static CodeActionListItem CreateItem(
        string title,
        CodeActionKind kind,
        int start,
        int length)
    {
        return new CodeActionListItem
        {
            ActionId = Guid.NewGuid(),
            Title = title,
            Kind = kind,
            Location = new CodeActionLocation
            {
                Document = new DocumentReference
                {
                    Path = "Code.cs",
                },
                Span = new TextSpanRange
                {
                    Start = start,
                    Length = length,
                },
            },
        };
    }
}
