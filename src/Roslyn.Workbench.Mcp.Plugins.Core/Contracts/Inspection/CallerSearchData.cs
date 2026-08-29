namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-callers.
/// </summary>
internal sealed record CallerSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned caller information.
    /// </summary>
    [Description("The returned caller information.")]
    public BoundedCollection<CallerInfo> Callers { get; init; } = BoundedCollection.Empty<CallerInfo>();
}
