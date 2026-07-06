using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Fallbacks;

internal sealed class UnavailableCompilerDiagnosticService : ICompilerDiagnosticService
{
    private const string _message = "Tool execution services are unavailable.";

    public ValueTask<IReadOnlyList<Diagnostic>> GetCompilerDiagnosticsAsync(IReadOnlyList<Document> selectedDocuments, CancellationToken cancellationToken)
    {
        _ = selectedDocuments;
        _ = cancellationToken;

        return ValueTask.FromException<IReadOnlyList<Diagnostic>>(new InvalidOperationException(_message));
    }
}
