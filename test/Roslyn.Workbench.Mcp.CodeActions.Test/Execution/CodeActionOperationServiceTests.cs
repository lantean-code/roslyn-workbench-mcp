using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Wrapping;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

#pragma warning disable CA1861 // Fresh mutable arrays keep each operation scenario isolated from other tests.
public sealed class CodeActionOperationServiceTests : IDisposable
{
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly Mock<ICodeActionDescriptorRegistry> _descriptorRegistry;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly CodeActionOperationService _target;

    public CodeActionOperationServiceTests()
    {
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _descriptorRegistry = new Mock<ICodeActionDescriptorRegistry>();
        _context = new Mock<ICodeActionExecutionContext>();
        _roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        _context.SetupGet(item => item.CurrentSolution).Returns(_roslyn.Solution);
        _descriptorRegistry
            .Setup(item => item.Classify(It.IsAny<CodeAction>(), string.Empty, It.IsAny<string>()))
            .Returns(new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            });
        _target = new CodeActionOperationService(_diagnosticService.Object, _descriptorRegistry.Object);
    }

    [Fact]
    public async Task GIVEN_ParameterisedAction_WHEN_CreatingMutationCandidate_THEN_ShouldRejectBeforeComputingOperations()
    {
        var action = new Mock<CodeAction>();
        action.SetupGet(item => item.Title).Returns("Title");
        _descriptorRegistry
            .Setup(item => item.Classify(action.Object, string.Empty, "Title"))
            .Returns(new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Parameterised,
            });

        var result = await _target.CreateMutationCandidateAsync(
            action.Object,
            "Summary",
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("ActionRequiresParameters");
    }

    [Fact]
    public async Task GIVEN_ActionWithoutOperations_WHEN_CreatingMutationCandidate_THEN_ShouldRejectUnsupportedOperation()
    {
        var action = new TestCodeAction([]);

        var result = await _target.CreateMutationCandidateAsync(
            action,
            "Summary",
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Fact]
    public async Task GIVEN_ActionWithUnsupportedOperation_WHEN_CreatingMutationCandidate_THEN_ShouldRejectUnsupportedOperation()
    {
        var action = new TestCodeAction([new UnsupportedOperation()]);

        var result = await _target.CreateMutationCandidateAsync(
            action,
            "Summary",
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Fact]
    public async Task GIVEN_ActionWithOnlyWrappingBookkeeping_WHEN_CreatingMutationCandidate_THEN_ShouldRejectMissingSourceMutation()
    {
        var action = new TestCodeAction([new WrapItemsAction.RecordCodeActionOperation()]);

        var result = await _target.CreateMutationCandidateAsync(
            action,
            "Summary",
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Fact]
    public async Task GIVEN_ActionWithMultipleApplyOperations_WHEN_CreatingMutationCandidate_THEN_ShouldRejectUnsupportedOperation()
    {
        var applyChanges = new ApplyChangesOperation(_roslyn.Solution);
        var action = new TestCodeAction([applyChanges, applyChanges]);

        var result = await _target.CreateMutationCandidateAsync(
            action,
            "Summary",
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Fact]
    public async Task GIVEN_ActionWithOneApplyAndWrappingBookkeeping_WHEN_CreatingMutationCandidate_THEN_ShouldReturnCandidate()
    {
        var changedSolution = _roslyn.Solution.WithDocumentText(_roslyn.Document.Id, SourceText.From("class Changed { }"));
        var action = new TestCodeAction(
        [
            new WrapItemsAction.RecordCodeActionOperation(),
            new ApplyChangesOperation(changedSolution),
        ]);

        var result = await _target.CreateMutationCandidateAsync(
            action,
            "Summary",
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.CandidateSolution.Should().BeSameAs(changedSolution);
        result.Data.Summary.Should().Be("Summary");
    }

    [Fact]
    public async Task GIVEN_FixAllProviderReturnsNoAction_WHEN_ApplyingDocumentFixAll_THEN_ShouldRejectUnavailableFixAll()
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync((CodeAction?)null);

        var result = await _target.ApplyFixAllAsync(
            provider.Object,
            fixAllProvider.Object,
            _roslyn.Document,
            new TextSpan(0, 1),
            FixAllScope.Document,
            ["DiagnosticId"],
            "EquivalenceKey",
            "SyntheticDiagnosticId",
            TestContext.Current.CancellationToken);

        result.Rejection!.Error!.Code.Should().Be("FixAllUnavailable");
    }

    [Fact]
    public async Task GIVEN_FixAllActionHasUnsupportedOperations_WHEN_ApplyingDocumentFixAll_THEN_ShouldRejectOperation()
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync(new TestCodeAction([new UnsupportedOperation()]));

        var result = await _target.ApplyFixAllAsync(
            provider.Object,
            fixAllProvider.Object,
            _roslyn.Document,
            new TextSpan(0, 1),
            FixAllScope.Document,
            ["DiagnosticId"],
            "EquivalenceKey",
            syntheticDiagnosticId: null,
            TestContext.Current.CancellationToken);

        result.Rejection!.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Theory]
    [InlineData(FixAllScope.Document)]
    [InlineData(FixAllScope.Solution)]
    [InlineData(FixAllScope.ContainingMember)]
    [InlineData(FixAllScope.ContainingType)]
    public async Task GIVEN_DocumentFixAllScope_WHEN_ApplyingFixAll_THEN_ShouldReturnChangedSolution(FixAllScope scope)
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        var changedSolution = _roslyn.Solution.WithDocumentText(_roslyn.Document.Id, SourceText.From("class Changed { }"));
        var action = CodeAction.Create("FixAllTitle", _ => Task.FromResult(changedSolution), "EquivalenceKey");
        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync(action);

        var result = await _target.ApplyFixAllAsync(
            provider.Object,
            fixAllProvider.Object,
            _roslyn.Document,
            new TextSpan(0, 1),
            scope,
            ["DiagnosticId"],
            "EquivalenceKey",
            "SyntheticDiagnosticId",
            TestContext.Current.CancellationToken);

        result.CandidateSolution.Should().BeSameAs(changedSolution);
        fixAllProvider.Verify(item => item.GetFixAsync(
            It.Is<FixAllContext>(context =>
                context.Document == _roslyn.Document
                && context.Project == _roslyn.Document.Project
                && context.Scope == scope
                && context.CodeFixProvider == provider.Object
                && context.CodeActionEquivalenceKey == "EquivalenceKey"
                && context.DiagnosticIds.SequenceEqual(new[] { "DiagnosticId" }))), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ProjectFixAll_WHEN_ApplyingFixAll_THEN_ShouldReturnChangedSolution()
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        var changedSolution = _roslyn.Solution.WithDocumentText(_roslyn.Document.Id, SourceText.From("class Changed { }"));
        var action = CodeAction.Create("FixAllTitle", _ => Task.FromResult(changedSolution), "EquivalenceKey");
        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync(action);

        var result = await _target.ApplyFixAllAsync(
            provider.Object,
            fixAllProvider.Object,
            _roslyn.Document.Project,
            ["DiagnosticId"],
            "EquivalenceKey",
            "SyntheticDiagnosticId",
            TestContext.Current.CancellationToken);

        result.CandidateSolution.Should().BeSameAs(changedSolution);
        fixAllProvider.Verify(item => item.GetFixAsync(
            It.Is<FixAllContext>(context =>
                context.Document == null
                && context.Project == _roslyn.Document.Project
                && context.Scope == FixAllScope.Project
                && context.CodeFixProvider == provider.Object
                && context.CodeActionEquivalenceKey == "EquivalenceKey"
                && context.DiagnosticIds.SequenceEqual(new[] { "DiagnosticId" }))), Times.Once);
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

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<CodeActionOperation>>(_operations);
        }
    }

    private sealed class UnsupportedOperation : CodeActionOperation
    {
    }
}
#pragma warning restore CA1861
