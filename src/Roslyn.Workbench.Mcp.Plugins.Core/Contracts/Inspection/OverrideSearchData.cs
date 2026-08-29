namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-overrides.
/// </summary>
internal sealed record OverrideSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the queried base member.
    /// </summary>
    [Description("The queried base member.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned overrides.
    /// </summary>
    [Description("The returned overrides.")]
    public BoundedCollection<SymbolReference> Overrides { get; init; } = BoundedCollection.Empty<SymbolReference>();
}
