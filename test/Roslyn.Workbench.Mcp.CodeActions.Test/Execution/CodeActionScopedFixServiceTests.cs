using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

public sealed class CodeActionScopedFixServiceTests : IDisposable
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionOperationService> _operationService;
    private readonly Mock<ICodeActionSolutionChangeCounter> _solutionChangeCounter;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionScopeResolver> _scopeResolver;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly Mock<CodeFixProvider> _provider;
    private readonly Mock<FixAllProvider> _fixAllProvider;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly ImmutableArray<Diagnostic> _diagnostics;
    private readonly DiscoveredCodeAction _discoveredAction;
    private readonly CodeActionScopedFixService _target;

    public CodeActionScopedFixServiceTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _operationService = new Mock<ICodeActionOperationService>();
        _solutionChangeCounter = new Mock<ICodeActionSolutionChangeCounter>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _scopeResolver = new Mock<ICodeActionScopeResolver>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _provider = new Mock<CodeFixProvider>();
        _fixAllProvider = new Mock<FixAllProvider>();
        _roslyn = RoslynTestFactory.CreateDocument("class C { }");
        _diagnostics = ImmutableArray.Create(CreateDiagnostic());
        _discoveredAction = CreateDiscoveredAction(_roslyn.Solution, "Title", "EquivalenceKey", ["DiagnosticId"]);
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
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                It.IsAny<Document>(),
                _diagnostics,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_discoveredAction]);
        _provider.Setup(item => item.GetFixAllProvider()).Returns(_fixAllProvider.Object);
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Document>(),
                It.IsAny<TextSpan>(),
                It.IsAny<FixAllScope>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeActionApplyResult
            {
                CandidateSolution = _roslyn.Solution,
            });
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Project>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeActionApplyResult
            {
                CandidateSolution = _roslyn.Solution,
            });
        _solutionChangeCounter
            .Setup(item => item.CountChangedSourceDocumentsAsync(
                It.IsAny<Solution>(),
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _target = new CodeActionScopedFixService(
            _providerCatalog.Object,
            _discoveryService.Object,
            _operationService.Object,
            _diagnosticService.Object,
            _scopeResolver.Object,
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
    public async Task GIVEN_ScopeIsMissing_WHEN_StagingScopedFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageScopedCodeFixAsync(
            new ScopedCodeFixRequest
            {
                DiagnosticIds = ["DiagnosticId"],
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_DiagnosticIdsAreEmpty_WHEN_StagingScopedFix_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageScopedCodeFixAsync(
            new ScopedCodeFixRequest
            {
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
    public async Task GIVEN_NoProviderMatches_WHEN_StagingScopedFix_THEN_ShouldRejectCodeFix()
    {
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders("ProviderId")).Returns([]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
        _diagnosticService.Verify(item => item.GetScopedCodeFixDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SelectedScopeHasNoDiagnostics_WHEN_StagingScopedFix_THEN_ShouldReturnNoChange()
    {
        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                _roslyn.Document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync([]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.NoChange);
        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            It.IsAny<CodeFixProvider>(),
            It.IsAny<Document>(),
            It.IsAny<ImmutableArray<Diagnostic>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentHasNoFilePath_WHEN_StagingSolutionFix_THEN_ShouldNormalizeDocumentName()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution.AddProject("ProjectName", "AssemblyName", LanguageNames.CSharp);
        var solution = project.Solution.AddDocument(DocumentId.CreateNewId(project.Id), "DocumentName.cs", SourceText.From("class C { }"));
        workspace.TryApplyChanges(solution);
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        _context.SetupGet(item => item.CurrentSolution).Returns(workspace.CurrentSolution);
        _workspaceResolver.Setup(item => item.NormalizeDocumentPath("DocumentName.cs")).Returns("NormalizedPath");

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        document.FilePath.Should().BeNull();
        _workspaceResolver.Verify(item => item.NormalizeDocumentPath("DocumentName.cs"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_FirstDocumentHasNoDiagnostics_WHEN_StagingSolutionFix_THEN_ShouldContinueWithRemainingDocument()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "First.cs", Source = "class First { }" },
                    new InMemoryRoslynDocumentDefinition { Name = "Second.cs", Source = "class Second { }" },
                ],
            },
        ]);
        var firstDocument = solution.GetDocument("First.cs");
        var secondDocument = solution.GetDocument("Second.cs");
        _context.SetupGet(item => item.CurrentSolution).Returns(solution.Solution);
        _workspaceResolver.Setup(item => item.NormalizeDocumentPath(firstDocument.FilePath ?? firstDocument.Name)).Returns("FirstPath");
        _workspaceResolver.Setup(item => item.NormalizeDocumentPath(secondDocument.FilePath ?? secondDocument.Name)).Returns("SecondPath");
        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                firstDocument,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync([]);
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                secondDocument,
                It.IsAny<TextSpan>(),
                FixAllScope.Solution,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                CandidateSolution = solution.Solution,
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            _provider.Object,
            secondDocument,
            _diagnostics,
            CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(CandidateFilter.Title)]
    [InlineData(CandidateFilter.EquivalenceKey)]
    public async Task GIVEN_ActionDoesNotSatisfyFilter_WHEN_StagingScopedFix_THEN_ShouldRejectCodeFix(CandidateFilter filter)
    {
        var request = filter == CandidateFilter.Title
            ? CreateRequest(ScopeKind.Solution) with { Title = "OtherTitle" }
            : CreateRequest(ScopeKind.Solution) with { EquivalenceKey = "OtherEquivalenceKey" };

        var result = await _target.StageScopedCodeFixAsync(
            request,
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeFixUnavailable");
        _discoveryService.Verify(item => item.GetProviderId(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionSatisfiesAllFilters_WHEN_StagingScopedFix_THEN_ShouldApplyCandidate()
    {
        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution) with
            {
                Title = "Title",
                EquivalenceKey = "EquivalenceKey",
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
    }

    [Fact]
    public async Task GIVEN_MultipleDistinctCandidatesMatch_WHEN_StagingScopedFix_THEN_ShouldRejectAmbiguousAction()
    {
        var secondAction = CreateDiscoveredAction(_roslyn.Solution, "SecondTitle", "SecondEquivalenceKey", ["DiagnosticId"]);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([_discoveredAction, secondAction]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
    }

    [Fact]
    public async Task GIVEN_DuplicateCandidatesHaveReorderedDiagnosticIds_WHEN_StagingScopedFix_THEN_ShouldApplyOneCandidate()
    {
        var firstAction = CreateDiscoveredAction(_roslyn.Solution, "Title", "EquivalenceKey", ["FirstDiagnosticId", "SecondDiagnosticId"]);
        var secondAction = CreateDiscoveredAction(_roslyn.Solution, "Title", "EquivalenceKey", ["SecondDiagnosticId", "FirstDiagnosticId"]);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([firstAction, secondAction]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _operationService.Verify(item => item.ApplyFixAllAsync(
            _provider.Object,
            _fixAllProvider.Object,
            _roslyn.Document,
            It.IsAny<TextSpan>(),
            FixAllScope.Solution,
            firstAction.DiagnosticIds,
            "EquivalenceKey",
            "SyntheticDiagnosticId",
            CancellationToken.None), Times.Once);
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
    public async Task GIVEN_OperationRejectsSolutionFix_WHEN_StagingScopedFix_THEN_ShouldReturnOperationRejection()
    {
        var rejection = CreateRejection();
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                _roslyn.Document,
                It.IsAny<TextSpan>(),
                FixAllScope.Solution,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                Rejection = rejection,
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateRequest(ScopeKind.Solution),
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
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
        _operationService.Verify(item => item.ApplyFixAllAsync(
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
    public async Task GIVEN_OperationRejectsDocumentFixAll_WHEN_StagingScopedFix_THEN_ShouldReturnOperationRejection()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        var rejection = CreateRejection();
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                _roslyn.Document,
                It.IsAny<TextSpan>(),
                FixAllScope.Document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                Rejection = rejection,
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
    }

    [Fact]
    public async Task GIVEN_DirectDocumentFixHasNoDiagnostics_WHEN_StagingScopedFix_THEN_ShouldRejectCodeFix()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _diagnosticService
            .SetupSequence(item => item.GetScopedCodeFixDiagnosticsAsync(
                _roslyn.Document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(_diagnostics)
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
        var directAction = mismatch == DirectActionMismatch.Title
            ? _discoveredAction with { Title = "OtherTitle" }
            : _discoveredAction with { EquivalenceKey = "OtherEquivalenceKey" };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _discoveryService
            .SetupSequence(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([_discoveredAction])
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
            .SetupSequence(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([_discoveredAction])
            .ReturnsAsync([_discoveredAction, _discoveredAction]);

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
    }

    [Theory]
    [InlineData(DirectProposalFailure.Rejected)]
    [InlineData(DirectProposalFailure.MissingCandidate)]
    public async Task GIVEN_DirectDocumentProposalFails_WHEN_StagingScopedFix_THEN_ShouldReturnProposalResult(DirectProposalFailure failure)
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        var proposal = failure == DirectProposalFailure.Rejected
            ? CreateRejection()
            : new CodeActionExecutionResult<WorkspaceMutationCandidate>
            {
                Outcome = CodeActionExecutionOutcome.Succeeded,
            };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(
                _discoveredAction.Action,
                "Title",
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(proposal);

        var result = await _target.StageScopedCodeFixAsync(
            CreateDocumentRequest(selector),
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(proposal);
    }

    [Fact]
    public async Task GIVEN_DirectDocumentProposalSucceeds_WHEN_StagingScopedFix_THEN_ShouldReturnCandidate()
    {
        var selector = new DocumentSelector { Path = "DocumentPath" };
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = _roslyn.Solution,
        };
        _provider.Setup(item => item.GetFixAllProvider()).Returns((FixAllProvider?)null);
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(
                _discoveredAction.Action,
                "Title",
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate));

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
    public async Task GIVEN_OperationRejectsProjectFix_WHEN_StagingScopedFix_THEN_ShouldReturnOperationRejection()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        var rejection = CreateRejection();
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                _roslyn.Document.Project,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                Rejection = rejection,
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Project, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
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
            .Returns(new CodeActionScopeResolution
            {
                Documents = firstProject.Documents.Concat(secondProject.Documents).ToArray(),
                Projects = [firstProject, secondProject],
            });
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                firstProject,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                CandidateSolution = solution.Solution.RemoveProject(secondProject.Id),
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [firstSelector, secondSelector]),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ProjectNotFound");
    }

    [Fact]
    public async Task GIVEN_OperationRejectsProjectInSet_WHEN_StagingProjectsFix_THEN_ShouldReturnOperationRejection()
    {
        var selector = new ProjectSelector { Name = "ProjectName" };
        var rejection = CreateRejection();
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                _roslyn.Document.Project,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                Rejection = rejection,
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [selector]),
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
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
            .Returns(new CodeActionScopeResolution
            {
                Documents = firstProject.Documents.Concat(secondProject.Documents).ToArray(),
                Projects = [firstProject, secondProject],
            });
        _operationService
            .Setup(item => item.ApplyFixAllAsync(
                _provider.Object,
                _fixAllProvider.Object,
                It.IsAny<Project>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync(new CodeActionApplyResult
            {
                CandidateSolution = solution.Solution,
            });

        var result = await _target.StageScopedCodeFixAsync(
            CreateProjectRequest(ScopeKind.Projects, [firstSelector, secondSelector]),
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        _operationService.Verify(item => item.ApplyFixAllAsync(
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
            .Returns((ScopeSelector _, Solution solution, IWorkspaceResolver _) => new CodeActionScopeResolution
            {
                Documents = solution.Projects.SelectMany(static project => project.Documents).ToArray(),
            });
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Document),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(new CodeActionScopeResolution
            {
                Documents = [_roslyn.Document],
            });
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Project),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(new CodeActionScopeResolution
            {
                Documents = [_roslyn.Document],
                Projects = [_roslyn.Document.Project],
            });
        _scopeResolver
            .Setup(item => item.Resolve(
                It.Is<ScopeSelector>(scope => scope.Kind == ScopeKind.Projects),
                It.IsAny<Solution>(),
                _workspaceResolver.Object))
            .Returns(new CodeActionScopeResolution
            {
                Documents = [_roslyn.Document],
                Projects = [_roslyn.Document.Project],
            });
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
        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
        {
            Code = "ErrorCode",
            Message = "Message",
        });
    }

    private static CodeActionScopeResolution CreateScopeRejection()
    {
        return new CodeActionScopeResolution
        {
            Rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "Message"),
        };
    }

    #pragma warning disable CA1515 // These enums are part of public xUnit theory method signatures.
    public enum CandidateFilter
    {
        Title,
        EquivalenceKey,
    }

    public enum DirectActionMismatch
    {
        Title,
        EquivalenceKey,
    }

    public enum DirectProposalFailure
    {
        Rejected,
        MissingCandidate,
    }
    #pragma warning restore CA1515
}
