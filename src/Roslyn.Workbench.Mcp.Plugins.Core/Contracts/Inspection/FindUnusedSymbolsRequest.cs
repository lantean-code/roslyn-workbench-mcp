namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find unused symbol candidates in a selected scope.
/// </summary>
internal sealed record FindUnusedSymbolsRequest : WorkspaceBoundRequest
{
    private const int _defaultCandidatesMaxResults = 50;

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("The optional search scope.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether internal members should be included.
    /// </summary>
    [Description("Whether internal members should be included.")]
    public bool IncludeInternal { get; init; }

    /// <summary>
    /// Gets a value indicating whether generated files should be excluded.
    /// </summary>
    [Description("Whether generated files should be excluded.")]
    public bool ExcludeGenerated { get; init; } = true;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCandidatesMaxResults)]
    public int? CandidatesLimit { get; init; } = _defaultCandidatesMaxResults;

    /// <summary>
    /// Gets the effective candidates limit.
    /// </summary>
    internal int EffectiveCandidatesLimit => ResultLimit.GetEffectiveValue(CandidatesLimit, _defaultCandidatesMaxResults);
}
