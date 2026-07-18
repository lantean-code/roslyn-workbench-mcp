namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find duplicate code groups in a selected scope.
/// </summary>
public sealed record FindDuplicateCodeRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the minimum statement count to consider.
    /// </summary>
    public int MinimumStatements { get; init; } = 3;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? GroupsLimit { get; init; }
}
