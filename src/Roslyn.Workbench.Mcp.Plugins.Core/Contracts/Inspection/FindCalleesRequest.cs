namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the symbols directly invoked by an executable symbol or selected body.
/// </summary>
[Description("Provide exactly one of symbol or location.")]
[RequiresExactlyOne(
    nameof(Symbol),
    nameof(Location),
    ErrorMessage = "Specify exactly one of symbol or location.")]
internal sealed record FindCalleesRequest : WorkspaceBoundRequest
{
    private const int _defaultCalleesMaxResults = 100;
    private const int _defaultMaxDepth = 3;

    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    [Description("Symbol whose callees should be found.")]
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    [Description("Source location or executable region whose contained callees should be found.")]
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether reachable source callees should be traversed transitively.
    /// </summary>
    [Description("Whether reachable source callees should be traversed transitively.")]
    public bool IncludeIndirect { get; init; }

    /// <summary>
    /// Gets the maximum call depth to traverse when indirect callees are included. Direct callees are at depth one.
    /// </summary>
    [Description("The maximum call depth to traverse when indirect callees are included. Direct callees are at depth one.")]
    [Range(1, int.MaxValue)]
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCalleesMaxResults)]
    public int? CalleesLimit { get; init; } = _defaultCalleesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective callees limit.
    /// </summary>
    internal int EffectiveCalleesLimit => ResultLimit.GetEffectiveValue(CalleesLimit, _defaultCalleesMaxResults);
}
