namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve attributes for a resolved symbol.
/// </summary>
internal sealed record GetSymbolAttributesRequest : WorkspaceBoundRequest
{
    private const int _defaultAttributesMaxResults = 50;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether inherited attributes should be included.
    /// </summary>
    [Description("Whether inherited attributes should be included.")]
    public bool IncludeInherited { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultAttributesMaxResults)]
    public int? AttributesLimit { get; init; } = _defaultAttributesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective attributes limit.
    /// </summary>
    internal int EffectiveAttributesLimit => ResultLimit.GetEffectiveValue(AttributesLimit, _defaultAttributesMaxResults);
}
