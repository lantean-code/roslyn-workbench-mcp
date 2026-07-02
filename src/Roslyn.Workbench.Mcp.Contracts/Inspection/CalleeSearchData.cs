using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-callees.
/// </summary>
public sealed record CalleeSearchData
{
    /// <summary>
    /// Gets the queried callable symbol.
    /// </summary>
    public SymbolReference? Source { get; init; }

    /// <summary>
    /// Gets the returned callees.
    /// </summary>
    public IReadOnlyList<SymbolReference> Callees { get; init; } = [];

    /// <summary>
    /// Gets the number of callees returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more callees were available.
    /// </summary>
    public bool HasMore { get; init; }
}
