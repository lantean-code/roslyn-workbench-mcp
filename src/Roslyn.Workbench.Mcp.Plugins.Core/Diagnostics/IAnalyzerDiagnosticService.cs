namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

internal interface IAnalyzerDiagnosticService
{
    ValueTask<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        IReadOnlyList<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken);
}
