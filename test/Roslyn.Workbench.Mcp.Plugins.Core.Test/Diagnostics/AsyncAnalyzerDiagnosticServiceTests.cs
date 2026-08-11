namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Diagnostics;

public sealed class AsyncAnalyzerDiagnosticServiceTests
{
    [Fact]
    public async Task GIVEN_MixedAnalyzerDiagnostics_WHEN_GettingAsyncAnalyzerDiagnostics_THEN_ShouldReturnOnlySupportedIds()
    {
        using var document = RoslynTestFactory.CreateDocument("class Sample { }");
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The test document did not provide a syntax tree.");

        var analyzerDiagnostics = Enumerable
            .Range(1, 6)
            .Select(index => RoslynTestFactory.CreateDiagnostic($"AsyncFixer0{index}", syntaxTree, index, 1))
            .Append(RoslynTestFactory.CreateDiagnostic("AsyncFixer99", syntaxTree, 0, 1))
            .ToArray();

        IReadOnlyList<Document> documents = [document.Document];
        IReadOnlyList<DiagnosticAnalyzer> analyzers = [new Mock<DiagnosticAnalyzer>().Object];
        var analyzerDiagnosticService = new Mock<IAnalyzerDiagnosticService>();
        var analyzerProvider = new Mock<IBundledAsyncAnalyzerProvider>();

        analyzerProvider
            .SetupGet(item => item.Analyzers)
            .Returns(analyzers);

        analyzerDiagnosticService
            .Setup(item => item.GetAnalyzerDiagnosticsAsync(
                documents,
                analyzers,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(analyzerDiagnostics);

        var target = new AsyncAnalyzerDiagnosticService(
            analyzerDiagnosticService.Object,
            analyzerProvider.Object);

        var result = await target.GetAsyncAnalyzerDiagnosticsAsync(
            documents,
            TestContext.Current.CancellationToken);

        result.Select(static diagnostic => diagnostic.Id).Should().BeEquivalentTo(
            "AsyncFixer01",
            "AsyncFixer02",
            "AsyncFixer03",
            "AsyncFixer04",
            "AsyncFixer05",
            "AsyncFixer06");
    }
}
