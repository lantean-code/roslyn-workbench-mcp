using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to find dependency cycles for a selected scope.
/// </summary>
public sealed record FindDependencyCyclesRequest : WorkspaceBoundRequest
{
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
    public ResultLimit? Limit { get; init; }
}
