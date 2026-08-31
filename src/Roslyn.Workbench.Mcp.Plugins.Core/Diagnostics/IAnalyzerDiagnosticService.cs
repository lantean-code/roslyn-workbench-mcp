namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Runs selected Roslyn analyzers against a bounded set of documents.
/// </summary>
internal interface IAnalyzerDiagnosticService
{
    /// <summary>
    /// Collects diagnostics produced by the requested analyzers in the selected documents.
    /// </summary>
    /// <param name="selectedDocuments">The documents whose source diagnostics should be retained.</param>
    /// <param name="analyzers">The analyzers to execute.</param>
    /// <param name="cancellationToken">The token that cancels diagnostic collection.</param>
    /// <returns>The analyzer diagnostics located in the selected documents.</returns>
    ValueTask<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        IReadOnlyList<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken);
}
