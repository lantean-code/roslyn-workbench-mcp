namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyze data flow for a selected region.
/// </summary>
internal sealed record AnalyzeDataFlowRequest : WorkspaceBoundRequest
{
    private const int _defaultSymbolsPerCategoryMaxResults = 50;

    /// <summary>
    /// Gets an exact expression, complete statement or contiguous statement range to analyze.
    /// </summary>
    [Description("An exact expression, complete statement or contiguous statement range to analyze.")]
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the optional symbols limit applied independently to each data-flow category.
    /// </summary>
    [Description("Maximum number of symbols to return in each data-flow category.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultSymbolsPerCategoryMaxResults)]
    public int? SymbolsPerCategoryLimit { get; init; } = _defaultSymbolsPerCategoryMaxResults;

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    [Description("The expected snapshot for the selected location.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveSymbolsPerCategoryLimit => ResultLimit.GetEffectiveValue(SymbolsPerCategoryLimit, _defaultSymbolsPerCategoryMaxResults);
}
