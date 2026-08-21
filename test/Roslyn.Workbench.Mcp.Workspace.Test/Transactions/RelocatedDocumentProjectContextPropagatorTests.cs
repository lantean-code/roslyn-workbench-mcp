namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class RelocatedDocumentProjectContextPropagatorTests : IDisposable
{
    private const string _currentDocumentName = "Current.cs";
    private const string _renamedDocumentName = "Renamed.cs";

    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly RelocatedDocumentProjectContextPropagator _target;
    private readonly AdhocWorkspace _workspace;

    public RelocatedDocumentProjectContextPropagatorTests()
    {
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        _target = new RelocatedDocumentProjectContextPropagator(_pathComparison.Object);
        _workspace = new AdhocWorkspace();
    }

    [Fact]
    public void GIVEN_RelocatedDocumentAndSiblingProjectContexts_WHEN_Propagating_THEN_ShouldRelocateEveryContext()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var otherProjectPath = Path.Combine(Path.GetTempPath(), "Other", "Other.csproj");
        var currentDocumentPath = Path.Combine(Path.GetTempPath(), "Project", _currentDocumentName);
        var renamedDocumentPath = Path.Combine(Path.GetTempPath(), "Project", _renamedDocumentName);
        var firstProject = AddProject("Project (net10.0)", projectPath);
        var secondProject = AddProject("Project (net9.0)", projectPath);
        var otherProject = AddProject("Other", otherProjectPath);
        AddProject("Project without path", filePath: null);
        var firstDocumentId = DocumentId.CreateNewId(firstProject.Id);
        var secondDocumentId = DocumentId.CreateNewId(secondProject.Id);
        var pathlessDocumentId = DocumentId.CreateNewId(secondProject.Id);
        var otherDocumentId = DocumentId.CreateNewId(otherProject.Id);
        var currentSolution = _workspace.CurrentSolution
            .AddDocument(
                firstDocumentId,
                _currentDocumentName,
                SourceText.From("class Current;"),
                filePath: currentDocumentPath)
            .AddDocument(
                secondDocumentId,
                _currentDocumentName,
                SourceText.From("class Current;"),
                folders: ["Linked"],
                filePath: currentDocumentPath)
            .AddDocument(pathlessDocumentId, "Pathless.cs", SourceText.From("class Pathless;"))
            .AddDocument(
                otherDocumentId,
                _currentDocumentName,
                SourceText.From("class Current;"),
                filePath: currentDocumentPath);

        var candidateSolution = currentSolution
            .WithDocumentFilePath(firstDocumentId, renamedDocumentPath)
            .WithDocumentName(firstDocumentId, _renamedDocumentName);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        var siblingDocument = result.GetDocument(secondDocumentId)
            ?? throw new InvalidOperationException("The sibling document was not found.");

        siblingDocument.FilePath.Should().Be(renamedDocumentPath);
        siblingDocument.Name.Should().Be(_renamedDocumentName);
        siblingDocument.Folders.Should().Equal("Linked");

        var otherDocument = result.GetDocument(otherDocumentId)
            ?? throw new InvalidOperationException("The document in the other physical project was not found.");

        otherDocument.FilePath.Should().Be(currentDocumentPath);
        otherDocument.Name.Should().Be(_currentDocumentName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_DocumentDidNotRelocate_WHEN_Propagating_THEN_ShouldRetainCandidate(bool documentHasPath)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var documentPath = documentHasPath
            ? Path.Combine(Path.GetTempPath(), "Project", _currentDocumentName)
            : null;

        var project = AddProject("Project", projectPath);
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _currentDocumentName,
            SourceText.From("class Current;"),
            filePath: documentPath);

        var candidateSolution = currentSolution.WithDocumentText(
            documentId,
            SourceText.From("class Updated;"));

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public void GIVEN_RelocatedDocumentWithoutPhysicalProjectPath_WHEN_Propagating_THEN_ShouldRetainCandidate()
    {
        var currentDocumentPath = Path.Combine(Path.GetTempPath(), _currentDocumentName);
        var renamedDocumentPath = Path.Combine(Path.GetTempPath(), _renamedDocumentName);
        var project = AddProject("Project", filePath: null);
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _currentDocumentName,
            SourceText.From("class Current;"),
            filePath: currentDocumentPath);

        var candidateSolution = currentSolution
            .WithDocumentFilePath(documentId, renamedDocumentPath)
            .WithDocumentName(documentId, _renamedDocumentName);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public void GIVEN_SiblingDocumentWasRemovedFromCandidate_WHEN_Propagating_THEN_ShouldRetainItsRemoval()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var currentDocumentPath = Path.Combine(Path.GetTempPath(), "Project", _currentDocumentName);
        var renamedDocumentPath = Path.Combine(Path.GetTempPath(), "Project", _renamedDocumentName);
        var firstProject = AddProject("Project (net10.0)", projectPath);
        var secondProject = AddProject("Project (net9.0)", projectPath);
        var firstDocumentId = DocumentId.CreateNewId(firstProject.Id);
        var secondDocumentId = DocumentId.CreateNewId(secondProject.Id);
        var currentSolution = _workspace.CurrentSolution
            .AddDocument(
                firstDocumentId,
                _currentDocumentName,
                SourceText.From("class Current;"),
                filePath: currentDocumentPath)
            .AddDocument(
                secondDocumentId,
                _currentDocumentName,
                SourceText.From("class Current;"),
                filePath: currentDocumentPath);

        var candidateSolution = currentSolution
            .WithDocumentFilePath(firstDocumentId, renamedDocumentPath)
            .WithDocumentName(firstDocumentId, _renamedDocumentName)
            .RemoveDocument(secondDocumentId);

        var result = _target.Propagate(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.GetDocument(secondDocumentId).Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_PropagatingRelocation_THEN_ShouldPropagateCancellation()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Project", "Project.csproj");
        var project = AddProject("Project", projectPath);
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = _workspace.CurrentSolution.AddDocument(
            documentId,
            _currentDocumentName,
            SourceText.From("class Current;"),
            filePath: Path.Combine(Path.GetTempPath(), "Project", _currentDocumentName));

        var renamedDocumentPath = Path.Combine(
            Path.GetTempPath(),
            "Project",
            _renamedDocumentName);

        var candidateSolution = currentSolution
            .WithDocumentFilePath(documentId, renamedDocumentPath)
            .WithDocumentName(documentId, _renamedDocumentName);

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
