namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find likely impacted tests for a symbol.
/// </summary>
internal sealed record GetTestImpactRequest : WorkspaceBoundRequest
{
    private const int _defaultTestsMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional scope to search for tests.
    /// </summary>
    [Description("The optional scope to search for tests.")]
    public ScopeSelector? TestScope { get; init; }

    /// <summary>
    /// Gets a value indicating whether explanatory reasons should be included.
    /// </summary>
    [Description("Whether explanatory reasons should be included.")]
    public bool IncludeReasons { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultTestsMaxResults)]
    public int? TestsLimit { get; init; } = _defaultTestsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    [Description("The expected snapshot for location-based symbol selectors.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveTestsLimit => ResultLimit.GetEffectiveValue(TestsLimit, _defaultTestsMaxResults);
}
