using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-callers.
/// </summary>
public sealed record CallerSearchData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned caller information.
    /// </summary>
    public IReadOnlyList<CallerInfo> Callers { get; init; } = [];

    /// <summary>
    /// Gets the number of callers returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more callers were available.
    /// </summary>
    public bool HasMore { get; init; }
}
