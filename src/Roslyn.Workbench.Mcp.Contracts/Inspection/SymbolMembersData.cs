using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-members.
/// </summary>
public sealed record SymbolMembersData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved members.
    /// </summary>
    public BoundedCollection<SymbolReference> Members { get; init; } = BoundedCollection<SymbolReference>.Empty();
}
