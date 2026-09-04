using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Workbench.Mcp.Plugins.Resolution;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Resolution;

public sealed class ToolRequestResolverTests : IDisposable
{
    private readonly Mock<IToolExecutionContext> _context;
    private readonly ToolRequestResolver _target;
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;

    public ToolRequestResolverTests()
    {
        _workspace = new AdhocWorkspace();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _context = new Mock<IToolExecutionContext>();
        _context.SetupGet(item => item.CurrentSolution).Returns(() => _workspace.CurrentSolution);
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new ToolRequestResolver();
    }

    [Fact]
    public void GIVEN_MissingDocumentSelector_WHEN_ResolvingDocument_THEN_ShouldRejectRequest()
    {
        var result = _target.ResolveDocument<object>(selector: null, _context.Object);

        AssertFailure(result.Rejection, "InvalidRequest", requiredAction: null);
    }

    [Fact]
    public void GIVEN_ResolvedDocumentSelector_WHEN_ResolvingDocument_THEN_ShouldReturnDocument()
    {
        var document = AddDocument("Project", "Document.cs");
        var selector = new DocumentSelector { Path = "Document.cs" };
        var resolution = SelectorResolveResult.Resolved(document);
        _workspaceResolver.Setup(item => item.ResolveDocument(selector)).Returns(resolution);

        var result = _target.ResolveDocument<object>(selector, _context.Object);

        result.HasRejection.Should().BeFalse();
        result.Value.Should().BeSameAs(document);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "DocumentNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "DocumentAmbiguous")]
    [InlineData(SelectorResolveStatus.Invalid, "DocumentSelectorInvalid")]
    public void GIVEN_UnresolvedDocumentSelector_WHEN_ResolvingDocument_THEN_ShouldNormalizeRejection(SelectorResolveStatus status, string expectedCode)
    {
        var selector = new DocumentSelector { Path = "Document.cs" };
        var resolution = CreateUnresolvedResult<Document>(status);
        _workspaceResolver.Setup(item => item.ResolveDocument(selector)).Returns(resolution);

        var result = _target.ResolveDocument<object>(selector, _context.Object);

        AssertFailure(result.Rejection, expectedCode, RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MissingProjectSelector_WHEN_ResolvingProject_THEN_ShouldRejectRequest()
    {
        var result = _target.ResolveProject<object>(selector: null, _context.Object);

        AssertFailure(result.Rejection, "InvalidRequest", requiredAction: null);
    }

    [Fact]
    public void GIVEN_ResolvedProjectSelector_WHEN_ResolvingProject_THEN_ShouldReturnProject()
    {
        var project = AddProject("Project");
        var selector = new ProjectSelector { Name = "Project" };
        var resolution = SelectorResolveResult.Resolved(project);
        _workspaceResolver.Setup(item => item.ResolveProject(selector)).Returns(resolution);

        var result = _target.ResolveProject<object>(selector, _context.Object);

        result.HasRejection.Should().BeFalse();
        result.Value.Should().BeSameAs(project);
    }

    [Fact]
    public void GIVEN_UnresolvedProjectSelector_WHEN_ResolvingProject_THEN_ShouldNormalizeRejection()
    {
        var selector = new ProjectSelector { Name = "Project" };
        var resolution = SelectorResolveResult.NotFound<Project>();
        _workspaceResolver.Setup(item => item.ResolveProject(selector)).Returns(resolution);

        var result = _target.ResolveProject<object>(selector, _context.Object);

        AssertFailure(result.Rejection, "ProjectNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MissingScope_WHEN_ResolvingDocuments_THEN_ShouldReturnAddressableSolutionDocuments()
    {
        var (_, document) = AddProjectWithIncludedAndExcludedDocuments();
        _workspaceResolver.Setup(item => item.GetDocuments(_workspace.CurrentSolution)).Returns([document]);

        var result = _target.ResolveDocuments<object>(scope: null, _context.Object);

        result.Value.Should().Equal(document);
    }

    [Fact]
    public void GIVEN_SolutionScope_WHEN_ResolvingDocuments_THEN_ShouldReturnAddressableSolutionDocuments()
    {
        var (_, document) = AddProjectWithIncludedAndExcludedDocuments();
        var scope = new ScopeSelector { Kind = ScopeKind.Solution };
        _workspaceResolver.Setup(item => item.GetDocuments(_workspace.CurrentSolution)).Returns([document]);

        var result = _target.ResolveDocuments<object>(scope, _context.Object);

        result.Value.Should().Equal(document);
    }

    [Fact]
    public void GIVEN_DocumentScope_WHEN_ResolvingDocuments_THEN_ShouldReturnResolvedDocument()
    {
        var document = AddDocument("Project", "Document.cs");
        var selector = new DocumentSelector { Path = "Document.cs" };
        var scope = new ScopeSelector { Kind = ScopeKind.Document, Document = selector };
        var resolution = SelectorResolveResult.Resolved(document);
        _workspaceResolver.Setup(item => item.ResolveDocument(selector)).Returns(resolution);

        var result = _target.ResolveDocuments<object>(scope, _context.Object);

        result.Value.Should().Equal(document);
    }

    [Fact]
    public void GIVEN_UnresolvedDocumentScope_WHEN_ResolvingDocuments_THEN_ShouldReturnRejection()
    {
        var selector = new DocumentSelector { Path = "Document.cs" };
        var scope = new ScopeSelector { Kind = ScopeKind.Document, Document = selector };
        var resolution = SelectorResolveResult.NotFound<Document>();
        _workspaceResolver.Setup(item => item.ResolveDocument(selector)).Returns(resolution);

        var result = _target.ResolveDocuments<object>(scope, _context.Object);

        AssertFailure(result.Rejection, "DocumentNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Theory]
    [InlineData(ScopeKind.Project)]
    [InlineData(ScopeKind.Projects)]
    public void GIVEN_ProjectScope_WHEN_ResolvingDocuments_THEN_ShouldReturnAddressableProjectDocuments(ScopeKind scopeKind)
    {
        var (project, document) = AddProjectWithIncludedAndExcludedDocuments();
        var selector = new ProjectSelector { Name = "Project" };
        var scope = CreateProjectScope(scopeKind, selector);
        var resolution = SelectorResolveResult.Resolved(project);
        _workspaceResolver.Setup(item => item.ResolveProject(selector)).Returns(resolution);
        _workspaceResolver.Setup(item => item.GetDocuments(project)).Returns([document]);

        var result = _target.ResolveDocuments<object>(scope, _context.Object);

        result.Value.Should().Equal(document);
    }

    [Fact]
    public void GIVEN_MultipleProjectScope_WHEN_ResolvingDocuments_THEN_ShouldAggregateAddressableDocuments()
    {
        var firstDocument = AddDocument("FirstProject", "FirstDocument.cs");
        var secondDocument = AddDocument("SecondProject", "SecondDocument.cs");
        var firstProject = firstDocument.Project;
        var secondProject = secondDocument.Project;
        var firstSelector = new ProjectSelector { Name = "FirstProject" };
        var secondSelector = new ProjectSelector { Name = "SecondProject" };
        var scope = new ScopeSelector
        {
            Kind = ScopeKind.Projects,
            Projects = [firstSelector, secondSelector],
        };
        var firstResolution = SelectorResolveResult.Resolved(firstProject);
        var secondResolution = SelectorResolveResult.Resolved(secondProject);
        _workspaceResolver.Setup(item => item.ResolveProject(firstSelector)).Returns(firstResolution);
        _workspaceResolver.Setup(item => item.ResolveProject(secondSelector)).Returns(secondResolution);
        _workspaceResolver.Setup(item => item.GetDocuments(firstProject)).Returns([firstDocument]);
        _workspaceResolver.Setup(item => item.GetDocuments(secondProject)).Returns([secondDocument]);

        var result = _target.ResolveDocuments<object>(scope, _context.Object);

        result.Value.Should().Equal(firstDocument, secondDocument);
    }

    [Theory]
    [InlineData(ScopeKind.Project)]
    [InlineData(ScopeKind.Projects)]
    public void GIVEN_UnresolvedProjectScope_WHEN_ResolvingDocuments_THEN_ShouldReturnRejection(ScopeKind scopeKind)
    {
        var selector = new ProjectSelector { Name = "Project" };
        var scope = CreateProjectScope(scopeKind, selector);
        var resolution = SelectorResolveResult.NotFound<Project>();
        _workspaceResolver.Setup(item => item.ResolveProject(selector)).Returns(resolution);

        var result = _target.ResolveDocuments<object>(scope, _context.Object);

        AssertFailure(result.Rejection, "ProjectNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MissingScope_WHEN_ResolvingProjects_THEN_ShouldReturnProjectsInNameOrder()
    {
        AddProject("SecondProject");
        AddProject("FirstProject");

        var result = _target.ResolveProjects<object>(scope: null, _context.Object);

        result.Value.Should().NotBeNull();
        result.Value!.Select(static project => project.Name).Should().Equal("FirstProject", "SecondProject");
    }

    [Fact]
    public void GIVEN_SolutionScope_WHEN_ResolvingProjects_THEN_ShouldReturnProjectsInNameOrder()
    {
        AddProject("SecondProject");
        AddProject("FirstProject");
        var scope = new ScopeSelector { Kind = ScopeKind.Solution };

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        result.Value.Should().NotBeNull();
        result.Value!.Select(static project => project.Name).Should().Equal("FirstProject", "SecondProject");
    }

    [Fact]
    public void GIVEN_DocumentScope_WHEN_ResolvingProjects_THEN_ShouldReturnContainingProject()
    {
        var document = AddDocument("Project", "Document.cs");
        var selector = new DocumentSelector { Path = "Document.cs" };
        var scope = new ScopeSelector { Kind = ScopeKind.Document, Document = selector };
        var resolution = SelectorResolveResult.Resolved(document);
        _workspaceResolver.Setup(item => item.ResolveDocument(selector)).Returns(resolution);

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        result.Value.Should().Equal(document.Project);
    }

    [Fact]
    public void GIVEN_UnresolvedDocumentScope_WHEN_ResolvingProjects_THEN_ShouldReturnRejection()
    {
        var selector = new DocumentSelector { Path = "Document.cs" };
        var scope = new ScopeSelector { Kind = ScopeKind.Document, Document = selector };
        var resolution = SelectorResolveResult.NotFound<Document>();
        _workspaceResolver.Setup(item => item.ResolveDocument(selector)).Returns(resolution);

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        AssertFailure(result.Rejection, "DocumentNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_ProjectScope_WHEN_ResolvingProjects_THEN_ShouldReturnResolvedProject()
    {
        var project = AddProject("Project");
        var selector = new ProjectSelector { Name = "Project" };
        var scope = new ScopeSelector { Kind = ScopeKind.Project, Project = selector };
        var resolution = SelectorResolveResult.Resolved(project);
        _workspaceResolver.Setup(item => item.ResolveProject(selector)).Returns(resolution);

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        result.Value.Should().Equal(project);
    }

    [Fact]
    public void GIVEN_UnresolvedProjectScope_WHEN_ResolvingProjects_THEN_ShouldReturnRejection()
    {
        var selector = new ProjectSelector { Name = "Project" };
        var scope = new ScopeSelector { Kind = ScopeKind.Project, Project = selector };
        var resolution = SelectorResolveResult.NotFound<Project>();
        _workspaceResolver.Setup(item => item.ResolveProject(selector)).Returns(resolution);

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        AssertFailure(result.Rejection, "ProjectNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MultipleProjectScope_WHEN_ResolvingProjects_THEN_ShouldRemoveDuplicatesAndOrderByName()
    {
        var secondProject = AddProject("SecondProject");
        var firstProject = AddProject("FirstProject");
        var documentText = SourceText.From("class Document { }");
        _workspace.AddDocument(firstProject.Id, "Document.cs", documentText);
        var duplicateProjectSnapshot = _workspace.CurrentSolution.GetProject(firstProject.Id)
            ?? throw new InvalidOperationException("The project was not found in the current solution.");
        var secondSelector = new ProjectSelector { Name = "SecondProject" };
        var firstSelector = new ProjectSelector { Name = "FirstProject" };
        var duplicateSelector = new ProjectSelector { ProjectId = "ProjectId" };
        var scope = new ScopeSelector
        {
            Kind = ScopeKind.Projects,
            Projects = [secondSelector, firstSelector, duplicateSelector],
        };
        var secondResolution = SelectorResolveResult.Resolved(secondProject);
        var firstResolution = SelectorResolveResult.Resolved(firstProject);
        var duplicateResolution = SelectorResolveResult.Resolved(duplicateProjectSnapshot);
        _workspaceResolver.Setup(item => item.ResolveProject(secondSelector)).Returns(secondResolution);
        _workspaceResolver.Setup(item => item.ResolveProject(firstSelector)).Returns(firstResolution);
        _workspaceResolver.Setup(item => item.ResolveProject(duplicateSelector)).Returns(duplicateResolution);

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        firstProject.Should().NotBeSameAs(duplicateProjectSnapshot);
        result.Value.Should().NotBeNull();
        result.Value!.Select(static project => project.Name).Should().Equal("FirstProject", "SecondProject");
    }

    [Fact]
    public void GIVEN_MultipleProjectScopeContainsUnresolvedSelector_WHEN_ResolvingProjects_THEN_ShouldReturnRejection()
    {
        var project = AddProject("Project");
        var resolvedSelector = new ProjectSelector { Name = "Project" };
        var unresolvedSelector = new ProjectSelector { Name = "MissingProject" };
        var scope = new ScopeSelector
        {
            Kind = ScopeKind.Projects,
            Projects = [resolvedSelector, unresolvedSelector],
        };
        var resolvedResolution = SelectorResolveResult.Resolved(project);
        var unresolvedResolution = SelectorResolveResult.NotFound<Project>();
        _workspaceResolver.Setup(item => item.ResolveProject(resolvedSelector)).Returns(resolvedResolution);
        _workspaceResolver.Setup(item => item.ResolveProject(unresolvedSelector)).Returns(unresolvedResolution);

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        AssertFailure(result.Rejection, "ProjectNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MultipleProjectScopeWithoutSelectors_WHEN_ResolvingProjects_THEN_ShouldReturnEmptyCollection()
    {
        var scope = new ScopeSelector { Kind = ScopeKind.Projects, Projects = null };

        var result = _target.ResolveProjects<object>(scope, _context.Object);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_MissingSymbolSelector_WHEN_ResolvingSymbol_THEN_ShouldRejectWithoutSnapshotValidation()
    {
        var result = await _target.ResolveSymbolAsync<object>(selector: null, expectedSnapshot: null, _context.Object, CancellationToken.None);

        AssertFailure(result.Rejection, "InvalidRequest", requiredAction: null);
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationSymbolHasMismatchedSnapshot_WHEN_ResolvingSymbol_THEN_ShouldReturnConflictWithoutResolution()
    {
        var selector = CreateLocationSymbolSelector();
        var expectedSnapshot = CreateSnapshot();
        var snapshotResult = SnapshotMatchResult.SnapshotIdMismatch();
        _workspaceResolver.Setup(item => item.ValidateSnapshot(expectedSnapshot)).Returns(snapshotResult);

        var result = await _target.ResolveSymbolAsync<object>(selector, expectedSnapshot, _context.Object, CancellationToken.None);

        AssertFailure(
            result.Rejection,
            "SnapshotMismatch",
            RequiredAction.ResolveTargetAgain,
            PluginExecutionOutcome.Conflict);
        _workspaceResolver.Verify(item => item.ResolveSymbolAsync(It.IsAny<SymbolSelector>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationSymbolHasMatchingSnapshot_WHEN_ResolvingSymbol_THEN_ShouldReturnResolvedSymbol()
    {
        var symbol = new Mock<ISymbol>();
        var selector = CreateLocationSymbolSelector();
        var expectedSnapshot = CreateSnapshot();
        var cancellationToken = new CancellationToken(canceled: false);
        var snapshotResult = SnapshotMatchResult.Matched();
        var resolution = SelectorResolveResult.Resolved(symbol.Object);
        _workspaceResolver.Setup(item => item.ValidateSnapshot(expectedSnapshot)).Returns(snapshotResult);
        _workspaceResolver.Setup(item => item.ResolveSymbolAsync(selector, cancellationToken)).ReturnsAsync(resolution);

        var result = await _target.ResolveSymbolAsync<object>(selector, expectedSnapshot, _context.Object, cancellationToken);

        result.HasRejection.Should().BeFalse();
        result.Value.Should().BeSameAs(symbol.Object);
    }

    [Fact]
    public async Task GIVEN_DocumentationIdSymbol_WHEN_ResolvingSymbol_THEN_ShouldResolveWithoutSnapshotValidation()
    {
        var symbol = new Mock<ISymbol>();
        var selector = new SymbolSelector { DocumentationCommentId = "DocumentationCommentId" };
        var resolution = SelectorResolveResult.Resolved(symbol.Object);
        _workspaceResolver.Setup(item => item.ResolveSymbolAsync(selector, CancellationToken.None)).ReturnsAsync(resolution);

        var result = await _target.ResolveSymbolAsync<object>(selector, expectedSnapshot: null, _context.Object, CancellationToken.None);

        result.HasRejection.Should().BeFalse();
        result.Value.Should().BeSameAs(symbol.Object);
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_UnresolvedSymbol_WHEN_ResolvingSymbol_THEN_ShouldNormalizeRejection()
    {
        var selector = new SymbolSelector { DocumentationCommentId = "DocumentationCommentId" };
        var resolution = SelectorResolveResult.NotFound<ISymbol>();
        _workspaceResolver.Setup(item => item.ResolveSymbolAsync(selector, CancellationToken.None)).ReturnsAsync(resolution);

        var result = await _target.ResolveSymbolAsync<object>(selector, expectedSnapshot: null, _context.Object, CancellationToken.None);

        AssertFailure(result.Rejection, "SymbolNotFound", RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MatchingSnapshot_WHEN_ValidatingSnapshot_THEN_ShouldNotReturnRejection()
    {
        var expectedSnapshot = CreateSnapshot();
        var snapshotResult = SnapshotMatchResult.Matched();
        _workspaceResolver.Setup(item => item.ValidateSnapshot(expectedSnapshot)).Returns(snapshotResult);

        var result = _target.ValidateSnapshot<object>(_context.Object, expectedSnapshot);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_MismatchedSnapshot_WHEN_ValidatingSnapshot_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = CreateSnapshot();
        var snapshotResult = SnapshotMatchResult.TransactionRevisionMismatch();
        _workspaceResolver.Setup(item => item.ValidateSnapshot(expectedSnapshot)).Returns(snapshotResult);

        var result = _target.ValidateSnapshot<object>(_context.Object, expectedSnapshot);

        AssertFailure(
            result,
            "SnapshotMismatch",
            RequiredAction.ResolveTargetAgain,
            PluginExecutionOutcome.Conflict);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private Project AddProject(string name)
    {
        var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, name, name, LanguageNames.CSharp);
        _workspace.AddProject(projectInfo);

        return _workspace.CurrentSolution.GetProject(projectInfo.Id)
            ?? throw new InvalidOperationException("The project was not found in the current solution.");
    }

    private Document AddDocument(string projectName, string documentName)
    {
        var project = AddProject(projectName);
        var documentText = SourceText.From("class Document { }");
        var document = _workspace.AddDocument(project.Id, documentName, documentText);

        return _workspace.CurrentSolution.GetDocument(document.Id)
            ?? throw new InvalidOperationException("The document was not found in the current solution.");
    }

    private (Project Project, Document IncludedDocument) AddProjectWithIncludedAndExcludedDocuments()
    {
        var project = AddProject("Project");
        var includedText = SourceText.From("class Included { }");
        var excludedText = SourceText.From("class Excluded { }");
        var includedDocument = _workspace.AddDocument(project.Id, "Included.cs", includedText);
        _workspace.AddDocument(project.Id, "Excluded.cs", excludedText);
        var currentProject = _workspace.CurrentSolution.GetProject(project.Id)
            ?? throw new InvalidOperationException("The project was not found in the current solution.");
        var currentIncludedDocument = _workspace.CurrentSolution.GetDocument(includedDocument.Id)
            ?? throw new InvalidOperationException("The included document was not found in the current solution.");

        return (currentProject, currentIncludedDocument);
    }

    private static void AssertFailure(
        PluginExecutionResult<object>? rejection,
        string expectedCode,
        RequiredAction? requiredAction,
        PluginExecutionOutcome expectedOutcome = PluginExecutionOutcome.Rejected)
    {
        rejection.Should().NotBeNull();
        rejection.Outcome.Should().Be(expectedOutcome);
        rejection.Error.Should().NotBeNull();
        rejection.Error.Code.Should().Be(expectedCode);
        rejection.RequiredAction.Should().Be(requiredAction);
    }

    private static ScopeSelector CreateProjectScope(ScopeKind scopeKind, ProjectSelector projectSelector)
    {
        return scopeKind switch
        {
            ScopeKind.Project => new ScopeSelector { Kind = scopeKind, Project = projectSelector },
            ScopeKind.Projects => new ScopeSelector { Kind = scopeKind, Projects = [projectSelector] },
            _ => throw new InvalidOperationException("Unsupported scope kind."),
        };
    }

    private static SnapshotPrecondition CreateSnapshot()
    {
        return new SnapshotPrecondition
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 1,
            SnapshotId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TransactionRevision = null,
        };
    }

    private static SymbolSelector CreateLocationSymbolSelector()
    {
        var document = new DocumentSelector { Path = "Document.cs" };
        var span = new TextSpanSelector
        {
            Document = document,
            Range = new TextSpanRange { Start = 0, Length = 1 },
        };
        var location = new LocationSelector { Span = span };

        return new SymbolSelector { Location = location };
    }

    private static SelectorResolveResult<T> CreateUnresolvedResult<T>(SelectorResolveStatus status)
        where T : class
    {
        return status switch
        {
            SelectorResolveStatus.NotFound => SelectorResolveResult.NotFound<T>(),
            SelectorResolveStatus.Ambiguous => SelectorResolveResult.Ambiguous<T>(),
            SelectorResolveStatus.Invalid => SelectorResolveResult.Invalid<T>(),
            _ => throw new InvalidOperationException("Unsupported resolution status."),
        };
    }
}
