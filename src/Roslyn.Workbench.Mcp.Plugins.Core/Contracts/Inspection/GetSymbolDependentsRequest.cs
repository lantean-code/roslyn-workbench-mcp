namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the direct dependents of a symbol.
/// </summary>
internal sealed record GetSymbolDependentsRequest : WorkspaceBoundRequest
{
    private const int _defaultDependentsMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("The optional search scope.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDependentsMaxResults)]
    public int? DependentsLimit { get; init; } = _defaultDependentsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    [Description("The expected snapshot for location-based symbol selectors.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDependentsLimit => ResultLimit.GetEffectiveValue(DependentsLimit, _defaultDependentsMaxResults);
}
