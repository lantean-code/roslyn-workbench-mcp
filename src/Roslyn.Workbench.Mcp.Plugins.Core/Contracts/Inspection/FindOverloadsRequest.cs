namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find overloads for a resolved callable symbol.
/// </summary>
internal sealed record FindOverloadsRequest : WorkspaceBoundRequest
{
    private const int _defaultOverloadsMaxResults = 50;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultOverloadsMaxResults)]
    public int? OverloadsLimit { get; init; } = _defaultOverloadsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveOverloadsLimit => ToolExecutionHelpers.GetMaxResults(OverloadsLimit, _defaultOverloadsMaxResults);
}
