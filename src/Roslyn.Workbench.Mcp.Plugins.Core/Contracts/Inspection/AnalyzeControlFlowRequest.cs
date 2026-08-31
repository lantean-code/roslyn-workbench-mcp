namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyze control flow for a selected region.
/// </summary>
internal sealed record AnalyzeControlFlowRequest : WorkspaceBoundRequest
{
    private const int _defaultExitsMaxResults = 100;
    private const int _defaultReturnsMaxResults = 100;

    /// <summary>
    /// Gets an exact complete statement or contiguous statement range to analyze.
    /// </summary>
    [Description("An exact complete statement or contiguous statement range to analyze.")]
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the optional exit points limit.
    /// </summary>
    [Description("Maximum number of exit points to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultExitsMaxResults)]
    public int? ExitsLimit { get; init; } = _defaultExitsMaxResults;

    /// <summary>
    /// Gets the optional return statements limit.
    /// </summary>
    [Description("Maximum number of return statements to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultReturnsMaxResults)]
    public int? ReturnsLimit { get; init; } = _defaultReturnsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective exits limit.
    /// </summary>
    internal int EffectiveExitsLimit => ResultLimit.GetEffectiveValue(ExitsLimit, _defaultExitsMaxResults);

    /// <summary>
    /// Gets the effective returns limit.
    /// </summary>
    internal int EffectiveReturnsLimit => ResultLimit.GetEffectiveValue(ReturnsLimit, _defaultReturnsMaxResults);
}
