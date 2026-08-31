namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Collects the bundled async-analysis diagnostics for selected documents.
/// </summary>
internal interface IAsyncAnalyzerDiagnosticService
{
    /// <summary>
    /// Runs the bundled async analyzers and retains their diagnostics in the selected documents.
    /// </summary>
    /// <param name="selectedDocuments">The documents to analyse.</param>
    /// <param name="cancellationToken">The token that cancels diagnostic collection.</param>
    /// <returns>The bundled async diagnostics located in the selected documents.</returns>
    ValueTask<IReadOnlyList<Diagnostic>> GetAsyncAnalyzerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        CancellationToken cancellationToken);
}
