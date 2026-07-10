using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one node in a derived-type hierarchy projection.
/// </summary>
public sealed record TypeHierarchyNode
{
    /// <summary>
    /// Gets the resolved type symbol.
    /// </summary>
    public SymbolReference? Type { get; init; }

    /// <summary>
    /// Gets the zero-based depth from the queried root type.
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Gets the nested derived types for this node.
    /// </summary>
    public IReadOnlyList<TypeHierarchyNode> DerivedTypes { get; init; } = [];
}
