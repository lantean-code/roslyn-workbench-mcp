namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-type-hierarchy.
/// </summary>
internal sealed record TypeHierarchyData : IQueryResponse
{
    /// <summary>
    /// Gets the queried type.
    /// </summary>
    [Description("The queried type.")]
    public SymbolReference? Type { get; init; }

    /// <summary>
    /// Gets the ordered base types.
    /// </summary>
    [Description("The ordered base types.")]
    public BoundedCollection<SymbolReference> BaseTypes { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the implemented interfaces.
    /// </summary>
    [Description("The implemented interfaces.")]
    public BoundedCollection<SymbolReference> Interfaces { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the optional derived types.
    /// </summary>
    [Description("The optional derived types.")]
    public BoundedCollection<TypeHierarchyNode>? DerivedTypes { get; init; }
}
