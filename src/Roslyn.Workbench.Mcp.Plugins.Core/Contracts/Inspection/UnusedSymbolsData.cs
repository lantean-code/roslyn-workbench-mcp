namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-unused-symbols.
/// </summary>
internal sealed record UnusedSymbolsData : IQueryResponse
{
    /// <summary>
    /// Gets the returned unused symbol candidates.
    /// </summary>
    [Description("The returned unused symbol candidates.")]
    public BoundedCollection<UnusedSymbolCandidate> Candidates { get; init; } = BoundedCollection.Empty<UnusedSymbolCandidate>();
}
