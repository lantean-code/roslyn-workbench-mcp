namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find dependency cycles for a selected scope.
/// </summary>
internal sealed record FindDependencyCyclesRequest : WorkspaceBoundRequest
{
    private const int _defaultCyclesMaxResults = 25;
    private const int _defaultEdgesMaxResults = 100_000;
    private const int _defaultNodesMaxResults = 25_000;
    private const int _maximumEdgesMaxResults = 500_000;
    private const int _maximumNodesMaxResults = 100_000;

    /// <summary>
    /// Gets the scope to analyse.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the dependency graph granularity.
    /// </summary>
    [AllowedValues("Project", "Namespace", "Type")]
    [DefaultValue("Type")]
    public string Granularity { get; init; } = "Type";

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCyclesMaxResults)]
    public int? CyclesLimit { get; init; } = _defaultCyclesMaxResults;

    /// <summary>
    /// Gets the maximum number of graph nodes to analyse.
    /// </summary>
    [Range(0, _maximumNodesMaxResults)]
    [DefaultValue(_defaultNodesMaxResults)]
    public int? NodesLimit { get; init; } = _defaultNodesMaxResults;

    /// <summary>
    /// Gets the maximum number of graph edges to analyse.
    /// </summary>
    [Range(0, _maximumEdgesMaxResults)]
    [DefaultValue(_defaultEdgesMaxResults)]
    public int? EdgesLimit { get; init; } = _defaultEdgesMaxResults;

    internal int EffectiveCyclesLimit => ResultLimit.GetEffectiveValue(CyclesLimit, _defaultCyclesMaxResults);

    internal int EffectiveNodesLimit => ResultLimit.GetEffectiveValue(NodesLimit, _defaultNodesMaxResults);

    internal int EffectiveEdgesLimit => ResultLimit.GetEffectiveValue(EdgesLimit, _defaultEdgesMaxResults);
}
