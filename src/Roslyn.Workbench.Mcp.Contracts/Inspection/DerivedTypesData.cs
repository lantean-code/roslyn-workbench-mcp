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
    public IReadOnlyList<TypeHierarchyNode> DerivedTypes { get; init; } = [];

    /// <summary>
    /// Gets the number of derived types returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more derived types were available.
    /// </summary>
    public bool HasMore { get; init; }
}
