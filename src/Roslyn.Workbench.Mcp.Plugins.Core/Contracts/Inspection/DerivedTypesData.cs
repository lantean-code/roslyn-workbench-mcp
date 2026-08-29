namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-derived-types.
/// </summary>
internal sealed record DerivedTypesData : IQueryResponse
{
    /// <summary>
    /// Gets the queried base type.
    /// </summary>
    [Description("The queried base type.")]
    public SymbolReference? BaseType { get; init; }

    /// <summary>
    /// Gets the resolved derived types.
    /// </summary>
    [Description("The resolved derived types.")]
    public BoundedCollection<TypeHierarchyNode> DerivedTypes { get; init; } = BoundedCollection.Empty<TypeHierarchyNode>();
}
