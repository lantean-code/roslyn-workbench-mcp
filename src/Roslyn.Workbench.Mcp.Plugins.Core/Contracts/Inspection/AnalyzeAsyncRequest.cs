namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyse async-related findings in a selected scope.
/// </summary>
public sealed record AnalyzeAsyncRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? FindingsLimit { get; init; }
}
