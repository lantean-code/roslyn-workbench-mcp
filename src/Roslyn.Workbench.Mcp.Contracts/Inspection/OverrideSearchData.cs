using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-overrides.
/// </summary>
public sealed record OverrideSearchData
{
    /// <summary>
    /// Gets the queried base member.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned overrides.
    /// </summary>
    public IReadOnlyList<SymbolReference> Overrides { get; init; } = [];

    /// <summary>
    /// Gets the number of overrides returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more overrides were available.
    /// </summary>
    public bool HasMore { get; init; }
}
