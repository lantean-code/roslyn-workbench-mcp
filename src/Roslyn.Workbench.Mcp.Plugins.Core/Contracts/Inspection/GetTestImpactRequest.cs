namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find likely impacted tests for a symbol.
/// </summary>
public sealed record GetTestImpactRequest : WorkspaceBoundRequest
{
    internal const int _defaultTestsMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional scope to search for tests.
    /// </summary>
    public ScopeSelector? TestScope { get; init; }

    /// <summary>
    /// Gets a value indicating whether explanatory reasons should be included.
    /// </summary>
    public bool IncludeReasons { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultTestsMaxResults)]
    public int? TestsLimit { get; init; } = _defaultTestsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
