using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

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
    public IReadOnlyList<SymbolReference> BaseTypes { get; init; } = [];

    /// <summary>
    /// Gets the implemented interfaces.
    /// </summary>
    public IReadOnlyList<SymbolReference> Interfaces { get; init; } = [];

    /// <summary>
    /// Gets the optional derived types.
    /// </summary>
    public IReadOnlyList<TypeHierarchyNode>? DerivedTypes { get; init; }

    /// <summary>
    /// Gets the number of derived types returned when requested.
    /// </summary>
    public int? ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more derived types were available when requested.
    /// </summary>
    public bool? HasMore { get; init; }
}
