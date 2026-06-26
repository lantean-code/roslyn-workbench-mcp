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
    public IReadOnlyList<SymbolReference> Members { get; init; } = [];

    /// <summary>
    /// Gets the number of members returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more members were available.
    /// </summary>
    public bool HasMore { get; init; }
}
