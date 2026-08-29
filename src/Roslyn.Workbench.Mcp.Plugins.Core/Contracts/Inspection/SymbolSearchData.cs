namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by search-symbols.
/// </summary>
internal sealed record SymbolSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the returned symbols.
    /// </summary>
    [Description("The returned symbols.")]
    public BoundedCollection<SymbolReference> Symbols { get; init; } = BoundedCollection.Empty<SymbolReference>();
}
