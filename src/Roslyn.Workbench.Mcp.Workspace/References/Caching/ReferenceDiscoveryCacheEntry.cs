using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.References.Caching;

internal sealed class ReferenceDiscoveryCacheEntry
{
    public ImmutableArray<ReferencedSymbol> ReferencedSymbols { get; }

    public long Size { get; }

    public ReferenceDiscoveryCacheEntry(ImmutableArray<ReferencedSymbol> referencedSymbols)
    {
        ReferencedSymbols = referencedSymbols;
        Size = CalculateSize(referencedSymbols);
    }

    private static long CalculateSize(ImmutableArray<ReferencedSymbol> referencedSymbols)
    {
        long size = 1;
        foreach (var referencedSymbol in referencedSymbols)
        {
            size++;
            foreach (var _ in referencedSymbol.Locations)
            {
                size++;
            }
        }

        return size;
    }
}
