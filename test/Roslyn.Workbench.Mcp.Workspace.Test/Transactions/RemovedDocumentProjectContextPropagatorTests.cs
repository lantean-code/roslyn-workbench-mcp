namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class RemovedDocumentProjectContextPropagatorTests : IDisposable
{
    private const string _documentName = "Removed.cs";
    private const string _documentText = "internal sealed class Removed;";

    private readonly RemovedDocumentProjectContextPropagator _target;
    private readonly AdhocWorkspace _workspace;

    public RemovedDocumentProjectContextPropagatorTests()
    {
        _target = new RemovedDocumentProjectContextPropagator(
            new WorkspacePathComparison());

        _workspace = new AdhocWorkspace();
    }

    [Fact]
    public void GIVEN_RemovedDocumentAndSiblingProjectContexts_WHEN_Propagating_THEN_ShouldRemoveDocumentFromEveryContext()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "Project", _documentName);
        var firstProject = AddProject("Project (net10.0)", projectPath);
        var secondProject = AddProject("Project (net9.0)", projectPath);
        var projectWithoutPath = AddProject("Project without path", filePath: null);
        var firstDocumentId = DocumentId.CreateNewId(firstProject.Id);
        var secondDocumentId = DocumentId.CreateNewId(secondProject.Id);
        var currentSolution = _workspace.CurrentSolution
            .AddDocument(
                firstDocumentId,
                _documentName,
                SourceText.From(_documentText),
                filePath: documentPath)
            .AddDocument(
                secondDocumentId,
                _documentName,
                SourceText.From(_documentText),
                filePath: documentPath);

        var candidateSolution = currentSolution.RemoveDocument(firstDocumentId);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.GetDocument(firstDocumentId).Should().BeNull();
        result.GetDocument(secondDocumentId).Should().BeNull();
        result.GetProject(projectWithoutPath.Id)?.Documents.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_RemovedDocumentAndDifferentPhysicalProjects_WHEN_Propagating_THEN_ShouldRetainOtherProjectDocument()
    {
        var firstProjectPath = Path.Combine(Path.GetTempPath(), "First", "Project.csproj");
        var secondProjectPath = Path.Combine(Path.GetTempPath(), "Second", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "Shared", _documentName);
        var firstProject = AddProject("First", firstProjectPath);
        var secondProject = AddProject("Second", secondProjectPath);
        var firstDocumentId = DocumentId.CreateNewId(firstProject.Id);
        var secondDocumentId = DocumentId.CreateNewId(secondProject.Id);
        var currentSolution = _workspace.CurrentSolution
            .AddDocument(
                firstDocumentId,
                _documentName,
                SourceText.From(_documentText),
                filePath: documentPath)
            .AddDocument(
                secondDocumentId,
                _documentName,
                SourceText.From(_documentText),
                filePath: documentPath);

        var candidateSolution = currentSolution.RemoveDocument(firstDocumentId);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
        result.GetDocument(secondDocumentId).Should().NotBeNull();
    }

    [Fact]
    public void GIVEN_RemovedDocumentWithoutPhysicalProjectPath_WHEN_Propagating_THEN_ShouldRetainCandidate()
    {
        var project = AddProject("Project", filePath: null);
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _documentName,
            SourceText.From(_documentText),
            filePath: Path.Combine(Path.GetTempPath(), _documentName));

        var candidateSolution = currentSolution.RemoveDocument(documentId);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public void GIVEN_RemovedDocumentWithoutPhysicalDocumentPath_WHEN_Propagating_THEN_ShouldRetainCandidate()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var project = AddProject("Project", projectPath);
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _documentName,
            SourceText.From(_documentText));

        var candidateSolution = currentSolution.RemoveDocument(documentId);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public void GIVEN_SiblingDoesNotContainPhysicalDocument_WHEN_Propagating_THEN_ShouldRetainCandidate()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "Project", _documentName);
        var firstProject = AddProject("Project (net10.0)", projectPath);
        AddProject("Project (net9.0)", projectPath);
        var documentId = DocumentId.CreateNewId(firstProject.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _documentName,
            SourceText.From(_documentText),
            filePath: documentPath);

        var candidateSolution = currentSolution.RemoveDocument(documentId);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_Propagating_THEN_ShouldPropagateCancellation()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var project = AddProject("Project", projectPath);
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _documentName,
            SourceText.From(_documentText),
            filePath: Path.Combine(Path.GetTempPath(), "Project", _documentName));

        var candidateSolution = currentSolution.RemoveDocument(documentId);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = () => _target.Propagate(
            currentSolution,
            candidateSolution,
            cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private Project AddProject(string name, string? filePath)
    {
        return _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            LanguageNames.CSharp,
            filePath: filePath));
    }
}
