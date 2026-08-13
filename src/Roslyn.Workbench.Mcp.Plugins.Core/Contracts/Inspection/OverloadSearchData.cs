namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-overloads.
/// </summary>
internal sealed record OverloadSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved overload signatures.
    /// </summary>
    public BoundedCollection<CallableSignature> Overloads { get; init; } = BoundedCollection.Empty<CallableSignature>();
}
