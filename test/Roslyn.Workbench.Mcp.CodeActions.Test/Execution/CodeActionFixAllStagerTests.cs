using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

public sealed class CodeActionFixAllStagerTests : IDisposable
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionResolver> _resolver;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<IFixAllActionFactory> _fixAllActionFactory;
    private readonly Mock<ICodeActionSolutionChangeCounter> _solutionChangeCounter;
    private readonly Mock<ICodeActionScopeResolver> _scopeResolver;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly Mock<CodeFixProvider> _provider;
    private readonly Mock<FixAllProvider> _fixAllProvider;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly CodeAction _fixAllAction;
    private readonly DiscoveredCodeAction _discoveredAction;
    private readonly CodeActionFixAllStager _target;

    public CodeActionFixAllStagerTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _resolver = new Mock<ICodeActionResolver>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _fixAllActionFactory = new Mock<IFixAllActionFactory>();
        _solutionChangeCounter = new Mock<ICodeActionSolutionChangeCounter>();
        _scopeResolver = new Mock<ICodeActionScopeResolver>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _provider = new Mock<CodeFixProvider>();
        _fixAllProvider = new Mock<FixAllProvider>();
        _roslyn = RoslynTestFactory.CreateDocument("class C { }");
        _fixAllAction = CodeAction.Create(
            "Fix all",
            _ => Task.FromResult(_roslyn.Solution),
            "FixAllEquivalenceKey");
        _discoveredAction = CreateDiscoveredAction(_roslyn.Solution);
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });

        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _context.SetupGet(item => item.CurrentSolution).Returns(_roslyn.Solution);
        SetupDefaultScopeResolutions();
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                It.IsAny<string>(),
                It.IsAny<SnapshotPrecondition?>(),
                DiscoveredActionKind.CodeFix,
                _context.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolution());
        _discoveryService.Setup(item => item.FindCodeFixProvider("ProviderId")).Returns(_provider.Object);
        _provider.Setup(item => item.GetFixAllProvider()).Returns(_fixAllProvider.Object);
        _fixAllActionFactory
            .Setup(item => item.CreateAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Document>(),
                new TextSpan(0, 1),
                It.IsAny<FixAllScope>(),
                _discoveredAction.DiagnosticIds,
                "EquivalenceKey",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixAllActionCreationResult.Created(_fixAllAction));

        _fixAllActionFactory
            .Setup(item => item.CreateAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Project>(),
                _discoveredAction.DiagnosticIds,
                "EquivalenceKey",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixAllActionCreationResult.Created(_fixAllAction));

        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeActionApplyResult.Applied(_roslyn.Solution));

        _solutionChangeCounter
            .Setup(item => item.CountChangedSourceDocumentsAsync(
                _roslyn.Solution,
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _target = new CodeActionFixAllStager(
            _providerCatalog.Object,
            _discoveryService.Object,
            _resolver.Object,
            _evaluator.Object,
            _fixAllActionFactory.Object,
            new CodeActionToolRequestResolver(_scopeResolver.Object),
            _solutionChangeCounter.Object);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_StagingFixAll_THEN_ShouldThrowBeforeCheckingAvailability()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _providerCatalog.VerifyGet(item => item.Status, Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingFixAll_THEN_ShouldRejectWithoutResolvingAction()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _resolver.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<string>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<DiscoveredActionKind?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_StagingFixAll_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        var rejection = CodeActionExecutionResult<WorkspaceMutationCandidate>.Conflict(
            new CodeActionExecutionError
            {
                Code = "SnapshotMismatch",
                Message = "Message",
            });

        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                "ActionId",
                expectedSnapshot,
                DiscoveredActionKind.CodeFix,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution<WorkspaceMutationCandidate>.Rejected(rejection));

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution) with
            {
                ExpectedSnapshot = expectedSnapshot,
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_ScopeIsMissing_WHEN_StagingFixAll_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageFixAllAsync(
            new StageFixAllRequest
            {
                ActionId = "ActionId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
        _resolver.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<string>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<DiscoveredActionKind?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolutionIsRejected_WHEN_StagingFixAll_THEN_ShouldReturnRejection()
    {
        var rejection = CreateRejection();
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                "ActionId",
                null,
                DiscoveredActionKind.CodeFix,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution<WorkspaceMutationCandidate>.Rejected(rejection));

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_ResolutionReportsUnavailableProvider_WHEN_StagingFixAll_THEN_ShouldRejectFixAll()
    {
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                "ActionId",
                null,
                DiscoveredActionKind.CodeFix,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution<WorkspaceMutationCandidate>.Rejected(
                CreateRejection(),
                CodeActionResolutionFailureKind.ProviderUnavailable));

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_OriginProviderIsUnavailable_WHEN_StagingFixAll_THEN_ShouldRejectFixAll()
    {
        _discoveryService.Setup(item => item.FindCodeFixProvider("ProviderId")).Returns((CodeFixProvider?)null);

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_ProviderHasNoFixAllProvider_WHEN_StagingFixAll_THEN_ShouldRejectFixAll()
    {
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_FixAllActionCannotBeCreated_WHEN_StagingFixAll_THEN_ShouldRejectWithoutEvaluation()
    {
        var failure = new FixAllActionCreationFailure
        {
            Message = "The fix-all action could not be created.",
        };
        _fixAllActionFactory
            .Setup(item => item.CreateAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Document>(),
                It.IsAny<TextSpan>(),
                FixAllScope.Solution,
                _discoveredAction.DiagnosticIds,
                "EquivalenceKey",
                null,
                CancellationToken.None))
            .ReturnsAsync(FixAllActionCreationResult.Failed(failure));

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
        result.Error.Message.Should().Be(failure.Message);
        _evaluator.Verify(item => item.EvaluateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_OriginDocumentIsMissingFromWorkingSolution_WHEN_StagingSolutionFixAll_THEN_ShouldRejectExpiredAction()
    {
        using var workspace = new AdhocWorkspace();
        _context.SetupGet(item => item.CurrentSolution).Returns(workspace.CurrentSolution);

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionExpired");
        _fixAllActionFactory.Verify(item => item.CreateAsync(
            It.IsAny<CodeFixProvider>(),
            It.IsAny<FixAllProvider>(),
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<FixAllScope>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsSolutionFixAll_WHEN_StagingFixAll_THEN_ShouldReturnEvaluatorRejection()
    {
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_TargetDocumentIsMissingFromWorkingSolution_WHEN_StagingDocumentFixAll_THEN_ShouldRejectDocument()
    {
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new DocumentSelector { Path = "TargetDocumentPath" };
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Document),
                _roslyn.Solution,
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved([otherRoslyn.Document]));

        var result = await _target.StageFixAllAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("DocumentNotFound");
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsDocumentFixAll_WHEN_StagingFixAll_THEN_ShouldReturnEvaluatorRejection()
    {
        var selector = new DocumentSelector { Path = "TargetDocumentPath" };
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageFixAllAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_TargetProjectIsMissingFromWorkingSolution_WHEN_StagingProjectFixAll_THEN_ShouldRejectProject()
    {
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new ProjectSelector { Name = "ProjectName" };
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Project),
                _roslyn.Solution,
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved(
                [otherRoslyn.Document],
                [otherRoslyn.Document.Project]));

        var result = await _target.StageFixAllAsync(
            CreateProjectRequest(ScopeKind.Project, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ProjectNotFound");
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsProjectFixAll_WHEN_StagingFixAll_THEN_ShouldReturnEvaluatorRejection()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageFixAllAsync(
            CreateProjectRequest(ScopeKind.Project, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsProjectInSet_WHEN_StagingProjectsFixAll_THEN_ShouldReturnEvaluatorRejection()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageFixAllAsync(
            CreateProjectRequest(ScopeKind.Projects, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_TargetProjectInSetIsMissingFromWorkingSolution_WHEN_StagingProjectsFixAll_THEN_ShouldRejectProject()
    {
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new ProjectSelector { Name = "ProjectName" };
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Projects),
                _roslyn.Solution,
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved(
                [otherRoslyn.Document],
                [otherRoslyn.Document.Project]));

        var result = await _target.StageFixAllAsync(
            CreateProjectRequest(ScopeKind.Projects, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ProjectNotFound");
        _fixAllActionFactory.Verify(item => item.CreateAsync(
            It.IsAny<CodeFixProvider>(),
            It.IsAny<FixAllProvider>(),
            It.IsAny<Project>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_MultipleProjectsAreSelected_WHEN_StagingProjectsFixAll_THEN_ShouldApplyEachProject()
    {
        using var solution = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var firstProject = solution.Solution.Projects.Single(project => project.Name == "FirstProject");
        var secondProject = solution.Solution.Projects.Single(project => project.Name == "SecondProject");
        var firstSelector = new ProjectSelector { Name = "FirstProject" };
        var secondSelector = new ProjectSelector { Name = "SecondProject" };
        _context.SetupGet(item => item.CurrentSolution).Returns(solution.Solution);
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Projects),
                solution.Solution,
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved(
                firstProject.Documents.Concat(secondProject.Documents).ToArray(),
                [firstProject, secondProject]));

        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                It.IsAny<Solution>(),
                CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(solution.Solution));

        var result = await _target.StageFixAllAsync(
            CreateProjectRequest(ScopeKind.Projects, [firstSelector, secondSelector]),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _fixAllActionFactory.Verify(item => item.CreateAsync(
            _provider.Object,
            _fixAllProvider.Object,
            It.IsAny<Project>(),
            _discoveredAction.DiagnosticIds,
            "EquivalenceKey",
            null,
            CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_ScopeKindIsUnsupported_WHEN_StagingFixAll_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageFixAllAsync(
            CreateRequest((ScopeKind)int.MaxValue),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Theory]
    [InlineData(null, 3, false)]
    [InlineData(3, 3, false)]
    [InlineData(2, 3, true)]
    public async Task GIVEN_ChangeLimit_WHEN_StagingFixAll_THEN_ShouldEnforceMaximum(
        int? maxChanges,
        int changedDocumentCount,
        bool expectedRejection)
    {
        _solutionChangeCounter
            .Setup(item => item.CountChangedSourceDocumentsAsync(
                _roslyn.Solution,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(changedDocumentCount);

        var result = await _target.StageFixAllAsync(
            CreateRequest(ScopeKind.Solution) with
            {
                MaxChanges = maxChanges,
            },
            _context.Object,
            CancellationToken.None);

        if (expectedRejection)
        {
            result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
            result.Error!.Code.Should().Be("FixAllLimitExceeded");
            result.RequiredAction.Should().Be(RequiredAction.NarrowRequest);
        }
        else
        {
            result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
            result.Data!.CandidateSolution.Should().BeSameAs(_roslyn.Solution);
            result.Data.Summary.Should().Be("Fix all: Title");
        }
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private void SetupDefaultScopeResolutions()
    {
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Solution),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns((ScopeSelector _, Solution solution, IWorkspaceResolver _) =>
                CodeActionScopeResolution.Resolved(
                    solution.Projects.SelectMany(static project => project.Documents).ToArray()));

        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Document),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved([_roslyn.Document]));

        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Project),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved(
                [_roslyn.Document],
                [_roslyn.Document.Project]));

        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Projects),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(CodeActionScopeResolution.Resolved(
                [_roslyn.Document],
                [_roslyn.Document.Project]));

        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => !Enum.IsDefined(scope.Kind)),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(CreateScopeRejection());
    }

    private static StageFixAllRequest CreateRequest(ScopeKind scopeKind)
    {
        return new StageFixAllRequest
        {
            ActionId = "ActionId",
            Scope = new ScopeSelector
            {
                Kind = scopeKind,
            },
        };
    }

    private static StageFixAllRequest CreateDocumentRequest(DocumentSelector selector)
    {
        return new StageFixAllRequest
        {
            ActionId = "ActionId",
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = selector,
            },
        };
    }

    private static StageFixAllRequest CreateProjectRequest(ScopeKind scopeKind, IReadOnlyList<ProjectSelector> selectors)
    {
        return new StageFixAllRequest
        {
            ActionId = "ActionId",
            Scope = new ScopeSelector
            {
                Kind = scopeKind,
                Project = scopeKind == ScopeKind.Project ? selectors[0] : null,
                Projects = scopeKind == ScopeKind.Projects ? selectors : null,
            },
        };
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(Solution solution)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey"),
            Kind = DiscoveredActionKind.CodeFix,
            ProviderId = "ProviderId",
            Title = "Title",
            Descriptor = new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            },
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
        };
    }

    private CodeActionResolution<WorkspaceMutationCandidate> CreateResolution()
    {
        return CodeActionResolution<WorkspaceMutationCandidate>.Resolved(
            _discoveredAction,
            _roslyn.Document,
            new TextSpan(0, 1));
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> CreateRejection()
    {
        var error = new CodeActionExecutionError
        {
            Code = "UnsupportedActionOperation",
            Message = "Message",
        };

        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(error);
    }

    private static CodeActionScopeResolution CreateScopeRejection()
    {
        var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
            "InvalidRequest",
            "Message");

        return CodeActionScopeResolution.Rejected(rejection);
    }

    private static CodeActionApplyResult CreateApplicationFailure(
        CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return CodeActionApplyResult.Failed(
            CodeActionApplyFailureKind.UnsupportedActionOperation,
            rejection.Error!.Message);
    }

}
