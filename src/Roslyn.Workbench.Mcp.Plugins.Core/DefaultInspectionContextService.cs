namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultInspectionContextService : IInspectionContextService
{
    public async ValueTask<string?> ReadContextAsync(Document? document, TextSpan span, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return null;
        }

        var text = await document.GetTextAsync(cancellationToken);
        var line = text.Lines.GetLineFromPosition(span.Start);
        return line.ToString().Trim();
    }

    public async ValueTask<ISymbol?> TryCreateContainingSymbolAsync(Document document, int position, CancellationToken cancellationToken)
    {
        return await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken);
    }
}
