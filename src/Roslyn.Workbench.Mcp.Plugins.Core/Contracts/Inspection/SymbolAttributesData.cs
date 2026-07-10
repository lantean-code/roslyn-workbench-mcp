using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

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
    public BoundedCollection<AttributeInfo> Attributes { get; init; } = BoundedCollection<AttributeInfo>.Empty();
}
