using System.Text;

using Microsoft.CodeAnalysis.Text;
using Roslyn.Workbench.Mcp.TestSupport.Workspace;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Application;

public sealed class CodeActionSolutionChangeCounterTests
{
    private readonly Mock<IWorkspaceDocumentContentService> _documentContentService;
    private readonly CodeActionSolutionChangeCounter _target;

    public CodeActionSolutionChangeCounterTests()
    {
        _documentContentService = new Mock<IWorkspaceDocumentContentService>();
        _documentContentService
            .Setup(item => item.CreateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns((Document document, CancellationToken cancellationToken) => WorkspaceDocumentContentTestFactory.CreateAsync(document, cancellationToken));

        _documentContentService
            .Setup(item => item.HasEquivalentContent(It.IsAny<WorkspaceDocumentContent>(), It.IsAny<WorkspaceDocumentContent>()))
            .Returns((WorkspaceDocumentContent expected, WorkspaceDocumentContent candidate) => WorkspaceDocumentContentTestFactory.HasEquivalentContent(expected, candidate));

        _target = new CodeActionSolutionChangeCounter(_documentContentService.Object);
    }

    [Fact]
    public async Task GIVEN_UnchangedSolution_WHEN_CountingChanges_THEN_ShouldReturnZero()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();

        var result = await _target.CountChangedSourceDocumentsAsync(
            roslyn.Solution,
            roslyn.Solution,
            TestContext.Current.CancellationToken);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GIVEN_OneChangedDocument_WHEN_CountingChanges_THEN_ShouldReturnOne()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var document = roslyn.GetDocument("First.cs");
        var updatedSolution = roslyn.Solution.WithDocumentText(document.Id, SourceText.From("class FirstChanged { }"));

        var result = await _target.CountChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_MultipleChangedDocuments_WHEN_CountingChanges_THEN_ShouldReturnChangedDocumentCount()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var firstDocument = roslyn.GetDocument("First.cs");
        var secondDocument = roslyn.GetDocument("Second.cs");
        var updatedSolution = roslyn.Solution
            .WithDocumentText(firstDocument.Id, SourceText.From("class FirstChanged { }"))
            .WithDocumentText(secondDocument.Id, SourceText.From("class SecondChanged { }"));

        var result = await _target.CountChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().Be(2);
    }

    [Fact]
    public async Task GIVEN_LargeProjectHasOneTextChange_WHEN_GettingChanges_THEN_ShouldReturnOnlyChangedDocument()
    {
        const int documentCount = 512;
        var documents = Enumerable.Range(0, documentCount)
            .Select(index => new InMemoryRoslynDocumentDefinition
            {
                Name = $"Document{index}.cs",
                Source = $"class Document{index} {{ }}",
            })
            .ToArray();

        using var roslyn = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "LargeProject",
                Documents = documents,
            },
        ]);

        var changedDocument = roslyn.GetDocument("Document256.cs");
        var updatedText = SourceText.From("class Document256Changed { }");
        var updatedSolution = roslyn.Solution.WithDocumentText(changedDocument.Id, updatedText);

        var result = await _target.GetChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Id.Should().Be(changedDocument.Id);
    }

    [Fact]
    public async Task GIVEN_DocumentTextVersionChangesWithoutContentChange_WHEN_GettingChanges_THEN_ShouldReturnEmpty()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var document = roslyn.GetDocument("First.cs");
        var originalText = await document.GetTextAsync(TestContext.Current.CancellationToken);
        var equivalentText = SourceText.From(originalText.ToString());
        var updatedSolution = roslyn.Solution.WithDocumentText(document.Id, equivalentText);

        var result = await _target.GetChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_EquivalentTextSerializesDifferently_WHEN_GettingChanges_THEN_ShouldReturnDocument()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var document = roslyn.GetDocument("First.cs");
        var contentBytes = Encoding.UTF8.GetBytes("class First { }");
        var originalText = SourceText.From(contentBytes, contentBytes.Length, Encoding.UTF8);
        var originalSolution = roslyn.Solution.WithDocumentText(document.Id, originalText);
        var encodingWithoutPreamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var updatedText = SourceText.From(contentBytes, contentBytes.Length, encodingWithoutPreamble);
        var updatedSolution = originalSolution.WithDocumentText(document.Id, updatedText);

        var result = await _target.GetChangedSourceDocumentsAsync(
            originalSolution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Id.Should().Be(document.Id);
    }

    [Fact]
    public async Task GIVEN_OnlyDocumentMetadataChanges_WHEN_GettingChanges_THEN_ShouldReturnEmpty()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var document = roslyn.GetDocument("First.cs");
        var updatedSolution = roslyn.Solution.WithDocumentFilePath(document.Id, "/workspace/Renamed.cs");

        var result = await _target.GetChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DocumentIsMissingFromCandidate_WHEN_CountingChanges_THEN_ShouldCountRemovedDocument()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var document = roslyn.GetDocument("First.cs");
        var updatedSolution = roslyn.Solution.RemoveDocument(document.Id);

        var result = await _target.CountChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_DocumentIsAddedToCandidate_WHEN_GettingChanges_THEN_ShouldReturnAddedDocument()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var project = roslyn.Solution.Projects.First();
        var documentId = DocumentId.CreateNewId(project.Id);
        var updatedSolution = roslyn.Solution.AddDocument(
            documentId,
            "Added.cs",
            SourceText.From("class Added { }"));

        var result = await _target.GetChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Id.Should().Be(documentId);
    }

    [Fact]
    public async Task GIVEN_ProjectIsAddedToCandidate_WHEN_GettingChanges_THEN_ShouldReturnItsDocuments()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var addedProject = roslyn.Solution.AddProject("AddedProject", "AddedProject", LanguageNames.CSharp);
        var documentId = DocumentId.CreateNewId(addedProject.Id);
        var updatedSolution = addedProject.Solution.AddDocument(
            documentId,
            "Added.cs",
            SourceText.From("class Added { }"));

        var result = await _target.GetChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Id.Should().Be(documentId);
    }

    [Fact]
    public async Task GIVEN_ProjectIsRemovedFromCandidate_WHEN_GettingChanges_THEN_ShouldReturnItsDocuments()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var removedProject = roslyn.Solution.Projects.First();
        var updatedSolution = roslyn.Solution.RemoveProject(removedProject.Id);

        var result = await _target.GetChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Select(static document => document.Id).Should().Equal(removedProject.DocumentIds);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_CountingChanges_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var cancellationToken = new CancellationToken(canceled: true);

        Func<Task> action = async () => await _target.CountChangedSourceDocumentsAsync(
            roslyn.Solution,
            roslyn.Solution,
            cancellationToken);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

}
