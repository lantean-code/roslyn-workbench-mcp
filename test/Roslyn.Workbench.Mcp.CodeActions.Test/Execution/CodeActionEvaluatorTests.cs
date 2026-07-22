using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Wrapping;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

#pragma warning disable CA1861 // Fresh mutable arrays keep each operation scenario isolated from other tests.
public sealed class CodeActionEvaluatorTests : IDisposable
{
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly CodeActionEvaluator _target;

    public CodeActionEvaluatorTests()
    {
        _roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        _target = new CodeActionEvaluator();
    }

    [Fact]
    public async Task GIVEN_ActionWithoutOperations_WHEN_Evaluating_THEN_ShouldRejectUnsupportedOperation()
    {
        var action = new TestCodeAction([]);

        var result = await _target.EvaluateAsync(
            action,
            _roslyn.Solution,
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.Should().Be(CodeActionApplyFailureKind.UnsupportedActionOperation);
    }

    [Fact]
    public async Task GIVEN_ActionWithUnsupportedOperation_WHEN_Evaluating_THEN_ShouldRejectUnsupportedOperation()
    {
        var action = new TestCodeAction([new UnsupportedOperation()]);

        var result = await _target.EvaluateAsync(
            action,
            _roslyn.Solution,
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.Should().Be(CodeActionApplyFailureKind.UnsupportedActionOperation);
    }

    [Fact]
    public async Task GIVEN_ActionWithOnlyWrappingBookkeeping_WHEN_Evaluating_THEN_ShouldRejectMissingSourceMutation()
    {
        var action = new TestCodeAction([new WrapItemsAction.RecordCodeActionOperation()]);

        var result = await _target.EvaluateAsync(
            action,
            _roslyn.Solution,
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.Should().Be(CodeActionApplyFailureKind.UnsupportedActionOperation);
    }

    [Fact]
    public async Task GIVEN_ActionWithMultipleApplyOperations_WHEN_Evaluating_THEN_ShouldRejectUnsupportedOperation()
    {
        var applyChanges = new ApplyChangesOperation(_roslyn.Solution);
        var action = new TestCodeAction([applyChanges, applyChanges]);

        var result = await _target.EvaluateAsync(
            action,
            _roslyn.Solution,
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.Should().Be(CodeActionApplyFailureKind.UnsupportedActionOperation);
    }

    [Fact]
    public async Task GIVEN_ActionWithOneApplyAndWrappingBookkeeping_WHEN_Evaluating_THEN_ShouldReturnCandidate()
    {
        var changedSolution = _roslyn.Solution.WithDocumentText(
            _roslyn.Document.Id,
            SourceText.From("class Changed { }"));
        var action = new TestCodeAction(
        [
            new WrapItemsAction.RecordCodeActionOperation(),
            new ApplyChangesOperation(changedSolution),
        ]);

        var result = await _target.EvaluateAsync(
            action,
            _roslyn.Solution,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeFalse();
        result.CandidateSolution.Should().BeSameAs(changedSolution);
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private sealed class TestCodeAction : CodeAction
    {
        private readonly IReadOnlyList<CodeActionOperation> _operations;

        public TestCodeAction(IReadOnlyList<CodeActionOperation> operations)
        {
            _operations = operations;
        }

        public override string Title
        {
            get
            {
                return "Title";
            }
        }

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<CodeActionOperation>>(_operations);
        }
    }

    private sealed class UnsupportedOperation : CodeActionOperation
    {
    }
}
#pragma warning restore CA1861
