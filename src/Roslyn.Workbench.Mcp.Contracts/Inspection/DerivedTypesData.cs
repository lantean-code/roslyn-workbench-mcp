using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-derived-types.
/// </summary>
public sealed record DerivedTypesData
{
    /// <summary>
    /// Gets the queried base type.
    /// </summary>
    public SymbolReference? BaseType { get; init; }

    /// <summary>
    /// Gets the resolved derived types.
    /// </summary>
    public BoundedCollection<TypeHierarchyNode> DerivedTypes { get; init; } = BoundedCollection<TypeHierarchyNode>.Empty();
}
