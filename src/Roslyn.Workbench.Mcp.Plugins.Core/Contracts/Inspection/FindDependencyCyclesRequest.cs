namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find dependency cycles for a selected scope.
/// </summary>
internal sealed record FindDependencyCyclesRequest : WorkspaceBoundRequest
{
    private const int _defaultCyclesMaxResults = 25;

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

    internal int EffectiveCyclesLimit => ResultLimit.GetEffectiveValue(CyclesLimit, _defaultCyclesMaxResults);
}
