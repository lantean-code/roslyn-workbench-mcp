using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

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
    public BoundedCollection<SymbolReference> Callees { get; init; } = BoundedCollection<SymbolReference>.Empty();
}
