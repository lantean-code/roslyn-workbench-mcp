namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.FixAll;

public sealed class WorkspaceFixAllDiagnosticProviderTests : IDisposable
{
    private readonly Mock<ICodeActionDiagnosticService> _diagnosticService;
    private readonly InMemoryRoslynSolution _roslyn;
    private readonly IReadOnlyList<string> _diagnosticIds;
    private readonly WorkspaceFixAllDiagnosticProvider _target;

    public WorkspaceFixAllDiagnosticProviderTests()
    {
        _diagnosticService = new Mock<ICodeActionDiagnosticService>();
        _roslyn = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "First.cs", Source = "class First { }" },
                    new InMemoryRoslynDocumentDefinition { Name = "Second.cs", Source = "class Second { }" },
                ],
            },
        ]);

        _diagnosticIds = ["DiagnosticId"];
        _target = new WorkspaceFixAllDiagnosticProvider(_diagnosticService.Object, _diagnosticIds, "SyntheticDiagnosticId");
    }

    [Fact]
    public async Task GIVEN_Document_WHEN_GettingDocumentDiagnostics_THEN_ShouldDelegateScopedRequest()
    {
        var document = _roslyn.GetDocument("First.cs");
        IReadOnlyList<Diagnostic> expected = [CreateDiagnostic("DiagnosticId")];
        _diagnosticService
            .Setup(item => item.GetScopedCodeFixDiagnosticsAsync(
                document,
                _diagnosticIds,
                null,
                "SyntheticDiagnosticId",
                TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        var result = await _target.GetDocumentDiagnosticsAsync(document, TestContext.Current.CancellationToken);

        result.Should().Equal(expected);
        _diagnosticService.Verify(item => item.GetScopedCodeFixDiagnosticsAsync(
            document,
            _diagnosticIds,
            null,
            "SyntheticDiagnosticId",
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_Project_WHEN_GettingProjectDiagnostics_THEN_ShouldDelegateProjectRequest()
    {
        var project = _roslyn.GetProject("Project");
        IReadOnlyList<Diagnostic> expected = [CreateDiagnostic("DiagnosticId")];
        _diagnosticService
            .Setup(item => item.GetProjectDiagnosticsAsync(
                project,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        var result = await _target.GetProjectDiagnosticsAsync(project, TestContext.Current.CancellationToken);

        result.Should().Equal(expected);
        _diagnosticService.Verify(item => item.GetProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MultipleDocumentsAndProjectDiagnostics_WHEN_GettingAllDiagnostics_THEN_ShouldAggregateInProjectOrder()
    {
        var project = _roslyn.GetProject("Project");
        var firstDocument = _roslyn.GetDocument("First.cs");
        var secondDocument = _roslyn.GetDocument("Second.cs");
        var firstDiagnostic = CreateDiagnostic("FirstDiagnosticId");
        var secondDiagnostic = CreateDiagnostic("SecondDiagnosticId");
        var projectDiagnostic = CreateDiagnostic("ProjectDiagnosticId");
        _diagnosticService
            .Setup(item => item.GetDocumentDiagnosticsAsync(
                firstDocument,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([firstDiagnostic]);

        _diagnosticService
            .Setup(item => item.GetDocumentDiagnosticsAsync(
                secondDocument,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([secondDiagnostic]);

        _diagnosticService
            .Setup(item => item.GetProjectDiagnosticsAsync(
                project,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([projectDiagnostic]);

        var result = await _target.GetAllDiagnosticsAsync(project, TestContext.Current.CancellationToken);

        result.Should().Equal(firstDiagnostic, secondDiagnostic, projectDiagnostic);
        _diagnosticService.Verify(item => item.GetDocumentDiagnosticsAsync(
            It.IsAny<Document>(),
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Exactly(2));

        _diagnosticService.Verify(item => item.GetProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private static Diagnostic CreateDiagnostic(string diagnosticId)
    {
        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            diagnosticId,
            diagnosticId,
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, Location.None);
    }
}
