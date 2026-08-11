namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

internal interface IAsyncAnalyzerDiagnosticService
{
    ValueTask<IReadOnlyList<Diagnostic>> GetAsyncAnalyzerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        CancellationToken cancellationToken);
}
