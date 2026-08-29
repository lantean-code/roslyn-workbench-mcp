namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one node in a derived-type hierarchy projection.
/// </summary>
internal sealed record TypeHierarchyNode
{
    /// <summary>
    /// Gets the resolved type symbol.
    /// </summary>
    [Description("The resolved type symbol.")]
    public SymbolReference? Type { get; init; }

    /// <summary>
    /// Gets the zero-based depth from the queried root type.
    /// </summary>
    [Description("The zero-based depth from the queried root type.")]
    public int Depth { get; init; }

    /// <summary>
    /// Gets the nested derived types for this node.
    /// </summary>
    [Description("The nested derived types for this node.")]
    public IReadOnlyList<TypeHierarchyNode> DerivedTypes { get; init; } = [];
}
