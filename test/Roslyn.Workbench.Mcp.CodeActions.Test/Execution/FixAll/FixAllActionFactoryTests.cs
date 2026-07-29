using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.FixAll;

public sealed class FixAllActionFactoryTests : IDisposable
{
    private static readonly string[] _diagnosticIds = ["DiagnosticId"];

    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly InMemoryRoslynDocument _roslyn;
    private readonly FixAllActionFactory _target;

    public FixAllActionFactoryTests()
    {
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        _target = new FixAllActionFactory(_diagnosticService.Object);
    }

    [Fact]
    public async Task GIVEN_FixAllProviderReturnsNoAction_WHEN_CreatingDocumentAction_THEN_ShouldRejectUnavailableFixAll()
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync((CodeAction?)null);

        var result = await _target.CreateDocumentAsync(
            provider.Object,
            fixAllProvider.Object,
            _roslyn.Document,
            _diagnosticIds,
            "EquivalenceKey",
            "SyntheticDiagnosticId",
            TestContext.Current.CancellationToken);

        result.Failure!.Message.Should().Be("The selected code fix could not produce a fix-all action.");
    }

    [Theory]
    [InlineData(FixAllScope.Document)]
    [InlineData(FixAllScope.Solution)]
    public async Task GIVEN_DocumentFixAllScope_WHEN_CreatingAction_THEN_ShouldReturnProviderAction(
        FixAllScope scope)
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        var action = CodeAction.Create(
            "FixAllTitle",
            _ => Task.FromResult(_roslyn.Solution),
            "EquivalenceKey");

        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync(action);

        FixAllActionCreationResult result;
        if (scope == FixAllScope.Document)
        {
            result = await _target.CreateDocumentAsync(
                provider.Object,
                fixAllProvider.Object,
                _roslyn.Document,
                _diagnosticIds,
                "EquivalenceKey",
                "SyntheticDiagnosticId",
                TestContext.Current.CancellationToken);
        }
        else
        {
            result = await _target.CreateSolutionAsync(
                provider.Object,
                fixAllProvider.Object,
                _roslyn.Document,
                _diagnosticIds,
                "EquivalenceKey",
                "SyntheticDiagnosticId",
                TestContext.Current.CancellationToken);
        }

        result.Action.Should().BeSameAs(action);
        fixAllProvider.Verify(item => item.GetFixAsync(
            It.Is<FixAllContext>(context =>
                context.Document == _roslyn.Document
                && context.Project == _roslyn.Document.Project
                && context.Scope == scope
                && context.CodeFixProvider == provider.Object
                && context.CodeActionEquivalenceKey == "EquivalenceKey"
                && context.DiagnosticIds.SequenceEqual(_diagnosticIds))), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ProjectFixAll_WHEN_CreatingAction_THEN_ShouldReturnProviderAction()
    {
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        var action = CodeAction.Create(
            "FixAllTitle",
            _ => Task.FromResult(_roslyn.Solution),
            "EquivalenceKey");

        fixAllProvider
            .Setup(item => item.GetFixAsync(It.IsAny<FixAllContext>()))
            .ReturnsAsync(action);

        var result = await _target.CreateProjectAsync(
            provider.Object,
            fixAllProvider.Object,
            _roslyn.Document.Project,
            _diagnosticIds,
            "EquivalenceKey",
            "SyntheticDiagnosticId",
            TestContext.Current.CancellationToken);

        result.Action.Should().BeSameAs(action);
        fixAllProvider.Verify(item => item.GetFixAsync(
            It.Is<FixAllContext>(context =>
                context.Document == null
                && context.Project == _roslyn.Document.Project
                && context.Scope == FixAllScope.Project
                && context.CodeFixProvider == provider.Object
                && context.CodeActionEquivalenceKey == "EquivalenceKey"
                && context.DiagnosticIds.SequenceEqual(_diagnosticIds))), Times.Once);
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }
}
