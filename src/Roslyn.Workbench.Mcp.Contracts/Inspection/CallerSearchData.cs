using Roslyn.Workbench.Mcp.Contracts.Results;
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
    public BoundedCollection<CallerInfo> Callers { get; init; } = BoundedCollection<CallerInfo>.Empty();
}
