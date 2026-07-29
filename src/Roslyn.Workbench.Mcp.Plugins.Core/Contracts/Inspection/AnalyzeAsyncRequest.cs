namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyse async-related findings in a selected scope.
/// </summary>
internal sealed record AnalyzeAsyncRequest : WorkspaceBoundRequest
{
    private const int _defaultFindingsMaxResults = 50;

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultFindingsMaxResults)]
    public int? FindingsLimit { get; init; } = _defaultFindingsMaxResults;

    internal int EffectiveFindingsLimit => ResultLimit.GetEffectiveValue(FindingsLimit, _defaultFindingsMaxResults);
}
