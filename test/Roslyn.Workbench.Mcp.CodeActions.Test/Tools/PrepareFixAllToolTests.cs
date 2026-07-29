using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class PrepareFixAllToolTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<IFixAllActionFactory> _fixAllActionFactory;
    private readonly Mock<ICodeActionReferenceStore> _referenceStore;
    private readonly Mock<ICodeActionResolver> _resolver;
    private readonly Mock<ICodeActionSolutionChangeCounter> _solutionChangeCounter;
    private readonly Mock<IWorkspaceMutationCandidateProcessor> _candidateProcessor;
    private readonly Mock<TimeProvider> _timeProvider;
    private readonly Mock<ICodeActionQueryContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly PrepareFixAllTool _target;

    public PrepareFixAllToolTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _fixAllActionFactory = new Mock<IFixAllActionFactory>();
        _referenceStore = new Mock<ICodeActionReferenceStore>();
        _resolver = new Mock<ICodeActionResolver>();
        _solutionChangeCounter = new Mock<ICodeActionSolutionChangeCounter>();
        _candidateProcessor = new Mock<IWorkspaceMutationCandidateProcessor>();
        _timeProvider = new Mock<TimeProvider>();
        _context = new Mock<ICodeActionQueryContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();

        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _target = new PrepareFixAllTool(
            _composition.Object,
            _discoveryService.Object,
            _evaluator.Object,
            _fixAllActionFactory.Object,
            _referenceStore.Object,
            _resolver.Object,
            _solutionChangeCounter.Object,
            _candidateProcessor.Object,
            _timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                ReferenceLifetime = TimeSpan.FromMinutes(5),
            }));
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_PreparingFixAll_THEN_ShouldRejectBeforeResolution()
    {
        _composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Unavailable("Unavailable."));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _resolver.Verify(item => item.ResolveActionAsync<PrepareFixAllData>(
            It.IsAny<Guid>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(50, -1)]
    public async Task GIVEN_NegativeLimit_WHEN_PreparingFixAll_THEN_ShouldRejectRequest(
        int maxChanges,
        int affectedDocumentsLimit)
    {
        var result = await _target.ExecuteAsync(
            CreateRequest(maxChanges, affectedDocumentsLimit),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Theory]
    [InlineData(null, null, 50, 20)]
    [InlineData(12, 7, 12, 7)]
    public void GIVEN_OptionalLimits_WHEN_GettingEffectiveLimits_THEN_ShouldUseRequestedOrPublishedValues(
        int? maxChanges,
        int? affectedDocumentsLimit,
        int expectedMaxChanges,
        int expectedAffectedDocumentsLimit)
    {
        var request = CreateRequest(maxChanges, affectedDocumentsLimit);

        request.EffectiveMaxChanges.Should().Be(expectedMaxChanges);
        request.EffectiveAffectedDocumentsLimit.Should().Be(expectedAffectedDocumentsLimit);
    }

    [Fact]
    public async Task GIVEN_UndefinedScope_WHEN_PreparingFixAll_THEN_ShouldRejectRequest()
    {
        var result = await _target.ExecuteAsync(
            CreateRequest(scope: (CodeActionFixAllScope)int.MaxValue),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_OriginResolutionFails_WHEN_PreparingFixAll_THEN_ShouldReturnResolutionFailure()
    {
        var rejection = CodeActionExecutionResultFactory.ActionExpired<PrepareFixAllData>();
        SetupResolution(CodeActionResolution.Rejected(rejection));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(rejection);
    }

    [Fact]
    public async Task GIVEN_OriginIsRefactoring_WHEN_PreparingFixAll_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupResolution(CreateResolution(roslyn.Document, DiscoveredActionKind.Refactoring, [CodeActionFixAllScope.Document]));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_OriginIsAlreadyPrepared_WHEN_PreparingFixAll_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var resolution = CreateResolution(
            roslyn.Document,
            DiscoveredActionKind.CodeFix,
            [CodeActionFixAllScope.Document]);

        var preparedReference = new CodeActionReference(
            resolution.Reference!.ActionId,
            resolution.Reference.Recipe with
            {
                PreparedFixAllScope = CodeActionFixAllScope.Document,
            },
            resolution.Reference.ExpiresAt);

        SetupResolution(new CodeActionResolution<PrepareFixAllData>(
            rejection: null,
            CodeActionResolutionFailureKind.None,
            resolution.Action,
            resolution.Descriptor,
            resolution.Document,
            resolution.Span,
            preparedReference));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Message.Should().Contain("already represents");
    }

    [Fact]
    public async Task GIVEN_ScopeIsNotAdvertised_WHEN_PreparingFixAll_THEN_ShouldRejectScope()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupResolution(CreateResolution(roslyn.Document, DiscoveredActionKind.CodeFix, [CodeActionFixAllScope.Project]));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Message.Should().Contain("does not support");
    }

    [Fact]
    public async Task GIVEN_ProviderIsUnavailable_WHEN_PreparingFixAll_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupResolution(CreateResolution(roslyn.Document, DiscoveredActionKind.CodeFix, [CodeActionFixAllScope.Document]));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_FixAllCreationFails_WHEN_PreparingFixAll_THEN_ShouldReturnCreationFailure()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var (provider, fixAllProvider) = SetupProvider(roslyn.Document);
        SetupDocumentCreation(
            provider,
            fixAllProvider,
            roslyn.Document,
            FixAllActionCreationResult.Failed(new FixAllActionCreationFailure { Message = "Creation failed." }));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Message.Should().Be("Creation failed.");
    }

    [Fact]
    public async Task GIVEN_EvaluationFails_WHEN_PreparingFixAll_THEN_ShouldReturnEvaluationFailure()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CodeAction.Create("Fix all", _ => Task.FromResult(roslyn.Solution));
        var (provider, fixAllProvider) = SetupProvider(roslyn.Document);
        SetupDocumentCreation(provider, fixAllProvider, roslyn.Document, FixAllActionCreationResult.Created(action));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, TestContext.Current.CancellationToken))
            .ReturnsAsync(CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.UnsupportedActionOperation,
                "Evaluation failed."));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Fact]
    public async Task GIVEN_FixAllChangesUnsupportedWorkspaceState_WHEN_PreparingFixAll_THEN_ShouldRejectBeforeCreatingReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupSuccessfulEvaluation(roslyn, CodeActionFixAllScope.Document, []);
        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                roslyn.Solution,
                roslyn.Solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(new WorkspaceOperationError
            {
                Code = "UnsupportedChange",
                Message = "Mutation proposals must not alter project references.",
            }));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedChange");
        _solutionChangeCounter.Verify(item => item.GetChangedSourceDocumentsAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _referenceStore.Verify(item => item.TryCreate(
            It.IsAny<CodeActionReplayRecipe>(),
            It.IsAny<DateTimeOffset>(),
            out It.Ref<CodeActionReference?>.IsAny), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentChangesCannotBeMerged_WHEN_PreparingFixAll_THEN_ShouldReturnMergeFailure()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupSuccessfulEvaluation(roslyn, CodeActionFixAllScope.Document, []);
        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                roslyn.Solution,
                roslyn.Solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(new WorkspaceOperationError
            {
                Code = "LinkedDocumentConflict",
                Message = "Linked document changes conflict.",
            }));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("LinkedDocumentConflict");
        _referenceStore.Verify(item => item.TryCreate(
            It.IsAny<CodeActionReplayRecipe>(),
            It.IsAny<DateTimeOffset>(),
            out It.Ref<CodeActionReference?>.IsAny), Times.Never);
    }

    [Fact]
    public async Task GIVEN_MergedFixAllChangesUnsupportedWorkspaceState_WHEN_PreparingFixAll_THEN_ShouldRejectMergedCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupSuccessfulEvaluation(roslyn, CodeActionFixAllScope.Document, []);
        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                roslyn.Solution,
                roslyn.Solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(new WorkspaceOperationError
            {
                Code = "UnsupportedChange",
                Message = "Merged changes are unsupported.",
            }));

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedChange");
        _solutionChangeCounter.Verify(item => item.GetChangedSourceDocumentsAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ChangedDocumentLimitIsExceeded_WHEN_PreparingFixAll_THEN_ShouldRejectWithoutStoringReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var changedDocuments = new[] { roslyn.Document, roslyn.Document };
        SetupSuccessfulEvaluation(roslyn, CodeActionFixAllScope.Document, changedDocuments);

        var result = await _target.ExecuteAsync(
            CreateRequest(maxChanges: 1),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("FixAllLimitExceeded");
        _referenceStore.Verify(item => item.TryCreate(
            It.IsAny<CodeActionReplayRecipe>(),
            It.IsAny<DateTimeOffset>(),
            out It.Ref<CodeActionReference?>.IsAny), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ReferenceStoreRejectsPreparedRecipe_WHEN_PreparingFixAll_THEN_ShouldRejectCapacity()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupSuccessfulEvaluation(roslyn, CodeActionFixAllScope.Document, []);
        CodeActionReference? rejectedReference = null;
        _referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                It.IsAny<DateTimeOffset>(),
                out rejectedReference))
            .Returns(false);

        var result = await _target.ExecuteAsync(
            CreateRequest(),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("ActionReferenceCapacityExceeded");
    }

    [Fact]
    public async Task GIVEN_FixAllHasNoChangesAndLimitsAreOmitted_WHEN_PreparingFixAll_THEN_ShouldReturnEmptyImpact()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupSuccessfulEvaluation(roslyn, CodeActionFixAllScope.Document, []);
        var preparedReference = new CodeActionReference(
            Guid.NewGuid(),
            CodeActionExecutionTestFactory.CreateReplayRecipe(),
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        _referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                It.IsAny<DateTimeOffset>(),
                out preparedReference))
            .Returns(true);

        var result = await _target.ExecuteAsync(
            CreateRequest(maxChanges: null, affectedDocumentsLimit: null),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Data!.AffectedDocuments.Items.Should().BeEmpty();
        result.Data.AffectedDocuments.TotalCount.Should().Be(0);
        result.Data.AffectedDocuments.HasMore.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GIVEN_SupportedScope_WHEN_PreparingFixAll_THEN_ShouldReturnBoundedImpactAndPreparedReference(
        int scopeValue)
    {
        var scope = (CodeActionFixAllScope)scopeValue;
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var changedDocuments = new[] { roslyn.Document, roslyn.Document };
        SetupSuccessfulEvaluation(roslyn, scope, changedDocuments);
        var documentReferences = new[]
        {
            new DocumentReference { Path = "First.cs" },
            new DocumentReference { Path = "Second.cs" },
        };

        _workspaceResolver
            .SetupSequence(item => item.CreateDocumentReference(roslyn.Document))
            .Returns(documentReferences[0])
            .Returns(documentReferences[1]);

        var preparedReference = new CodeActionReference(
            Guid.NewGuid(),
            CodeActionExecutionTestFactory.CreateReplayRecipe(),
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        _referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero),
                out preparedReference))
            .Returns(true);

        var affectedDocumentsLimit = scope == CodeActionFixAllScope.Document ? 2 : 1;

        var result = await _target.ExecuteAsync(
            CreateRequest(scope: scope, affectedDocumentsLimit: affectedDocumentsLimit),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Data!.ActionId.Should().Be(preparedReference.ActionId);
        result.Data.Scope.Should().Be(scope);
        result.Data.AffectedDiagnosticCount.Should().BeNull();
        result.Data.AffectedDocuments.TotalCount.Should().Be(2);
        result.Data.AffectedDocuments.Items.Should().Equal(documentReferences.Take(affectedDocumentsLimit));
        result.Data.AffectedDocuments.HasMore.Should().Be(
            changedDocuments.Length > affectedDocumentsLimit);

        _referenceStore.Verify(item => item.TryCreate(
            It.Is<CodeActionReplayRecipe>(recipe => recipe.PreparedFixAllScope == scope),
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero),
            out preparedReference), Times.Once);
    }

    private void SetupSuccessfulEvaluation(
        InMemoryRoslynDocument roslyn,
        CodeActionFixAllScope scope,
        IReadOnlyList<Document> changedDocuments)
    {
        var action = CodeAction.Create("Fix all", _ => Task.FromResult(roslyn.Solution));
        var (provider, fixAllProvider) = SetupProvider(roslyn.Document, scope);
        var creation = FixAllActionCreationResult.Created(action);
        if (scope == CodeActionFixAllScope.Project)
        {
            _fixAllActionFactory
                .Setup(item => item.CreateProjectAsync(
                    provider,
                    fixAllProvider,
                    roslyn.Document.Project,
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string?>(),
                    null,
                    TestContext.Current.CancellationToken))
                .ReturnsAsync(creation);
        }
        else if (scope == CodeActionFixAllScope.Document)
        {
            SetupDocumentCreation(provider, fixAllProvider, roslyn.Document, creation);
        }
        else
        {
            SetupSolutionCreation(provider, fixAllProvider, roslyn.Document, creation);
        }

        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _evaluator
            .Setup(item => item.EvaluateAsync(action, roslyn.Solution, TestContext.Current.CancellationToken))
            .ReturnsAsync(CodeActionApplyResult.Applied(roslyn.Solution));

        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                roslyn.Solution,
                roslyn.Solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Succeeded(roslyn.Solution));

        _solutionChangeCounter
            .Setup(item => item.GetChangedSourceDocumentsAsync(
                roslyn.Solution,
                roslyn.Solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(changedDocuments);
    }

    private (CodeFixProvider Provider, FixAllProvider FixAllProvider) SetupProvider(
        Document document,
        CodeActionFixAllScope scope = CodeActionFixAllScope.Document)
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        provider.Setup(item => item.GetFixAllProvider()).Returns(fixAllProvider.Object);
        _discoveryService.Setup(item => item.FindCodeFixProvider("ProviderId")).Returns(provider.Object);
        SetupResolution(CreateResolution(document, DiscoveredActionKind.CodeFix, [scope]));
        return (provider.Object, fixAllProvider.Object);
    }

    private void SetupDocumentCreation(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        FixAllActionCreationResult result)
    {
        _fixAllActionFactory
            .Setup(item => item.CreateDocumentAsync(
                provider,
                fixAllProvider,
                document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                null,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(result);
    }

    private void SetupSolutionCreation(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        FixAllActionCreationResult result)
    {
        _fixAllActionFactory
            .Setup(item => item.CreateSolutionAsync(
                provider,
                fixAllProvider,
                originDocument,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                null,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(result);
    }

    private void SetupResolution(CodeActionResolution<PrepareFixAllData> resolution)
    {
        _resolver
            .Setup(item => item.ResolveActionAsync<PrepareFixAllData>(
                Guid.Empty,
                It.IsAny<SnapshotPrecondition>(),
                _context.Object,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(resolution);
    }

    private static CodeActionResolution<PrepareFixAllData> CreateResolution(
        Document document,
        DiscoveredActionKind kind,
        IReadOnlyList<CodeActionFixAllScope> scopes)
    {
        var recipe = CodeActionExecutionTestFactory.CreateReplayRecipe() with
        {
            ProviderId = "ProviderId",
        };

        var action = new DiscoveredCodeAction
        {
            Action = CodeAction.Create("Title", _ => Task.FromResult(document.Project.Solution)),
            Kind = kind,
            ProviderId = "ProviderId",
            Title = "Title",
            Descriptor = new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            },
            TargetSpan = default,
            EquivalenceKey = "EquivalenceKey",
            DiagnosticIds = ["DiagnosticId"],
            FixAllScopes = scopes,
        };

        var expiresAt = new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero);
        var reference = new CodeActionReference(Guid.Empty, recipe, expiresAt);

        return CodeActionResolution.Resolved<PrepareFixAllData>(
            action,
            document,
            default,
            reference);
    }

    private static PrepareFixAllRequest CreateRequest(
        int? maxChanges = 50,
        int? affectedDocumentsLimit = 20,
        CodeActionFixAllScope scope = CodeActionFixAllScope.Document)
    {
        return new PrepareFixAllRequest
        {
            ActionId = Guid.Empty,
            Scope = scope,
            MaxChanges = maxChanges,
            AffectedDocumentsLimit = affectedDocumentsLimit,
            ExpectedSnapshot = new SnapshotPrecondition(),
        };
    }
}
