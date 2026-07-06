namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Collects compiler diagnostics for tool execution.
/// </summary>
public interface ICompilerDiagnosticService
{
    /// <summary>
    /// Gets compiler diagnostics for the selected source documents.
    /// </summary>
    /// <param name="selectedDocuments">The selected documents.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The compiler diagnostics that belong to the selected documents.</returns>
    ValueTask<IReadOnlyList<Diagnostic>> GetCompilerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        CancellationToken cancellationToken);
}
