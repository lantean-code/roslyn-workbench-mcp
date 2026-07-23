namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-attributes.
/// </summary>
internal sealed record SymbolAttributesData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved attributes.
    /// </summary>
    public BoundedCollection<AttributeInfo> Attributes { get; init; } = BoundedCollection<AttributeInfo>.Empty();
}
