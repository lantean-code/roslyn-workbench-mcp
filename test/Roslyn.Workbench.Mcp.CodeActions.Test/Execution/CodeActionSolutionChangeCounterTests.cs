using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

public sealed class CodeActionSolutionChangeCounterTests
{
    private readonly CodeActionSolutionChangeCounter _target;

    public CodeActionSolutionChangeCounterTests()
    {
        _target = new CodeActionSolutionChangeCounter();
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
    public async Task GIVEN_DocumentIsMissingFromCandidate_WHEN_CountingChanges_THEN_ShouldIgnoreMissingDocument()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var document = roslyn.GetDocument("First.cs");
        var updatedSolution = roslyn.Solution.RemoveDocument(document.Id);

        var result = await _target.CountChangedSourceDocumentsAsync(
            roslyn.Solution,
            updatedSolution,
            TestContext.Current.CancellationToken);

        result.Should().Be(0);
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
