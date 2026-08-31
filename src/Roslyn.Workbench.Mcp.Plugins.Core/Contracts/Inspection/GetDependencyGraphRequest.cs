namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return a bounded dependency graph for a selected scope.
/// </summary>
internal sealed record GetDependencyGraphRequest : WorkspaceBoundRequest
{
    private const int _defaultEdgesMaxResults = 400;
    private const int _defaultNodesMaxResults = 200;

    /// <summary>
    /// Gets the scope to graph.
    /// </summary>
    [Description("The scope to graph.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the dependency graph granularity.
    /// </summary>
    [Description("The dependency graph granularity.")]
    [AllowedValues("Project", "Namespace", "Type", "Symbol")]
    [DefaultValue("Type")]
    public string Granularity { get; init; } = "Type";

    /// <summary>
    /// Gets the optional nodes limit.
    /// </summary>
    [Description("Maximum number of nodes to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultNodesMaxResults)]
    public int? NodesLimit { get; init; } = _defaultNodesMaxResults;

    /// <summary>
    /// Gets the optional edges limit.
    /// </summary>
    [Description("Maximum number of edges to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultEdgesMaxResults)]
    public int? EdgesLimit { get; init; } = _defaultEdgesMaxResults;

    /// <summary>
    /// Gets the effective nodes limit.
    /// </summary>
    internal int EffectiveNodesLimit => ResultLimit.GetEffectiveValue(NodesLimit, _defaultNodesMaxResults);

    /// <summary>
    /// Gets the effective edges limit.
    /// </summary>
    internal int EffectiveEdgesLimit => ResultLimit.GetEffectiveValue(EdgesLimit, _defaultEdgesMaxResults);
}
