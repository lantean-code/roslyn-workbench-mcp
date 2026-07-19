namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the symbols directly invoked by an executable symbol or selected body.
/// </summary>
public sealed record FindCalleesRequest : WorkspaceBoundRequest
{
    private const int _defaultCalleesMaxResults = 100;
    private const int _defaultMaxDepth = 3;

    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether reachable source callees should be traversed transitively.
    /// </summary>
    public bool IncludeIndirect { get; init; }

    /// <summary>
    /// Gets the maximum call depth to traverse when indirect callees are included. Direct callees are at depth one.
    /// </summary>
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultCalleesMaxResults)]
    public int? CalleesLimit { get; init; } = _defaultCalleesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveCalleesLimit => ToolExecutionHelpers.GetMaxResults(CalleesLimit, _defaultCalleesMaxResults);
}
