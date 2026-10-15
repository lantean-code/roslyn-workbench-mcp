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
    [Description("Selects documents or projects to analyse when location is omitted; omitted searches the loaded solution. Ignored when location is supplied.")]
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
    [Description("Caps returned nullable-flow diagnostics. Omitted or null uses 50; zero returns no findings.")]
    public int? FindingsLimit { get; init; } = _defaultFindingsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective findings limit.
    /// </summary>
    internal int EffectiveFindingsLimit => ResultLimit.GetEffectiveValue(FindingsLimit, _defaultFindingsMaxResults);
}
