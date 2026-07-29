namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve partial declarations for a resolved symbol.
/// </summary>
internal sealed record GetPartialDeclarationsRequest : WorkspaceBoundRequest
{
    private const int _defaultDeclarationsMaxResults = 32;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDeclarationsMaxResults)]
    public int? DeclarationsLimit { get; init; } = _defaultDeclarationsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDeclarationsLimit => ResultLimit.GetEffectiveValue(DeclarationsLimit, _defaultDeclarationsMaxResults);
}
