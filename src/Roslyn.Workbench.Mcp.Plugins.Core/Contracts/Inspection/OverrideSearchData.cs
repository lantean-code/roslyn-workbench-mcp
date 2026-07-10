using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

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
    public BoundedCollection<SymbolReference> Overrides { get; init; } = BoundedCollection<SymbolReference>.Empty();
}
