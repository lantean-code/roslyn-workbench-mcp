namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyse disposable-usage findings in a selected scope.
/// </summary>
internal sealed record AnalyzeDisposablesRequest : WorkspaceBoundRequest
{
    private const int _defaultFindingsMaxResults = 50;

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("The optional search scope.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultFindingsMaxResults)]
    public int? FindingsLimit { get; init; } = _defaultFindingsMaxResults;

    internal int EffectiveFindingsLimit => ResultLimit.GetEffectiveValue(FindingsLimit, _defaultFindingsMaxResults);
}
