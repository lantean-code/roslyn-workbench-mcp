namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-overloads.
/// </summary>
internal sealed record OverloadSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved overload signatures.
    /// </summary>
    [Description("The resolved overload signatures.")]
    public BoundedCollection<CallableSignature> Overloads { get; init; } = BoundedCollection.Empty<CallableSignature>();
}
