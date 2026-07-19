namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find duplicate code groups in a selected scope.
/// </summary>
public sealed record FindDuplicateCodeRequest : WorkspaceBoundRequest
{
    internal const int _defaultGroupsMaxResults = 25;
    internal const int _defaultMinimumStatements = 3;

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the minimum statement count to consider.
    /// </summary>
    [DefaultValue(_defaultMinimumStatements)]
    public int MinimumStatements { get; init; } = _defaultMinimumStatements;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultGroupsMaxResults)]
    public int? GroupsLimit { get; init; } = _defaultGroupsMaxResults;
}
