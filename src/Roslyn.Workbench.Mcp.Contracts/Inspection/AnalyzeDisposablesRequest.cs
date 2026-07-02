using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to analyse disposable-usage findings in a selected scope.
/// </summary>
public sealed record AnalyzeDisposablesRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public ResultLimit? Limit { get; init; }
}
