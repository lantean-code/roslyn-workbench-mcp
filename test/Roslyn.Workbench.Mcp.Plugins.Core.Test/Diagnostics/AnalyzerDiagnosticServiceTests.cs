namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Diagnostics;

public sealed class AnalyzerDiagnosticServiceTests
{
    [Fact]
    public async Task GIVEN_NoSelectedDocuments_WHEN_GettingAnalyzerDiagnostics_THEN_ShouldReturnEmptyWithoutExecutingAnalyzer()
    {
        var analyzer = new Mock<DiagnosticAnalyzer>();
        var target = new AnalyzerDiagnosticService();

        var result = await target.GetAnalyzerDiagnosticsAsync(
            [],
            [analyzer.Object],
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        analyzer.Verify(
            item => item.Initialize(It.IsAny<AnalysisContext>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoAnalyzers_WHEN_GettingAnalyzerDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("class Sample { }");
        var target = new AnalyzerDiagnosticService();
        var document = workspace.Solution.Projects.Single().Documents.Single();

        var result = await target.GetAnalyzerDiagnosticsAsync(
            [document],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AnalyzerReportsAcrossMultipleDocuments_WHEN_GettingAnalyzerDiagnostics_THEN_ShouldReturnOnlySelectedDocumentDiagnostic()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp(
        [
            ("First.cs", "class First { }"),
            ("Second.cs", "class Second { }"),
        ]);

        var descriptor = new DiagnosticDescriptor(
            "TEST001",
            "Title",
            "Message",
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        var analyzer = CreateSyntaxTreeAnalyzer(descriptor);
        var target = new AnalyzerDiagnosticService();
        var selectedDocument = workspace.Solution.Projects
            .Single()
            .Documents
            .Single(static document => document.Name == "Second.cs");

        var result = await target.GetAnalyzerDiagnosticsAsync(
            [selectedDocument],
            [analyzer.Object],
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].Id.Should().Be("TEST001");
        result[0].Location.SourceTree!.FilePath.Should().EndWith("Second.cs");
    }

    [Fact]
    public async Task GIVEN_AnalyzerThrows_WHEN_GettingAnalyzerDiagnostics_THEN_ShouldThrowActionableFailure()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("class Sample { }");
        var descriptor = new DiagnosticDescriptor(
            "TEST001",
            "Title",
            "Message",
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([descriptor]);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(analysisContext =>
            {
                analysisContext.RegisterCompilationAction(_ => throw new InvalidOperationException("Failure"));
            });

        var target = new AnalyzerDiagnosticService();
        var document = workspace.Solution.Projects.Single().Documents.Single();

        var action = async () => await target.GetAnalyzerDiagnosticsAsync(
            [document],
            [analyzer.Object],
            TestContext.Current.CancellationToken);

        var exception = await action.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Analyzer 'Castle.Proxies.DiagnosticAnalyzerProxy' failed during diagnostic analysis.");

        exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    private static Mock<DiagnosticAnalyzer> CreateSyntaxTreeAnalyzer(DiagnosticDescriptor descriptor)
    {
        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([descriptor]);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(analysisContext =>
            {
                analysisContext.EnableConcurrentExecution();
                analysisContext.RegisterSyntaxTreeAction(syntaxContext =>
                {
                    var root = syntaxContext.Tree.GetRoot(syntaxContext.CancellationToken);
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(descriptor, root.GetLocation()));
                });
            });

        return analyzer;
    }
}
