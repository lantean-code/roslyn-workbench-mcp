using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-overloads.
/// </summary>
public sealed record OverloadSearchData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved overload signatures.
    /// </summary>
    public BoundedCollection<CallableSignature> Overloads { get; init; } = BoundedCollection<CallableSignature>.Empty();
}
