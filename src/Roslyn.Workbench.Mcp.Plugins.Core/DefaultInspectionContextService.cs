using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultInspectionContextService : IInspectionContextService
{
    public async ValueTask<string?> ReadContextAsync(Document? document, TextSpan span, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return null;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(span.Start);
        return line.ToString().Trim();
    }

    public async ValueTask<ISymbol?> TryCreateContainingSymbolAsync(Document document, int position, Solution currentSolution, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return null;
        }

        return await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, currentSolution.Workspace, cancellationToken).ConfigureAwait(false);
    }
}
