namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve attributes for a resolved symbol.
/// </summary>
public sealed record GetSymbolAttributesRequest : WorkspaceBoundRequest
{
    private const int _defaultAttributesMaxResults = 50;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether inherited attributes should be included.
    /// </summary>
    public bool IncludeInherited { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultAttributesMaxResults)]
    public int? AttributesLimit { get; init; } = _defaultAttributesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveAttributesLimit => ToolExecutionHelpers.GetMaxResults(AttributesLimit, _defaultAttributesMaxResults);
}
