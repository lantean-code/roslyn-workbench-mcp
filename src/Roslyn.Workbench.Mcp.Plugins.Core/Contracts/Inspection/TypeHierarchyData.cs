using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-type-hierarchy.
/// </summary>
public sealed record TypeHierarchyData
{
    /// <summary>
    /// Gets the queried type.
    /// </summary>
    public SymbolReference? Type { get; init; }

    /// <summary>
    /// Gets the ordered base types.
    /// </summary>
    public BoundedCollection<SymbolReference> BaseTypes { get; init; } = BoundedCollection<SymbolReference>.Empty();

    /// <summary>
    /// Gets the implemented interfaces.
    /// </summary>
    public BoundedCollection<SymbolReference> Interfaces { get; init; } = BoundedCollection<SymbolReference>.Empty();

    /// <summary>
    /// Gets the optional derived types.
    /// </summary>
    public BoundedCollection<TypeHierarchyNode>? DerivedTypes { get; init; }
}
