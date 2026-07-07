using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by search-symbols.
/// </summary>
public sealed record SymbolSearchData
{
    /// <summary>
    /// Gets the returned symbols.
    /// </summary>
    public BoundedCollection<SymbolReference> Symbols { get; init; } = BoundedCollection<SymbolReference>.Empty();
}
