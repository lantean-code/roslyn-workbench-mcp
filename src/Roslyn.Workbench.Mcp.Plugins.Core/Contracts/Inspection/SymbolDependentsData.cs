using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-dependents.
/// </summary>
public sealed record SymbolDependentsData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned direct dependents.
    /// </summary>
    public BoundedCollection<SymbolReference> Dependents { get; init; } = BoundedCollection<SymbolReference>.Empty();
}
