namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyse nullability findings in a selected scope or location.
/// </summary>
internal sealed record AnalyzeNullabilityRequest : WorkspaceBoundRequest
{
    private const int _defaultFindingsMaxResults = 50;

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
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultFindingsMaxResults)]
    public int? FindingsLimit { get; init; } = _defaultFindingsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveFindingsLimit => ResultLimit.GetEffectiveValue(FindingsLimit, _defaultFindingsMaxResults);
}
