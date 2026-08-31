namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Runs the bundled async analyzers and returns their diagnostics for selected documents.
/// </summary>
internal sealed class AsyncAnalyzerDiagnosticService : IAsyncAnalyzerDiagnosticService
{
    private readonly IAnalyzerDiagnosticService _analyzerDiagnosticService;
    private readonly IBundledAsyncAnalyzerProvider _analyzerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncAnalyzerDiagnosticService"/> class.
    /// </summary>
    /// <param name="analyzerDiagnosticService">The service that executes Roslyn analyzers.</param>
    /// <param name="analyzerProvider">The provider of the bundled async analyzers.</param>
    public AsyncAnalyzerDiagnosticService(
        IAnalyzerDiagnosticService analyzerDiagnosticService,
        IBundledAsyncAnalyzerProvider analyzerProvider)
    {
        _analyzerDiagnosticService = analyzerDiagnosticService;
        _analyzerProvider = analyzerProvider;
    }

    /// <inheritdoc/>
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
