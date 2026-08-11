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
                Name = "FirstProject",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "First.cs", Source = "class First { }" },
                    new InMemoryRoslynDocumentDefinition { Name = "Second.cs", Source = "class Second { }" },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "SecondProject",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "Third.cs", Source = "class Third { }" },
                ],
            },
        ]);

        _diagnosticIds = ["DiagnosticId"];
        _target = new WorkspaceFixAllDiagnosticProvider(_diagnosticService.Object, _diagnosticIds);
    }

    [Fact]
    public async Task GIVEN_ProjectCallbacksShareOneProvider_WHEN_GettingDiagnostics_THEN_ShouldCollectAndPartitionOnce()
    {
        var project = _roslyn.GetProject("FirstProject");
        var document = _roslyn.GetDocument("First.cs");
        var syntaxTree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The test document did not provide a syntax tree.");

        var sourceDiagnostic = RoslynTestFactory.CreateDiagnostic("SourceDiagnosticId", syntaxTree, 0, 1);
        var projectDiagnostic = CreateProjectDiagnostic("ProjectDiagnosticId");
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>
        {
            [syntaxTree] = [sourceDiagnostic],
        };

        var collection = new CodeActionProjectDiagnosticCollection(
            [sourceDiagnostic, projectDiagnostic],
            [projectDiagnostic],
            diagnosticsBySyntaxTree,
            []);

        _diagnosticService
            .Setup(item => item.CollectProjectDiagnosticsAsync(
                project,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(collection);

        var documentDiagnostics = await _target.GetDocumentDiagnosticsAsync(
            document,
            TestContext.Current.CancellationToken);

        var projectDiagnostics = await _target.GetProjectDiagnosticsAsync(
            project,
            TestContext.Current.CancellationToken);

        var allDiagnostics = await _target.GetAllDiagnosticsAsync(
            project,
            TestContext.Current.CancellationToken);

        documentDiagnostics.Should().Equal(sourceDiagnostic);
        projectDiagnostics.Should().Equal(projectDiagnostic);
        allDiagnostics.Should().Equal(sourceDiagnostic, projectDiagnostic);
        _diagnosticService.Verify(item => item.CollectProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ConcurrentCallbacks_WHEN_GettingSameProjectDiagnostics_THEN_ShouldShareInFlightCollection()
    {
        var project = _roslyn.GetProject("FirstProject");
        var completion = new TaskCompletionSource<CodeActionProjectDiagnosticCollection>(TaskCreationOptions.RunContinuationsAsynchronously);
        _diagnosticService
            .Setup(item => item.CollectProjectDiagnosticsAsync(
                project,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .Returns(completion.Task);

        var firstRequest = _target.GetAllDiagnosticsAsync(project, TestContext.Current.CancellationToken);
        var secondRequest = _target.GetProjectDiagnosticsAsync(project, TestContext.Current.CancellationToken);
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>();
        var collection = new CodeActionProjectDiagnosticCollection([], [], diagnosticsBySyntaxTree, []);
        completion.SetResult(collection);

        await Task.WhenAll(firstRequest, secondRequest);

        _diagnosticService.Verify(item => item.CollectProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CachedCollectionIsInFlight_WHEN_ASecondCallbackIsCancelled_THEN_ShouldCancelOnlyThatWait()
    {
        var project = _roslyn.GetProject("FirstProject");
        var completion = new TaskCompletionSource<CodeActionProjectDiagnosticCollection>(TaskCreationOptions.RunContinuationsAsynchronously);
        _diagnosticService
            .Setup(item => item.CollectProjectDiagnosticsAsync(
                project,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .Returns(completion.Task);

        var firstRequest = _target.GetAllDiagnosticsAsync(project, TestContext.Current.CancellationToken);
        var cancelledToken = new CancellationToken(canceled: true);
        Func<Task> cancelledRequest = async () => await _target.GetProjectDiagnosticsAsync(
            project,
            cancelledToken);

        await cancelledRequest.Should().ThrowAsync<OperationCanceledException>();

        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>();
        var collection = new CodeActionProjectDiagnosticCollection([], [], diagnosticsBySyntaxTree, []);
        completion.SetResult(collection);
        await firstRequest;

        _diagnosticService.Verify(item => item.CollectProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DifferentProjects_WHEN_GettingAllDiagnostics_THEN_ShouldCollectEachProject()
    {
        var firstProject = _roslyn.GetProject("FirstProject");
        var secondProject = _roslyn.GetProject("SecondProject");
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>();
        var collection = new CodeActionProjectDiagnosticCollection([], [], diagnosticsBySyntaxTree, []);
        _diagnosticService
            .Setup(item => item.CollectProjectDiagnosticsAsync(
                It.IsAny<Project>(),
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(collection);

        await _target.GetAllDiagnosticsAsync(firstProject, TestContext.Current.CancellationToken);
        await _target.GetAllDiagnosticsAsync(secondProject, TestContext.Current.CancellationToken);

        _diagnosticService.Verify(item => item.CollectProjectDiagnosticsAsync(
            firstProject,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);

        _diagnosticService.Verify(item => item.CollectProjectDiagnosticsAsync(
            secondProject,
            _diagnosticIds,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DocumentHasNoSyntaxTree_WHEN_GettingDocumentDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var unsupported = RoslynTestFactory.CreateUnsupportedDocument();
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>();
        var collection = new CodeActionProjectDiagnosticCollection([], [], diagnosticsBySyntaxTree, []);
        _diagnosticService
            .Setup(item => item.CollectProjectDiagnosticsAsync(
                unsupported.Document.Project,
                _diagnosticIds,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(collection);

        var result = await _target.GetDocumentDiagnosticsAsync(
            unsupported.Document,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _roslyn.Dispose();
    }

    private static Diagnostic CreateProjectDiagnostic(string diagnosticId)
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
