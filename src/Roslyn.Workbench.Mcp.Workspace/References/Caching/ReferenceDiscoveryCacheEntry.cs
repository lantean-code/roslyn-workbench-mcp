using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.References.Caching;

/// <summary>
/// Retains Roslyn reference groups for one query and their cache charge.
/// </summary>
internal sealed class ReferenceDiscoveryCacheEntry
{
    /// <summary>
    /// Gets the Roslyn reference groups retained for the query.
    /// </summary>
    public ImmutableArray<ReferencedSymbol> ReferencedSymbols { get; }

    /// <summary>
    /// Gets the estimated cache charge for the retained reference groups.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceDiscoveryCacheEntry"/> class.
    /// </summary>
    /// <param name="referencedSymbols">The Roslyn reference groups cached for the query.</param>
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
