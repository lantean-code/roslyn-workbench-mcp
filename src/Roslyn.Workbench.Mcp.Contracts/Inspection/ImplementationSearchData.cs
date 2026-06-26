using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-implementations.
/// </summary>
public sealed record ImplementationSearchData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved implementations.
    /// </summary>
    public IReadOnlyList<SymbolReference> Implementations { get; init; } = [];

    /// <summary>
    /// Gets the number of implementations returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more implementations were available.
    /// </summary>
    public bool HasMore { get; init; }
}
