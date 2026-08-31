namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find implementations for a resolved symbol.
/// </summary>
internal sealed record FindImplementationsRequest : WorkspaceBoundRequest
{
    private const int _defaultImplementationsMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultImplementationsMaxResults)]
    public int? ImplementationsLimit { get; init; } = _defaultImplementationsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective implementations limit.
    /// </summary>
    internal int EffectiveImplementationsLimit => ResultLimit.GetEffectiveValue(ImplementationsLimit, _defaultImplementationsMaxResults);
}
