namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-unused-symbols.
/// </summary>
[PublishedCollectionResponse(nameof(Candidates))]
public sealed record UnusedSymbolsData
{
    /// <summary>
    /// Gets the returned unused symbol candidates.
    /// </summary>
    public IReadOnlyList<UnusedSymbolCandidate> Candidates { get; init; } = [];

    /// <summary>
    /// Gets the number of candidates returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more candidates were available.
    /// </summary>
    public bool HasMore { get; init; }
}
