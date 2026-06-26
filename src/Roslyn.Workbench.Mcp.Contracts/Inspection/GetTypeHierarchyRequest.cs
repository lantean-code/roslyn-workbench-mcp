using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve type-hierarchy information for a resolved symbol.
/// </summary>
public sealed record GetTypeHierarchyRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether derived types should be included.
    /// </summary>
    public bool IncludeDerived { get; init; } = true;

    /// <summary>
    /// Gets the optional maximum traversal depth.
    /// </summary>
    public int? MaxDepth { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public ResultLimit? Limit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
