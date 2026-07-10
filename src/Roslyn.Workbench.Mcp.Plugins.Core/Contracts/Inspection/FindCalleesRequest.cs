using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the symbols directly invoked by an executable symbol or selected body.
/// </summary>
public sealed record FindCalleesRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether reachable source callees should be traversed transitively.
    /// </summary>
    public bool IncludeIndirect { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? CalleesLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
