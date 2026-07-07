using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-references.
/// </summary>
public sealed record ReferenceSearchData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned references.
    /// </summary>
    public BoundedCollection<ReferenceLocation> References { get; init; } = BoundedCollection<ReferenceLocation>.Empty();
}
