using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve type-hierarchy information for a resolved symbol.
/// </summary>
public sealed record GetTypeHierarchyRequest : WorkspaceBoundRequest
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
    /// Gets the optional base types limit.
    /// </summary>
    public CollectionLimit? BaseTypesLimit { get; init; }

    /// <summary>
    /// Gets the optional interfaces limit.
    /// </summary>
    public CollectionLimit? InterfacesLimit { get; init; }

    /// <summary>
    /// Gets the optional derived types limit.
    /// </summary>
    public CollectionLimit? DerivedTypesLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
