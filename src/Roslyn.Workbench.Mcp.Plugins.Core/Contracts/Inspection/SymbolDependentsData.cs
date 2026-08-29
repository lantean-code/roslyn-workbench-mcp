namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-dependents.
/// </summary>
internal sealed record SymbolDependentsData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned direct dependents.
    /// </summary>
    [Description("The returned direct dependents.")]
    public BoundedCollection<SymbolReference> Dependents { get; init; } = BoundedCollection.Empty<SymbolReference>();
}
