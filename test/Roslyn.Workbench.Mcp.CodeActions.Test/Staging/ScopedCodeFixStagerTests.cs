using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class ScopedCodeFixStagerTests : IDisposable
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionEvaluator> _evaluator;
    private readonly Mock<IFixAllActionFactory> _fixAllActionFactory;
    private readonly Mock<ICodeActionSolutionChangeCounter> _solutionChangeCounter;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<IScopedCodeFixCandidateResolver> _candidateResolver;
    private readonly Mock<ICodeActionScopeResolver> _scopeResolver;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly Mock<CodeFixProvider> _provider;
    private readonly Mock<FixAllProvider> _fixAllProvider;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly CodeAction _fixAllAction;
    private readonly IReadOnlyList<Diagnostic> _diagnostics;
    private readonly DiscoveredCodeAction _discoveredAction;
    private readonly ScopedCodeFixCandidate _candidate;
    private readonly ScopedCodeFixStager _target;

    public ScopedCodeFixStagerTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _evaluator = new Mock<ICodeActionEvaluator>();
        _fixAllActionFactory = new Mock<IFixAllActionFactory>();
        _solutionChangeCounter = new Mock<ICodeActionSolutionChangeCounter>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _candidateResolver = new Mock<IScopedCodeFixCandidateResolver>();
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

        _diagnostics = [CreateDiagnostic()];
        _discoveredAction = CreateDiscoveredAction(_roslyn.Solution, "Title", "EquivalenceKey", ["DiagnosticId"]);
        _candidate = new ScopedCodeFixCandidate
        {
            Document = _roslyn.Document,
            DocumentSpan = new TextSpan(0, 1),
            Provider = _provider.Object,
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
            DiagnosticIds = ["DiagnosticId"],
        };

        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });

        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());

        _workspaceResolver
            .Setup(item => item.NormalizeDocumentPath(It.IsAny<string>()))
            .Returns("NormalizedPath");

        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _context.SetupGet(item => item.CurrentSolution).Returns(_roslyn.Solution);
        SetupDefaultScopeResolutions();
        _discoveryService
            .Setup(item => item.GetMatchingCodeFixProviders(It.IsAny<string?>()))
            .Returns([_provider.Object]);

        _discoveryService
            .Setup(item => item.GetProviderId(_provider.Object))
            .Returns("ProviderId");

        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                It.IsAny<Document>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_diagnostics);

        _candidateResolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<ScopedCodeFixRequest>(),
                It.IsAny<IReadOnlyList<Document>>(),
                _workspaceResolver.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScopedCodeFixCandidateResolution.Resolved(_candidate));

        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                It.IsAny<Document>(),
                _diagnostics,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_discoveredAction]);

        _provider.Setup(item => item.GetFixAllProvider()).Returns(_fixAllProvider.Object);
        _fixAllActionFactory
            .Setup(item => item.CreateAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Document>(),
                It.IsAny<TextSpan>(),
                It.IsAny<FixAllScope>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixAllActionCreationResult.Created(_fixAllAction));

        _fixAllActionFactory
            .Setup(item => item.CreateAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Project>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
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
                It.IsAny<Solution>(),
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _target = new ScopedCodeFixStager(
            _providerCatalog.Object,
            _discoveryService.Object,
            _evaluator.Object,
            _fixAllActionFactory.Object,
            _diagnosticService.Object,
            _candidateResolver.Object,
            new CodeActionToolRequestResolver(_scopeResolver.Object),
            _solutionChangeCounter.Object);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_StagingScopedFix_THEN_ShouldThrowBeforeCheckingAvailability()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _providerCatalog.VerifyGet(item => item.Status, Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingScopedFix_THEN_ShouldRejectWithoutResolvingScope()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_StagingScopedFix_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(expectedSnapshot))
            .Returns(SnapshotMatchResult.WorkspaceEpochMismatch());

        var result = await _target.StageScopedCodeFixAsync(
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
    public async Task GIVEN_DiagnosticIdsAreEmpty_WHEN_StagingScopedFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageScopedCodeFixAsync(
            new ScopedCodeFixRequest
            {
                ExpectedSnapshot = new SnapshotPrecondition(),
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Solution,
                },
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
        _discoveryService.Verify(item => item.GetMatchingCodeFixProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ScopeKindIsUnsupported_WHEN_StagingScopedFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest((ScopeKind)int.MaxValue),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_CandidateIsUnavailable_WHEN_StagingScopedFix_THEN_ShouldReturnResolverFailure()
    {
        _candidateResolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<ScopedCodeFixRequest>(),
                It.IsAny<IReadOnlyList<Document>>(),
                _workspaceResolver.Object,
                CancellationToken.None))
            .ReturnsAsync(ScopedCodeFixCandidateResolution.Unavailable(
                "Candidate unavailable."));

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
        result.Error.Message.Should().Be("Candidate unavailable.");
    }

    [Fact]
    public async Task GIVEN_ScopeHasNoDiagnostics_WHEN_StagingScopedFix_THEN_ShouldReturnNoChange()
    {
        _candidateResolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<ScopedCodeFixRequest>(),
                It.IsAny<IReadOnlyList<Document>>(),
                _workspaceResolver.Object,
                CancellationToken.None))
            .ReturnsAsync(ScopedCodeFixCandidateResolution.NoDiagnostics());

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.NoChange);
    }

    [Fact]
    public async Task GIVEN_CandidateIsAmbiguous_WHEN_StagingScopedFix_THEN_ShouldReturnResolverFailure()
    {
        _candidateResolver
            .Setup(item => item.ResolveAsync(
                It.IsAny<ScopedCodeFixRequest>(),
                It.IsAny<IReadOnlyList<Document>>(),
                _workspaceResolver.Object,
                CancellationToken.None))
            .ReturnsAsync(ScopedCodeFixCandidateResolution.Ambiguous(
                "Candidate ambiguous."));

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
        result.Error.Message.Should().Be("Candidate ambiguous.");
    }

    [Fact]
    public async Task GIVEN_ProviderHasNoFixAllProvider_WHEN_StagingSolutionFix_THEN_ShouldRejectFixAll()
    {
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsSolutionFix_WHEN_StagingScopedFix_THEN_ShouldReturnEvaluatorRejection()
    {
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_FixAllProviderIsAvailable_WHEN_StagingDocumentFix_THEN_ShouldApplyDocumentFixAll()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _scopeResolver.Verify(item => item.Resolve(
            It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Document),
            _roslyn.Solution,
            _workspaceResolver.Object), Times.Once);

        _fixAllActionFactory.Verify(item => item.CreateAsync(
            _provider.Object,
            _fixAllProvider.Object,
            _roslyn.Document,
            It.IsAny<TextSpan>(),
            FixAllScope.Document,
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsDocumentFixAll_WHEN_StagingScopedFix_THEN_ShouldReturnEvaluatorRejection()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_DirectDocumentFixHasNoDiagnostics_WHEN_StagingScopedFix_THEN_ShouldRejectCodeFix()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                _roslyn.Document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync([]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
    }

    [Theory]
    [InlineData(DirectActionMismatch.Title)]
    [InlineData(DirectActionMismatch.EquivalenceKey)]
    public async Task GIVEN_DirectDocumentActionDoesNotMatch_WHEN_StagingScopedFix_THEN_ShouldRejectCodeFix(DirectActionMismatch mismatch)
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        var directAction = _discoveredAction;
        if (mismatch == DirectActionMismatch.Title)
        {
            directAction = directAction with { Title = "OtherTitle" };
        }
        else
        {
            directAction = directAction with { EquivalenceKey = "OtherEquivalenceKey" };
        }

        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([directAction]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
    }

    [Fact]
    public async Task GIVEN_MultipleDirectDocumentActionsMatch_WHEN_StagingScopedFix_THEN_ShouldRejectAmbiguousAction()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([_discoveredAction, _discoveredAction]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
    }

    [Fact]
    public async Task GIVEN_DirectDocumentProposalIsRejected_WHEN_StagingScopedFix_THEN_ShouldReturnProposalResult()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        var proposal = CreateRejection();
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _discoveredAction.Action,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(proposal));

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error.Should().BeEquivalentTo(proposal.Error);
    }

    [Fact]
    public async Task GIVEN_DirectDocumentProposalSucceeds_WHEN_StagingScopedFix_THEN_ShouldReturnCandidate()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _discoveredAction.Action,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(_roslyn.Solution));

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.CandidateSolution.Should().BeSameAs(_roslyn.Solution);
    }

    [Fact]
    public async Task GIVEN_ProjectProviderHasNoFixAllProvider_WHEN_StagingScopedFix_THEN_ShouldRejectFixAll()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Project, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsProjectFix_WHEN_StagingScopedFix_THEN_ShouldReturnEvaluatorRejection()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Project, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_ProjectFixSucceeds_WHEN_StagingScopedFix_THEN_ShouldReturnCandidate()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Project, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _scopeResolver.Verify(item => item.Resolve(
            It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Project),
            _roslyn.Solution,
            _workspaceResolver.Object), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ProjectsProviderHasNoFixAllProvider_WHEN_StagingScopedFix_THEN_ShouldRejectFixAll()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_ProjectDisappearsAfterEarlierProjectFix_WHEN_StagingProjectsFix_THEN_ShouldRejectProject()
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
                solution.Solution,
                CancellationToken.None))
            .ReturnsAsync(CodeActionApplyResult.Applied(
                solution.Solution.RemoveProject(secondProject.Id)));

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [firstSelector, secondSelector]),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ProjectNotFound");
    }

    [Fact]
    public async Task GIVEN_EvaluatorRejectsProjectInSet_WHEN_StagingProjectsFix_THEN_ShouldReturnEvaluatorRejection()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        var rejection = CreateRejection();
        _evaluator
            .Setup(item => item.EvaluateAsync(
                _fixAllAction,
                _roslyn.Solution,
                CancellationToken.None))
            .ReturnsAsync(CreateApplicationFailure(rejection));

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_MultipleProjectsAreSelected_WHEN_StagingProjectsFix_THEN_ShouldApplyEachProject()
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

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [firstSelector, secondSelector]),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _fixAllActionFactory.Verify(item => item.CreateAsync(
            _provider.Object,
            _fixAllProvider.Object,
            It.IsAny<Project>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            CancellationToken.None), Times.Exactly(2));
    }

    [Theory]
    [InlineData(null, 3, false)]
    [InlineData(3, 3, false)]
    [InlineData(2, 3, true)]
    public async Task GIVEN_ChangeLimit_WHEN_StagingScopedFix_THEN_ShouldEnforceMaximum(
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

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution) with
            {
                MaxChanges = maxChanges,
            },
            _context.Object,
            CancellationToken.None);

        if (expectedRejection)
        {
            result.Error!.Code.Should().Be("FixAllLimitExceeded");
            result.RequiredAction.Should().Be(RequiredAction.NarrowRequest);
        }
        else
        {
            result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
            result.Data!.CandidateSolution.Should().BeSameAs(_roslyn.Solution);
            result.Data.Summary.Should().Be("Title");
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

    private static ScopedCodeFixRequest CreateRequest(ScopeKind scopeKind)
    {
        return new ScopedCodeFixRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Scope = new ScopeSelector
            {
                Kind = scopeKind,
            },
            DiagnosticIds = ["DiagnosticId"],
            ProviderId = "ProviderId",
            AnalyzerTypeName = "AnalyzerTypeName",
            SyntheticDiagnosticId = "SyntheticDiagnosticId",
        };
    }

    private static ScopedCodeFixRequest CreateDocumentRequest(DocumentSelector selector)
    {
        return CreateRequest(ScopeKind.Document) with
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = selector,
            },
        };
    }

    private static ScopedCodeFixRequest CreateProjectRequest(ScopeKind scopeKind, IReadOnlyList<ProjectSelector> selectors)
    {
        return CreateRequest(scopeKind) with
        {
            Scope = new ScopeSelector
            {
                Kind = scopeKind,
                Project = scopeKind == ScopeKind.Project ? selectors[0] : null,
                Projects = scopeKind == ScopeKind.Projects ? selectors : null,
            },
        };
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(
        Solution solution,
        string title,
        string equivalenceKey,
        IReadOnlyList<string> diagnosticIds)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create(title, _ => Task.FromResult(solution), equivalenceKey),
            Kind = DiscoveredActionKind.CodeFix,
            ProviderId = "ProviderId",
            Title = title,
            Descriptor = new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            },
            EquivalenceKey = equivalenceKey,
            DiagnosticIds = diagnosticIds,
        };
    }

    private static Diagnostic CreateDiagnostic()
    {
        return Diagnostic.Create(
            new DiagnosticDescriptor("DiagnosticId", "Title", "Message", "Category", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, true),
            Location.None);
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> CreateRejection()
    {
        var error = new CodeActionExecutionError
        {
            Code = "UnsupportedActionOperation",
            Message = "Message",
        };

        return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error);
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

#pragma warning disable CA1515 // The enum is part of a public xUnit theory method signature.
    public enum DirectActionMismatch
    {
        Title,
        EquivalenceKey,
    }

#pragma warning restore CA1515
}
