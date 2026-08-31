namespace Roslyn.Workbench.Mcp.Plugins.Context;

/// <summary>
/// Provides lightweight source context and containing-symbol lookup for plugin result projections.
/// </summary>
internal sealed class InspectionContextService : IInspectionContextService
{
    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async ValueTask<ISymbol?> TryCreateContainingSymbolAsync(Document document, int position, CancellationToken cancellationToken)
    {
        return await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken);
    }
}
