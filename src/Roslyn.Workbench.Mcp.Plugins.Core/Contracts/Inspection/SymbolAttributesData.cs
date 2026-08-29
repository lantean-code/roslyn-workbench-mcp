namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-attributes.
/// </summary>
internal sealed record SymbolAttributesData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved attributes.
    /// </summary>
    [Description("The resolved attributes.")]
    public BoundedCollection<AttributeInfo> Attributes { get; init; } = BoundedCollection.Empty<AttributeInfo>();
}
