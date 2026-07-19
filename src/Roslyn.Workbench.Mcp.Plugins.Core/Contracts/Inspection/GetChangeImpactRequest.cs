namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to estimate change impact for a symbol.
/// </summary>
public sealed record GetChangeImpactRequest : WorkspaceBoundRequest
{
    internal const int _defaultLocationsMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultLocationsMaxResults)]
    public int? LocationsLimit { get; init; } = _defaultLocationsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
