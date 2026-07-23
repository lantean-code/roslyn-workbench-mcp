using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class LinkedDocumentChangeMergerTests : IDisposable
{
    private const string _baselineText = """
        class C
        {
            int A;
            int B;
        }
        """;

    private readonly AdhocWorkspace _workspace;
    private readonly LinkedDocumentChangeMerger _target;

    public LinkedDocumentChangeMergerTests()
    {
        _workspace = new AdhocWorkspace();
        _target = new LinkedDocumentChangeMerger();
    }

    [Fact]
    public async Task GIVEN_NoTextChanges_WHEN_Merging_THEN_ShouldReturnCandidateSolution()
    {
        var (currentSolution, _, _) = CreateLinkedSolution();

        var result = await _target.MergeAsync(
            currentSolution,
            currentSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Solution.Should().BeSameAs(currentSolution);
    }

    [Fact]
    public async Task GIVEN_ChangedDocumentWithoutLinkedSiblings_WHEN_Merging_THEN_ShouldRetainCandidateSolution()
    {
        var project = AddProject("Project");
        var document = _workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "Document.cs",
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(_baselineText),
                VersionStamp.Default)),
            filePath: Path.Combine(Path.GetTempPath(), "Project", "Document.cs")));
        var currentSolution = _workspace.CurrentSolution;
        var candidateSolution = currentSolution.WithDocumentText(
            document.Id,
            SourceText.From(_baselineText.Replace("int A;", "int Updated;", StringComparison.Ordinal)));

        var result = await _target.MergeAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Solution.Should().BeSameAs(candidateSolution);
    }

    [Fact]
    public async Task GIVEN_OneLinkedDocumentChanges_WHEN_Merging_THEN_ShouldPropagateTextToAllLinkedDocuments()
    {
        var (currentSolution, firstDocumentId, secondDocumentId) = CreateLinkedSolution();
        var updatedText = SourceText.From(
            _baselineText.Replace("int A;", "int Updated;", StringComparison.Ordinal));
        var candidateSolution = currentSolution.WithDocumentText(firstDocumentId, updatedText);

        var result = await _target.MergeAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        var firstText = await GetRequiredDocument(result.Solution, firstDocumentId)
            .GetTextAsync(TestContext.Current.CancellationToken);
        var secondText = await GetRequiredDocument(result.Solution, secondDocumentId)
            .GetTextAsync(TestContext.Current.CancellationToken);

        firstText.ContentEquals(updatedText).Should().BeTrue();
        secondText.ContentEquals(updatedText).Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentsHaveDistinctNonOverlappingChanges_WHEN_Merging_THEN_ShouldCombineChanges()
    {
        var (currentSolution, firstDocumentId, secondDocumentId) = CreateLinkedSolution();
        var firstText = SourceText.From(
            _baselineText.Replace("int A;", "int First;", StringComparison.Ordinal));
        var secondText = SourceText.From(
            _baselineText.Replace("int B;", "int Second;", StringComparison.Ordinal));
        var candidateSolution = currentSolution
            .WithDocumentText(firstDocumentId, firstText)
            .WithDocumentText(secondDocumentId, secondText);

        var result = await _target.MergeAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        var expectedText = _baselineText
            .Replace("int A;", "int First;", StringComparison.Ordinal)
            .Replace("int B;", "int Second;", StringComparison.Ordinal);
        var firstMergedText = await GetRequiredDocument(result.Solution, firstDocumentId)
            .GetTextAsync(TestContext.Current.CancellationToken);
        var secondMergedText = await GetRequiredDocument(result.Solution, secondDocumentId)
            .GetTextAsync(TestContext.Current.CancellationToken);

        firstMergedText.ToString().Should().Be(expectedText);
        secondMergedText.ToString().Should().Be(expectedText);
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentsHaveIdenticalChanges_WHEN_Merging_THEN_ShouldApplyChangeOnce()
    {
        var (currentSolution, firstDocumentId, secondDocumentId) = CreateLinkedSolution();
        var updatedText = SourceText.From(
            _baselineText.Replace("int A;", "int Updated;", StringComparison.Ordinal));
        var candidateSolution = currentSolution
            .WithDocumentText(firstDocumentId, updatedText)
            .WithDocumentText(secondDocumentId, updatedText);

        var result = await _target.MergeAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        var mergedText = await GetRequiredDocument(result.Solution, firstDocumentId)
            .GetTextAsync(TestContext.Current.CancellationToken);

        mergedText.ContentEquals(updatedText).Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentsHaveOverlappingChanges_WHEN_Merging_THEN_ShouldReturnConflict()
    {
        var (currentSolution, firstDocumentId, secondDocumentId) = CreateLinkedSolution();
        var firstText = SourceText.From(
            _baselineText.Replace("int A;", "int First;", StringComparison.Ordinal));
        var secondText = SourceText.From(
            _baselineText.Replace("int A;", "int Second;", StringComparison.Ordinal));
        var candidateSolution = currentSolution
            .WithDocumentText(firstDocumentId, firstText)
            .WithDocumentText(secondDocumentId, secondText);

        var result = await _target.MergeAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        var error = result.Error
            ?? throw new InvalidOperationException("The merge failure did not include an error.");

        error.Code.Should().Be(WorkspaceErrorCodes.LinkedDocumentConflict);
        error.Message.Should().Contain("overlapping changes");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private (Solution Solution, DocumentId FirstDocumentId, DocumentId SecondDocumentId) CreateLinkedSolution()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "Linked", "Document.cs");
        var firstProject = AddProject("FirstProject");
        var secondProject = AddProject("SecondProject");
        var firstDocument = _workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(firstProject.Id),
            "Document.cs",
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(_baselineText),
                VersionStamp.Default)),
            filePath: filePath));
        var secondDocument = _workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(secondProject.Id),
            "Document.cs",
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(_baselineText),
                VersionStamp.Default)),
            filePath: filePath));

        return (_workspace.CurrentSolution, firstDocument.Id, secondDocument.Id);
    }

    private Project AddProject(string name)
    {
        return _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            LanguageNames.CSharp,
            filePath: Path.Combine(Path.GetTempPath(), name, $"{name}.csproj")));
    }

    private static Document GetRequiredDocument(
        Solution? solution,
        DocumentId documentId)
    {
        return solution?.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The document '{documentId}' was not found.");
    }
}
