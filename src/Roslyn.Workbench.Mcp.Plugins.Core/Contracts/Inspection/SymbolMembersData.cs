namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-members.
/// </summary>
internal sealed record SymbolMembersData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved members.
    /// </summary>
    [Description("The resolved members.")]
    public BoundedCollection<SymbolReference> Members { get; init; } = BoundedCollection.Empty<SymbolReference>();
}
