namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find overrides of a virtual or abstract member.
/// </summary>
internal sealed record FindOverridesRequest : WorkspaceBoundRequest
{
    private const int _defaultOverridesMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultOverridesMaxResults)]
    public int? OverridesLimit { get; init; } = _defaultOverridesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveOverridesLimit => ResultLimit.GetEffectiveValue(OverridesLimit, _defaultOverridesMaxResults);
}
