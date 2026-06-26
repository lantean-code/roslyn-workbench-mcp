using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-attributes.
/// </summary>
public sealed record SymbolAttributesData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved attributes.
    /// </summary>
    public IReadOnlyList<AttributeInfo> Attributes { get; init; } = [];

    /// <summary>
    /// Gets the number of attributes returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more attributes were available.
    /// </summary>
    public bool HasMore { get; init; }
}
