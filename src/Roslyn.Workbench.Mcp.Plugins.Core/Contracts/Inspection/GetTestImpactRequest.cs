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
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional scope to search for tests.
    /// </summary>
    [Description("Limits candidate test discovery to the selected documents or projects; omitted searches the loaded solution.")]
    public ScopeSelector? TestScope { get; init; }

    /// <summary>
    /// Gets a value indicating whether explanatory reasons should be included.
    /// </summary>
    [Description("Include explanations of why each returned test may be affected; defaults to false and does not change which tests match.")]
    public bool IncludeReasons { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultTestsMaxResults)]
    [Description("Caps returned likely impacted tests. Omitted or null uses 100; zero returns no tests.")]
    public int? TestsLimit { get; init; } = _defaultTestsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective tests limit.
    /// </summary>
    internal int EffectiveTestsLimit => ResultLimit.GetEffectiveValue(TestsLimit, _defaultTestsMaxResults);
}
