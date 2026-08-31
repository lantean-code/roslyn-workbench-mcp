namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find duplicate code groups in a selected scope.
/// </summary>
internal sealed record FindDuplicateCodeRequest : WorkspaceBoundRequest
{
    private const int _defaultGroupsMaxResults = 25;
    private const int _defaultMinimumStatements = 3;
    private const int _defaultOccurrencesPerGroupMaxResults = 100;

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("The optional search scope.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the minimum statement count to consider.
    /// </summary>
    [Description("The minimum statement count to consider.")]
    [Range(1, int.MaxValue)]
    [DefaultValue(_defaultMinimumStatements)]
    public int MinimumStatements { get; init; } = _defaultMinimumStatements;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultGroupsMaxResults)]
    public int? GroupsLimit { get; init; } = _defaultGroupsMaxResults;

    /// <summary>
    /// Gets the optional occurrence limit applied independently to each returned duplicate group.
    /// </summary>
    [Description("Maximum number of occurrences to return for each duplicate group.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultOccurrencesPerGroupMaxResults)]
    public int? OccurrencesPerGroupLimit { get; init; } = _defaultOccurrencesPerGroupMaxResults;

    /// <summary>
    /// Gets the effective groups limit.
    /// </summary>
    internal int EffectiveGroupsLimit => ResultLimit.GetEffectiveValue(GroupsLimit, _defaultGroupsMaxResults);

    /// <summary>
    /// Gets the effective occurrences per group limit.
    /// </summary>
    internal int EffectiveOccurrencesPerGroupLimit => ResultLimit.GetEffectiveValue(OccurrencesPerGroupLimit, _defaultOccurrencesPerGroupMaxResults);
}
