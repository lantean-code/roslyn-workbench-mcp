namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find unused symbol candidates in a selected scope.
/// </summary>
public sealed record FindUnusedSymbolsRequest : WorkspaceBoundRequest
{
    internal const int _defaultCandidatesMaxResults = 50;

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether internal members should be included.
    /// </summary>
    public bool IncludeInternal { get; init; }

    /// <summary>
    /// Gets a value indicating whether generated files should be excluded.
    /// </summary>
    public bool ExcludeGenerated { get; init; } = true;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultCandidatesMaxResults)]
    public int? CandidatesLimit { get; init; } = _defaultCandidatesMaxResults;
}
