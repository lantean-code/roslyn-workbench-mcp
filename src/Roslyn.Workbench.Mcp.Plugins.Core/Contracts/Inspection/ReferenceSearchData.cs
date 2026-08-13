namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-references.
/// </summary>
internal sealed record ReferenceSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned references.
    /// </summary>
    public BoundedCollection<ReferenceLocation> References { get; init; } = BoundedCollection.Empty<ReferenceLocation>();
}
