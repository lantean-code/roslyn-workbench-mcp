namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to estimate change impact for a symbol.
/// </summary>
internal sealed record GetChangeImpactRequest : WorkspaceBoundRequest
{
    private const int _defaultLocationsMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("Selects documents for references and callers, and projects for override and implementation counts. A document scope uses its containing project for those counts; omitted searches the loaded solution.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultLocationsMaxResults)]
    [Description("Caps returned reference locations without limiting impact counts. Omitted or null uses 100; zero returns counts without locations.")]
    public int? LocationsLimit { get; init; } = _defaultLocationsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective locations limit.
    /// </summary>
    internal int EffectiveLocationsLimit => ResultLimit.GetEffectiveValue(LocationsLimit, _defaultLocationsMaxResults);
}
