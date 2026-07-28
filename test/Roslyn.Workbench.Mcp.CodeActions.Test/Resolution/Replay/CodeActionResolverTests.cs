using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution.Replay;

#pragma warning disable CA1861 // Fresh mutable arrays keep each resolution scenario isolated from other tests.
public sealed class CodeActionResolverTests : IDisposable
{
    private static readonly Guid _actionId = new("11111111-1111-1111-1111-111111111111");

    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionReferenceStore> _referenceStore;
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
        _referenceStore = new Mock<ICodeActionReferenceStore>();
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

        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());

        _workspaceResolver
            .Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult.Resolved(_roslyn.Document));

        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        _context.SetupGet(item => item.TransactionRevision).Returns(2);
        _discoveryService
            .Setup(item => item.FindRefactoringProvider("ProviderId"))
            .Returns(_refactoringProvider.Object);

        _discoveryService
            .Setup(item => item.RediscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_matchingAction]);

        SetupReference(CreateRecipe());

        _target = new CodeActionResolver(
            _discoveryService.Object,
            _diagnosticService.Object,
            _referenceStore.Object,
            new CodeActionToolRequestResolver(new CodeActionScopeResolver()));
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_ResolvingAction_THEN_ShouldThrowBeforeValidatingSnapshot()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.ResolveActionAsync<object>(
            _actionId,
            expectedSnapshot: null,
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
            _actionId,
            expectedSnapshot,
            _context.Object,
            CancellationToken.None);

        result.Rejection!.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Rejection.Error!.Code.Should().Be("SnapshotMismatch");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.None);
        _referenceStore.Verify(
            item => item.TryGet(It.IsAny<Guid>(), out It.Ref<CodeActionReference?>.IsAny),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_ReferenceCannotBeFound_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction()
    {
        CodeActionReference? reference = null;
        _referenceStore.Setup(item => item.TryGet(_actionId, out reference)).Returns(false);

        var result = await ResolveAsync();

        AssertExpired(result);
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
        _workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Theory]
    [InlineData(WorkspaceMismatch.WorkspaceId)]
    [InlineData(WorkspaceMismatch.WorkspaceEpoch)]
    [InlineData(WorkspaceMismatch.TransactionRevision)]
    public async Task GIVEN_ReferenceWorkspaceDoesNotMatch_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction(
        WorkspaceMismatch mismatch)
    {
        var recipe = CreateRecipe();
        recipe = mismatch switch
        {
            WorkspaceMismatch.WorkspaceId => recipe with { WorkspaceId = "OtherWorkspaceId" },
            WorkspaceMismatch.WorkspaceEpoch => recipe with { WorkspaceEpoch = 2 },
            WorkspaceMismatch.TransactionRevision => recipe with { TransactionRevision = 3 },
            _ => recipe,
        };

        SetupReference(recipe);

        var result = await ResolveAsync();

        AssertExpired(result);
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
        _workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound)]
    [InlineData(SelectorResolveStatus.Ambiguous)]
    public async Task GIVEN_ReferenceDocumentDoesNotResolve_WHEN_ResolvingAction_THEN_ShouldReturnExpiredAction(
        SelectorResolveStatus status)
    {
        _workspaceResolver
            .Setup(item => item.ResolveDocument(It.Is<DocumentSelector>(selector =>
                selector.Path == "DocumentPath"
                && selector.Project != null
                && selector.Project.ProjectId == "ProjectId")))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Document>(status));

        var result = await ResolveAsync();

        AssertExpired(result);
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
    }

    [Fact]
    public async Task GIVEN_ReferenceProject_WHEN_ResolvingAction_THEN_ShouldQualifyDocumentSelector()
    {
        var result = await ResolveAsync();

        result.HasRejection.Should().BeFalse();
        _workspaceResolver.Verify(item => item.ResolveDocument(It.Is<DocumentSelector>(selector =>
            selector.Path == "DocumentPath"
            && selector.Project != null
            && selector.Project.ProjectId == "ProjectId")), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LegacyReferenceWithoutProject_WHEN_ResolvingAction_THEN_ShouldUseUnqualifiedDocumentSelector()
    {
        SetupReference(CreateRecipe() with { ProjectId = string.Empty });

        var result = await ResolveAsync();

        result.HasRejection.Should().BeFalse();
        _workspaceResolver.Verify(item => item.ResolveDocument(It.Is<DocumentSelector>(selector =>
            selector.Path == "DocumentPath"
            && selector.Project == null)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RefactoringProviderIsUnavailable_WHEN_ResolvingAction_THEN_ShouldReportUnavailableProvider()
    {
        _discoveryService
            .Setup(item => item.FindRefactoringProvider("ProviderId"))
            .Returns((CodeRefactoringProvider?)null);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.ProviderUnavailable);
        _discoveryService.Verify(item => item.RediscoverRefactoringsAsync(
            It.IsAny<CodeRefactoringProvider>(),
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeFixProviderIsUnavailable_WHEN_ResolvingAction_THEN_ShouldReportUnavailableProvider()
    {
        SetupReference(CreateRecipe() with { Kind = DiscoveredActionKind.CodeFix });
        _discoveryService
            .Setup(item => item.FindCodeFixProvider("ProviderId"))
            .Returns((CodeFixProvider?)null);

        var result = await ResolveAsync();

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
    [InlineData(ActionIdentityMismatch.Diagnostics)]
    [InlineData(ActionIdentityMismatch.Kind)]
    [InlineData(ActionIdentityMismatch.ProviderId)]
    [InlineData(ActionIdentityMismatch.TargetSpan)]
    public async Task GIVEN_RediscoveredActionIdentityDoesNotMatch_WHEN_ResolvingAction_THEN_ShouldRejectAmbiguousAction(
        ActionIdentityMismatch mismatch)
    {
        var action = mismatch switch
        {
            ActionIdentityMismatch.Title => _matchingAction with { Title = "OtherTitle" },
            ActionIdentityMismatch.EquivalenceKey => _matchingAction with { EquivalenceKey = "OtherEquivalenceKey" },
            ActionIdentityMismatch.ActionPath => _matchingAction with { ActionPath = [2] },
            ActionIdentityMismatch.DiagnosticIds => _matchingAction with { DiagnosticIds = ["OtherDiagnosticId"] },
            ActionIdentityMismatch.Diagnostics => _matchingAction with
            {
                Diagnostics =
                [
                    new CodeActionDiagnosticIdentity
                    {
                        Id = "DiagnosticId",
                        Message = "OtherMessage",
                        Start = 3,
                        Length = 4,
                    },
                ],
            },
            ActionIdentityMismatch.Kind => _matchingAction with { Kind = DiscoveredActionKind.CodeFix },
            ActionIdentityMismatch.ProviderId => _matchingAction with { ProviderId = "OtherProviderId" },
            ActionIdentityMismatch.TargetSpan => _matchingAction with { TargetSpan = new TextSpan(4, 4) },
            _ => _matchingAction,
        };

        _discoveryService
            .Setup(item => item.RediscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
    }

    [Fact]
    public async Task GIVEN_MultipleRediscoveredActionsMatch_WHEN_ResolvingAction_THEN_ShouldRejectAmbiguousAction()
    {
        _discoveryService
            .Setup(item => item.RediscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                CancellationToken.None))
            .ReturnsAsync([_matchingAction, _matchingAction]);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionAmbiguous");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
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
            .Setup(item => item.RediscoverRefactoringsAsync(
                _refactoringProvider.Object,
                _roslyn.Document,
                new TextSpan(3, 4),
                CancellationToken.None))
            .ReturnsAsync([hiddenAction]);

        var result = await ResolveAsync();

        result.Rejection!.Error!.Code.Should().Be("ActionUnavailable");
        result.Rejection.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
    }

    [Fact]
    public async Task GIVEN_RefactoringIsRediscoveredUniquely_WHEN_ResolvingAction_THEN_ShouldReturnResolvedAction()
    {
        var result = await ResolveAsync();

        result.HasRejection.Should().BeFalse();
        result.Action.Should().BeSameAs(_matchingAction);
        result.Descriptor.Should().BeSameAs(_visibleDescriptor);
        result.Document.Should().BeSameAs(_roslyn.Document);
        result.Span.Should().Be(new TextSpan(3, 4));
        result.Reference.Should().NotBeNull();
        result.Reference.ActionId.Should().Be(_actionId);
        _discoveryService.Verify(item => item.RediscoverRefactoringsAsync(
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

        var codeFixAction = _matchingAction with { Kind = DiscoveredActionKind.CodeFix };
        SetupReference(CreateRecipe() with { Kind = DiscoveredActionKind.CodeFix });
        _discoveryService
            .Setup(item => item.FindCodeFixProvider("ProviderId"))
            .Returns(_codeFixProvider.Object);

        _diagnosticService
            .Setup(item => item.GetDocumentDiagnosticsAsync(
                _roslyn.Document,
                new TextSpan(3, 4),
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "DiagnosticId" })),
                CancellationToken.None))
            .ReturnsAsync(diagnostics);

        _discoveryService
            .Setup(item => item.RediscoverCodeFixesAsync(
                _codeFixProvider.Object,
                _roslyn.Document,
                diagnostics,
                CancellationToken.None))
            .ReturnsAsync([codeFixAction]);

        var result = await ResolveAsync();

        result.HasRejection.Should().BeFalse();
        result.Action.Should().BeSameAs(codeFixAction);
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            _roslyn.Document,
            new TextSpan(3, 4),
            It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "DiagnosticId" })),
            CancellationToken.None), Times.Once);

        _discoveryService.Verify(item => item.RediscoverCodeFixesAsync(
            _codeFixProvider.Object,
            _roslyn.Document,
            diagnostics,
            CancellationToken.None), Times.Once);
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private ValueTask<CodeActionResolution<object>> ResolveAsync()
    {
        return _target.ResolveActionAsync<object>(
            _actionId,
            expectedSnapshot: null,
            _context.Object,
            CancellationToken.None);
    }

    private void SetupReference(CodeActionReplayRecipe recipe)
    {
        CodeActionReference? reference = new(
            _actionId,
            recipe,
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        _referenceStore
            .Setup(item => item.TryGet(_actionId, out reference))
            .Returns(true);
    }

    private static CodeActionReplayRecipe CreateRecipe()
    {
        return new CodeActionReplayRecipe
        {
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
            Diagnostics =
            [
                new CodeActionDiagnosticIdentity
                {
                    Id = "DiagnosticId",
                    Message = "Message",
                    Start = 3,
                    Length = 4,
                },
            ],
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
            TransactionRevision = 2,
            DocumentPath = "DocumentPath",
            ProjectId = "ProjectId",
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
            TargetSpan = new TextSpan(3, 4),
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
            Diagnostics =
            [
                new CodeActionDiagnosticIdentity
                {
                    Id = "DiagnosticId",
                    Message = "Message",
                    Start = 3,
                    Length = 4,
                },
            ],
        };
    }

    private static void AssertExpired(CodeActionResolution<object> result)
    {
        result.Rejection!.Error!.Code.Should().Be("ActionExpired");
        result.Rejection.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
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
        Diagnostics,
        Kind,
        ProviderId,
        TargetSpan,
    }
#pragma warning restore CA1515
}
#pragma warning restore CA1861
