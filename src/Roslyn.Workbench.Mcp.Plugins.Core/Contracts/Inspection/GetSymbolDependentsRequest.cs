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
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultDependentsMaxResults)]
    public int? DependentsLimit { get; init; } = _defaultDependentsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDependentsLimit => ToolExecutionHelpers.GetMaxResults(DependentsLimit, _defaultDependentsMaxResults);
}
