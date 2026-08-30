namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find types derived from a resolved symbol.
/// </summary>
internal sealed record FindDerivedTypesRequest : WorkspaceBoundRequest
{
    private const int _defaultDerivedTypesMaxResults = 100;
    private const int _defaultMaxDepth = 3;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the maximum traversal depth. Directly derived types are at depth one.
    /// </summary>
    [Description("Maximum traversal depth; directly derived types are at depth one.")]
    [Range(1, int.MaxValue)]
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDerivedTypesMaxResults)]
    public int? DerivedTypesLimit { get; init; } = _defaultDerivedTypesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDerivedTypesLimit => ResultLimit.GetEffectiveValue(DerivedTypesLimit, _defaultDerivedTypesMaxResults);
}
