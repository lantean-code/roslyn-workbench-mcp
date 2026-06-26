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
    public IReadOnlyList<CallableSignature> Overloads { get; init; } = [];

    /// <summary>
    /// Gets the number of overloads returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more overloads were available.
    /// </summary>
    public bool HasMore { get; init; }
}
