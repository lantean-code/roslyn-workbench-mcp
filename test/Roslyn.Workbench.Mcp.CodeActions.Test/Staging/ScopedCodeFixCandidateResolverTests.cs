using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class ScopedCodeFixCandidateResolverTests : IDisposable
{
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly Mock<CodeFixProvider> _provider;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly ImmutableArray<Diagnostic> _diagnostics;
    private readonly DiscoveredCodeAction _action;
    private readonly ScopedCodeFixCandidateResolver _target;

    public ScopedCodeFixCandidateResolverTests()
    {
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _provider = new Mock<CodeFixProvider>();
        _roslyn = RoslynTestFactory.CreateDocument("class C { }");
        _diagnostics = ImmutableArray.Create(CreateDiagnostic());
        _action = CreateAction("Title", "EquivalenceKey", ["DiagnosticId"]);
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
            .ReturnsAsync([_action]);

        _workspaceResolver
            .Setup(item => item.NormalizeDocumentPath(It.IsAny<string>()))
            .Returns((string path) => path);

        _target = new ScopedCodeFixCandidateResolver(
            _discoveryService.Object,
            _diagnosticService.Object);
    }

    [Fact]
    public async Task GIVEN_NoProviderMatches_WHEN_ResolvingCandidate_THEN_ShouldReturnUnavailable()
    {
        _discoveryService.Setup(item => item.GetMatchingCodeFixProviders("ProviderId")).Returns([]);

        var result = await _target.ResolveAsync(
            CreateRequest(),
            [_roslyn.Document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(ScopedCodeFixCandidateResolutionOutcome.Unavailable);
        result.Message.Should().Be("No matching code-fix provider is available.");
        _diagnosticService.Verify(item => item.GetScopedCodeFixDiagnosticsAsync(
            It.IsAny<Document>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentsHaveNoDiagnostics_WHEN_ResolvingCandidate_THEN_ShouldReturnNoDiagnostics()
    {
        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                _roslyn.Document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync([]);

        var result = await _target.ResolveAsync(
            CreateRequest(),
            [_roslyn.Document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(ScopedCodeFixCandidateResolutionOutcome.NoDiagnostics);
        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            It.IsAny<CodeFixProvider>(),
            It.IsAny<Document>(),
            It.IsAny<ImmutableArray<Diagnostic>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentHasNoFilePath_WHEN_ResolvingCandidate_THEN_ShouldNormalizeDocumentName()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution.AddProject("ProjectName", "AssemblyName", LanguageNames.CSharp);
        var solution = project.Solution.AddDocument(
            DocumentId.CreateNewId(project.Id),
            "DocumentName.cs",
            SourceText.From("class C { }"));

        _ = workspace.TryApplyChanges(solution);
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();

        var result = await _target.ResolveAsync(
            CreateRequest(),
            [document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.IsResolved.Should().BeTrue();
        document.FilePath.Should().BeNull();
        _workspaceResolver.Verify(item => item.NormalizeDocumentPath("DocumentName.cs"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_FirstDocumentHasNoDiagnostics_WHEN_ResolvingCandidate_THEN_ShouldContinueWithRemainingDocument()
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
        _workspaceResolver
            .Setup(item => item.NormalizeDocumentPath(firstDocument.FilePath ?? firstDocument.Name))
            .Returns("FirstPath");

        _workspaceResolver
            .Setup(item => item.NormalizeDocumentPath(secondDocument.FilePath ?? secondDocument.Name))
            .Returns("SecondPath");

        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                firstDocument,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                CancellationToken.None))
            .ReturnsAsync([]);

        var result = await _target.ResolveAsync(
            CreateRequest(),
            [secondDocument, firstDocument],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.IsResolved.Should().BeTrue();
        result.Candidate!.Document.Should().BeSameAs(secondDocument);
        _discoveryService.Verify(item => item.DiscoverCodeFixesAsync(
            _provider.Object,
            secondDocument,
            _diagnostics,
            CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_ActionDoesNotSatisfyFilter_WHEN_ResolvingCandidate_THEN_ShouldReturnUnavailable(
        bool mismatchTitle)
    {
        var request = CreateRequest();
        if (mismatchTitle)
        {
            request = request with { Title = "OtherTitle" };
        }
        else
        {
            request = request with { EquivalenceKey = "OtherEquivalenceKey" };
        }

        var result = await _target.ResolveAsync(
            request,
            [_roslyn.Document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(ScopedCodeFixCandidateResolutionOutcome.Unavailable);
        result.Message.Should().Be("No matching code fix was available for the selected scope.");
    }

    [Fact]
    public async Task GIVEN_ActionSatisfiesAllFilters_WHEN_ResolvingCandidate_THEN_ShouldReturnCandidate()
    {
        var request = CreateRequest() with
        {
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
        };

        var result = await _target.ResolveAsync(
            request,
            [_roslyn.Document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.IsResolved.Should().BeTrue();
        result.Candidate!.Title.Should().Be("Title");
        result.Candidate.EquivalenceKey.Should().Be("EquivalenceKey");
    }

    [Fact]
    public async Task GIVEN_MultipleDistinctActionsMatch_WHEN_ResolvingCandidate_THEN_ShouldReturnAmbiguous()
    {
        var secondAction = CreateAction("SecondTitle", "SecondEquivalenceKey", ["DiagnosticId"]);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([_action, secondAction]);

        var result = await _target.ResolveAsync(
            CreateRequest(),
            [_roslyn.Document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(ScopedCodeFixCandidateResolutionOutcome.Ambiguous);
        result.Message.Should().Be("The requested code fix could not be selected uniquely.");
    }

    [Fact]
    public async Task GIVEN_DuplicateActionsHaveReorderedDiagnosticIds_WHEN_ResolvingCandidate_THEN_ShouldReturnOneCandidate()
    {
        var firstAction = CreateAction("Title", "EquivalenceKey", ["FirstDiagnosticId", "SecondDiagnosticId"]);
        var secondAction = CreateAction("Title", "EquivalenceKey", ["SecondDiagnosticId", "FirstDiagnosticId"]);
        _discoveryService
            .Setup(item => item.DiscoverCodeFixesAsync(
                _provider.Object,
                _roslyn.Document,
                _diagnostics,
                CancellationToken.None))
            .ReturnsAsync([firstAction, secondAction]);

        var result = await _target.ResolveAsync(
            CreateRequest(),
            [_roslyn.Document],
            _workspaceResolver.Object,
            CancellationToken.None);

        result.IsResolved.Should().BeTrue();
        result.Candidate!.DiagnosticIds.Should().BeSameAs(firstAction.DiagnosticIds);
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private DiscoveredCodeAction CreateAction(
        string title,
        string equivalenceKey,
        IReadOnlyList<string> diagnosticIds)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create(title, _ => Task.FromResult(_roslyn.Solution), equivalenceKey),
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

    private static ScopedCodeFixRequest CreateRequest()
    {
        return new ScopedCodeFixRequest
        {
            Scope = new ScopeSelector { Kind = ScopeKind.Solution },
            ProviderId = "ProviderId",
            DiagnosticIds = ["DiagnosticId"],
            SyntheticDiagnosticId = "SyntheticDiagnosticId",
        };
    }

    private static Diagnostic CreateDiagnostic()
    {
        return Diagnostic.Create(
            new DiagnosticDescriptor(
                "DiagnosticId",
                "Title",
                "Message",
                "Category",
                Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
                true),
            Location.None);
    }
}
