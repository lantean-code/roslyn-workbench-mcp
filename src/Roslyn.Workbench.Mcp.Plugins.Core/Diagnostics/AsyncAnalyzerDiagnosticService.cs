namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

internal sealed class AsyncAnalyzerDiagnosticService : IAsyncAnalyzerDiagnosticService
{
    private readonly IAnalyzerDiagnosticService _analyzerDiagnosticService;
    private readonly IBundledAsyncAnalyzerProvider _analyzerProvider;

    public AsyncAnalyzerDiagnosticService(
        IAnalyzerDiagnosticService analyzerDiagnosticService,
        IBundledAsyncAnalyzerProvider analyzerProvider)
    {
        _analyzerDiagnosticService = analyzerDiagnosticService;
        _analyzerProvider = analyzerProvider;
    }

    public async ValueTask<IReadOnlyList<Diagnostic>> GetAsyncAnalyzerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        CancellationToken cancellationToken)
    {
        var analyzerDiagnostics = await _analyzerDiagnosticService.GetAnalyzerDiagnosticsAsync(
            selectedDocuments,
            _analyzerProvider.Analyzers,
            cancellationToken);

        return analyzerDiagnostics
            .Where(static diagnostic => IsBundledAsyncDiagnostic(diagnostic.Id))
            .ToArray();
    }

    private static bool IsBundledAsyncDiagnostic(string diagnosticId)
    {
        return diagnosticId is "AsyncFixer01"
            or "AsyncFixer02"
            or "AsyncFixer03"
            or "AsyncFixer04"
            or "AsyncFixer05"
            or "AsyncFixer06";
    }
}
