using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to analyse nullability findings in a selected scope or location.
/// </summary>
public sealed record AnalyzeNullabilityRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? FindingsLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
