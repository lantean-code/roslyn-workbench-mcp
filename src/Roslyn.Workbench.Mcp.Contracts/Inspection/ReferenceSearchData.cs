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
    public IReadOnlyList<ReferenceLocation> References { get; init; } = [];

    /// <summary>
    /// Gets the number of references returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more references were available.
    /// </summary>
    public bool HasMore { get; init; }
}
