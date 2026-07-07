using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-dependents.
/// </summary>
[PublishedCollectionResponse(nameof(Dependents))]
public sealed record SymbolDependentsData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned direct dependents.
    /// </summary>
    public IReadOnlyList<SymbolReference> Dependents { get; init; } = [];

    /// <summary>
    /// Gets the number of dependents returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more dependents were available.
    /// </summary>
    public bool HasMore { get; init; }
}
