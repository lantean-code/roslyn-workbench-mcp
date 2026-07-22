using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution.Replay;

#pragma warning disable CA1861 // Fresh mutable arrays keep each resolution scenario isolated from other tests.
public sealed class CodeActionResolverTests : IDisposable
{
    private static readonly DateTimeOffset _utcNow = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionTokenService> _tokenService;
    private readonly Mock<TimeProvider> _timeProvider;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly Mock<CodeRefactoringProvider> _refactoringProvider;
    private readonly Mock<CodeFixProvider> _codeFixProvider;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly DiscoveredCodeAction _matchingAction;
    private readonly CodeActionDescriptorEntry _visibleDescriptor;
    private readonly CodeActionResolver _target;

    public CodeActionResolverTests()
    {
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _tokenService = new Mock<ICodeActionTokenService>();
        _timeProvider = new Mock<TimeProvider>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _refactoringProvider = new Mock<CodeRefactoringProvider>();
        _codeFixProvider = new Mock<CodeFixProvider>();
        _roslyn = RoslynTestFactory.CreateDocument("class C { }");
        _visibleDescriptor = new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Replay,
        };

        _matchingAction = CreateAction();

        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());
        _workspaceResolver
            .Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult<Document>.Resolved(_roslyn.Document));
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        _context.SetupGet(item => item.TransactionRevision).Returns(2);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([_refactoringProvider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_matchingAction]);
        SetupToken(CreatePayload());

        _target = new CodeActionResolver(
            _discoveryService.Object,
            _diagnosticService.Object,
            _tokenService.Object,
            new CodeActionToolRequestResolver(new CodeActionScopeResolver()),
            _timeProvider.Object);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_ResolvingAction_THEN_ShouldThrowBeforeValidatingSnapshot()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.ResolveActionAsync<object>(
            "ActionId",
            expectedSnapshot: null,
            expectedKind: null,
            _context.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_ResolvingAction_THEN_ShouldReturnSnapshotConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(expectedSnapshot))
            .Returns(SnapshotMatchResult.WorkspaceEpochMismatch());

        var result = await _target.ResolveActionAsync<object>(
            "ActionId",
            expectedSnapshot,
            expectedKind: null,
            _context.Object,
            CancellationToken.None);

        result.Rejection!.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Rejection.Error!.Code.Should().Be("SnapshotMismatch");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.None);
        _tokenService.Verify(item => item.TryDecode(It.IsAny<string>(), out It.Ref<CodeActionTokenPayload>.IsAny), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TokenCannotBeDecoded_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction()
    {
        var payload = new CodeActionTokenPayload();
        _tokenService.Setup(item => item.TryDecode("ActionId", out payload)).Returns(false);

        var result = await ResolveAsync(DiscoveredActionKind.CodeFix);

        AssertExpired(result);
        _workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TokenKindIsInvalid_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction()
    {
        SetupToken(CreatePayload() with { Kind = "InvalidKind" });

        var result = await ResolveAsync();

        AssertExpired(result);
        _timeProvider.Verify(item => item.GetUtcNow(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TokenKindDoesNotMatchExpectedKind_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction()
    {
        var result = await ResolveAsync(DiscoveredActionKind.CodeFix);

        AssertExpired(result);
        _timeProvider.Verify(item => item.GetUtcNow(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TokenExpiryIsMalformed_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction()
    {
        SetupToken(CreatePayload() with { ExpiresAt = "ExpiresAt" });

        var result = await ResolveAsync();

        AssertExpired(result);
    }

    [Fact]
    public async Task GIVEN_TokenHasExpired_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction()
    {
        SetupToken(CreatePayload() with { ExpiresAt = _utcNow.AddTicks(-1).ToString("O") });

        var result = await ResolveAsync();

        AssertExpired(result);
    }

    [Theory]
    [InlineData(WorkspaceMismatch.WorkspaceId)]
    [InlineData(WorkspaceMismatch.WorkspaceEpoch)]
    [InlineData(WorkspaceMismatch.TransactionRevision)]
    public async Task GIVEN_TokenWorkspaceDoesNotMatch_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction(
        WorkspaceMismatch mismatch)
    {
        var payload = CreatePayload();
        payload = mismatch switch
        {
            WorkspaceMismatch.WorkspaceId => payload with { WorkspaceId = "OtherWorkspaceId" },
            WorkspaceMismatch.WorkspaceEpoch => payload with { WorkspaceEpoch = 2 },
            WorkspaceMismatch.TransactionRevision => payload with { TransactionRevision = 3 },
            _ => payload,
        };

        SetupToken(payload);

        var result = await ResolveAsync();

        AssertExpired(result);
        _workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound)]
    [InlineData(SelectorResolveStatus.Ambiguous)]
    public async Task GIVEN_TokenDocumentDoesNotResolve_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction(
        SelectorResolveStatus status)
    {
        _workspaceResolver
            .Setup(item => item.ResolveDocument(It.Is<DocumentSelector>(selector => selector.Path == "DocumentPath")))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Document>(status));

        var result = await ResolveAsync();

        AssertExpired(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task GIVEN_RefactoringProviderIsNotUnique_WHEN_ResolvingAction_THEN_ShouldReportUnavailableProvider(
        int providerCount)
    {
        var firstProvider = new Mock<CodeRefactoringProvider>();
        var secondProvider = new Mock<CodeRefactoringProvider>();
        IReadOnlyList<CodeRefactoringProvider> providers = providerCount == 0
            ? []
            : [firstProvider.Object, secondProvider.Object];
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns(providers);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.ProviderUnavailable);
        _discoveryService.Verify(item => item.DiscoverRefactoringsAsync(
            It.IsAny<CodeRefactoringProvider>(),
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeFixProviderIsNotUnique_WHEN_ResolvingAction_THEN_ShouldReportUnavailableProvider()
    {
        SetupToken(CreatePayload() with { Kind = DiscoveredActionKind.CodeFix.ToString() });
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([]);

        var result = await ResolveAsync(DiscoveredActionKind.CodeFix);

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.ProviderUnavailable);
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ActionIdentityMismatch.Title)]
    [InlineData(ActionIdentityMismatch.EquivalenceKey)]
    [InlineData(ActionIdentityMismatch.ActionPath)]
    [InlineData(ActionIdentityMismatch.DiagnosticIds)]
    public async Task GIVEN_RediscoveredActionIdentityDoesNotMatch_WHEN_ResolvingAction_THEN_ShouldRejectAmbiguousAction(
        ActionIdentityMismatch mismatch)
    {
        var action = mismatch switch
        {
            ActionIdentityMismatch.Title => _matchingAction with { Title = "OtherTitle" },
            ActionIdentityMismatch.EquivalenceKey => _matchingAction with { EquivalenceKey = "OtherEquivalenceKey" },
            ActionIdentityMismatch.ActionPath => _matchingAction with { ActionPath = [2] },
            ActionIdentityMismatch.DiagnosticIds => _matchingAction with { DiagnosticIds = ["OtherDiagnosticId"] },
            _ => _matchingAction,
        };

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.None);
    }

    [Fact]
    public async Task GIVEN_MultipleRediscoveredActionsMatch_WHEN_ResolvingAction_THEN_ShouldRejectAmbiguousAction()
    {
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                CancellationToken.None))
            .ReturnsAsync([_matchingAction, _matchingAction]);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
    }

    [Fact]
    public async Task GIVEN_RediscoveredActionIsHidden_WHEN_ResolvingAction_THEN_ShouldRejectUnavailableAction()
    {
        var hiddenAction = _matchingAction with
        {
            Descriptor = new CodeActionDescriptorEntry
            {
                IsVisible = false,
            },
        };

        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                CancellationToken.None))
            .ReturnsAsync([hiddenAction]);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionUnavailable");
        result.Rejection.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_RefactoringIsRediscoveredUniquely_WHEN_ResolvingAction_THEN_ShouldReturnResolvedAction()
    {
        var result = await ResolveAsync(expectedKind: null);

        result.HasRejection.Should().BeFalse();
        result.Action.Should().BeSameAs(_matchingAction);
        result.Descriptor.Should().BeSameAs(_visibleDescriptor);
        result.Document.Should().BeSameAs(_roslyn.Document);
        result.Span.Should().Be(new TextSpan(3, 4));
        _discoveryService.Verify(item => item.DiscoverRefactoringsAsync(
            _refactoringProvider.Object,
            _roslyn.Document,
            new TextSpan(3, 4),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CodeFixIsRediscoveredUniquely_WHEN_ResolvingAction_THEN_ShouldUseDocumentDiagnostics()
    {
        IReadOnlyList<Diagnostic> diagnostics = [Diagnostic.Create(
            new DiagnosticDescriptor(
                "DiagnosticId",
                "Title",
                "Message",
                "Category",
                Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            Location.None)];

        SetupToken(CreatePayload() with { Kind = DiscoveredActionKind.CodeFix.ToString() });
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders("ProviderId"))
            .Returns([_codeFixProvider.Object]);
        _diagnosticService
            .Setup(item => item.GetDocumentDiagnosticsAsync(
                _roslyn.Document,
                new TextSpan(3, 4),
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "DiagnosticId" })),
                CancellationToken.None))
            .ReturnsAsync(diagnostics);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _codeFixProvider.Object,
                _roslyn.Document,
                diagnostics,
                CancellationToken.None))
            .ReturnsAsync([_matchingAction]);

        var result = await ResolveAsync(DiscoveredActionKind.CodeFix);

        result.HasRejection.Should().BeFalse();
        result.Action.Should().BeSameAs(_matchingAction);
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            _roslyn.Document,
            new TextSpan(3, 4),
            It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "DiagnosticId" })),
            CancellationToken.None), Times.Once);

        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            _codeFixProvider.Object,
            _roslyn.Document,
            diagnostics,
            CancellationToken.None), Times.Once);
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private ValueTask<CodeActionResolution<object>> ResolveAsync(
        DiscoveredActionKind? expectedKind = DiscoveredActionKind.Refactoring)
    {
        return _target.ResolveActionAsync<object>(
            "ActionId",
            expectedSnapshot: null,
            expectedKind,
            _context.Object,
            CancellationToken.None);
    }

    private void SetupToken(CodeActionTokenPayload payload)
    {
        _tokenService
            .Setup(item => item.TryDecode("ActionId", out payload))
            .Returns(true);
    }

    private static CodeActionTokenPayload CreatePayload()
    {
        return new CodeActionTokenPayload
        {
            Kind = DiscoveredActionKind.Refactoring.ToString(),
            ProviderId = "ProviderId",
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
            TransactionRevision = 2,
            ExpiresAt = _utcNow.AddHours(1).ToString("O"),
            DocumentPath = "DocumentPath",
            Start = 3,
            Length = 4,
        };
    }

    private DiscoveredCodeAction CreateAction()
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create("Title", _ => Task.FromResult(_roslyn.Solution), "EquivalenceKey"),
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = "Title",
            Descriptor = _visibleDescriptor,
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
        };
    }

    private static void AssertExpired(CodeActionResolution<object> result)
    {
        result.Rejection!.Error!.Code.Should().Be("ActionExpired");
        result.Rejection.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.None);
    }

#pragma warning disable CA1515 // These enums are part of public xUnit theory method signatures.
    public enum WorkspaceMismatch
    {
        WorkspaceId,
        WorkspaceEpoch,
        TransactionRevision,
    }

    public enum ActionIdentityMismatch
    {
        Title,
        EquivalenceKey,
        ActionPath,
        DiagnosticIds,
    }
#pragma warning restore CA1515
}
#pragma warning restore CA1861
