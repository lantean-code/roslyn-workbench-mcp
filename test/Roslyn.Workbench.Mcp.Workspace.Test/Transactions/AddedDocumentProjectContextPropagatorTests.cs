using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class AddedDocumentProjectContextPropagatorTests : IDisposable
{
    private const string _documentName = "Added.cs";
    private const string _documentText = "internal sealed class Added;";

    private readonly AddedDocumentProjectContextPropagator _target;
    private readonly AdhocWorkspace _workspace;

    public AddedDocumentProjectContextPropagatorTests()
    {
        _target = new AddedDocumentProjectContextPropagator(
            new WorkspacePathComparison());
        _workspace = new AdhocWorkspace();
    }

    [Fact]
    public async Task GIVEN_AddedDocumentAndSiblingProjectContexts_WHEN_Propagating_THEN_ShouldAddDocumentToEveryContext()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "Project", _documentName);
        var firstProject = AddProject("Project (net10.0)", projectPath);
        var secondProject = AddProject("Project (net9.0)", projectPath);
        var projectWithoutPath = AddProject("Project without path", filePath: null);
        var currentSolution = _workspace.CurrentSolution;
        var addedDocumentId = DocumentId.CreateNewId(firstProject.Id);
        var candidateSolution = currentSolution.AddDocument(
            addedDocumentId,
            _documentName,
            SourceText.From(_documentText),
            folders: ["Generated"],
            filePath: documentPath);

        var result = await _target.PropagateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        var firstDocument = result.GetDocument(addedDocumentId)
            ?? throw new InvalidOperationException("The originating document was not found.");

        var secondDocument = result.GetProject(secondProject.Id)?.Documents
            .SingleOrDefault(document => document.FilePath == documentPath)
            ?? throw new InvalidOperationException("The propagated document was not found.");

        var firstText = await firstDocument.GetTextAsync(TestContext.Current.CancellationToken);
        var secondText = await secondDocument.GetTextAsync(TestContext.Current.CancellationToken);

        firstText.ToString().Should().Be(_documentText);
        secondText.ContentEquals(firstText).Should().BeTrue();
        secondDocument.Name.Should().Be(_documentName);
        secondDocument.Folders.Should().Equal("Generated");
        result.GetProject(projectWithoutPath.Id)?.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AddedDocumentAndDifferentPhysicalProjects_WHEN_Propagating_THEN_ShouldRetainOriginalCandidate()
    {
        var firstProjectPath = Path.Combine(Path.GetTempPath(), "First", "Project.csproj");
        var secondProjectPath = Path.Combine(Path.GetTempPath(), "Second", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "First", _documentName);
        var firstProject = AddProject("First", firstProjectPath);
        var secondProject = AddProject("Second", secondProjectPath);
        var currentSolution = _workspace.CurrentSolution;
        var candidateSolution = currentSolution.AddDocument(
            DocumentId.CreateNewId(firstProject.Id),
            _documentName,
            SourceText.From(_documentText),
            filePath: documentPath);

        var result = await _target.PropagateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
        result.GetProject(secondProject.Id)?.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AddedDocumentWithoutPhysicalProjectPath_WHEN_Propagating_THEN_ShouldRetainOriginalCandidate()
    {
        var project = AddProject("Project", filePath: null);
        var currentSolution = _workspace.CurrentSolution;
        var candidateSolution = currentSolution.AddDocument(
            DocumentId.CreateNewId(project.Id),
            _documentName,
            SourceText.From(_documentText),
            filePath: Path.Combine(Path.GetTempPath(), _documentName));

        var result = await _target.PropagateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public async Task GIVEN_AddedDocumentWithoutPhysicalDocumentPath_WHEN_Propagating_THEN_ShouldRetainOriginalCandidate()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var project = AddProject("Project", projectPath);
        var currentSolution = _workspace.CurrentSolution;
        var candidateSolution = currentSolution.AddDocument(
            DocumentId.CreateNewId(project.Id),
            _documentName,
            SourceText.From(_documentText));

        var result = await _target.PropagateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public async Task GIVEN_SiblingAlreadyContainsPhysicalDocument_WHEN_Propagating_THEN_ShouldNotAddDuplicate()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "Project", _documentName);
        var firstProject = AddProject("Project (net10.0)", projectPath);
        var secondProject = AddProject("Project (net9.0)", projectPath);
        var existingDocumentId = DocumentId.CreateNewId(secondProject.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            existingDocumentId,
            _documentName,
            SourceText.From(_documentText),
            filePath: documentPath);

        var candidateSolution = currentSolution.AddDocument(
            DocumentId.CreateNewId(firstProject.Id),
            _documentName,
            SourceText.From(_documentText),
            filePath: documentPath);

        var result = await _target.PropagateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        var siblingDocuments = result.GetProject(secondProject.Id)?.Documents
            ?? throw new InvalidOperationException("The sibling project was not found.");

        siblingDocuments.Should().ContainSingle(document => document.FilePath == documentPath);
        siblingDocuments.Single().Id.Should().Be(existingDocumentId);
    }

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_Propagating_THEN_ShouldPropagateCancellation()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var documentPath = Path.Combine(Path.GetTempPath(), "Project", _documentName);
        var project = AddProject("Project", projectPath);
        var currentSolution = _workspace.CurrentSolution;
        var candidateSolution = currentSolution.AddDocument(
            DocumentId.CreateNewId(project.Id),
            _documentName,
            SourceText.From(_documentText),
            filePath: documentPath);

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.PropagateAsync(
            currentSolution,
            candidateSolution,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
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
