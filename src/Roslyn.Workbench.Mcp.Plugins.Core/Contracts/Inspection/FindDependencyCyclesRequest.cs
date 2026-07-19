namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find dependency cycles for a selected scope.
/// </summary>
public sealed record FindDependencyCyclesRequest : WorkspaceBoundRequest
{
    private const int _defaultCyclesMaxResults = 25;

    /// <summary>
    /// Gets the scope to analyse.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the dependency graph granularity.
    /// </summary>
    public string Granularity { get; init; } = "Type";

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultCyclesMaxResults)]
    public int? CyclesLimit { get; init; } = _defaultCyclesMaxResults;

    internal int EffectiveCyclesLimit => ToolExecutionHelpers.GetMaxResults(CyclesLimit, _defaultCyclesMaxResults);
}
