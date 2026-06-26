using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by resolve-symbol.
/// </summary>
public sealed record ResolveSymbolData
{
    /// <summary>
    /// Gets the resolved symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the canonical selector for the resolved symbol.
    /// </summary>
    public SymbolSelector? Selector { get; init; }

    /// <summary>
    /// Gets the source declarations for the symbol.
    /// </summary>
    public IReadOnlyList<ResolvedLocation> Declarations { get; init; } = [];
}
