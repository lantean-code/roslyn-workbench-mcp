using Microsoft.CodeAnalysis.Text;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class UnavailableInspectionContextService : IInspectionContextService
{
    private const string _message = "Tool execution services are unavailable.";

    public ValueTask<string?> ReadContextAsync(Document? document, TextSpan span, CancellationToken cancellationToken)
    {
        _ = document;
        _ = span;
        _ = cancellationToken;

        return ValueTask.FromException<string?>(new InvalidOperationException(_message));
    }

    public ValueTask<ISymbol?> TryCreateContainingSymbolAsync(Document document, int position, Solution currentSolution, CancellationToken cancellationToken)
    {
        _ = document;
        _ = position;
        _ = currentSolution;
        _ = cancellationToken;

        return ValueTask.FromException<ISymbol?>(new InvalidOperationException(_message));
    }
}
