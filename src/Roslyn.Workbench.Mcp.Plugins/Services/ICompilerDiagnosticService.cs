namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Collects compiler diagnostics for tool execution.
/// </summary>
public interface ICompilerDiagnosticService
{
    /// <summary>
    /// Gets compiler diagnostics for the selected source documents.
    /// </summary>
    /// <param name="selectedDocuments">The documents whose source diagnostics should be retained.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The compiler diagnostics that belong to the selected documents.</returns>
    ValueTask<IReadOnlyList<Diagnostic>> GetCompilerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        CancellationToken cancellationToken);
}
