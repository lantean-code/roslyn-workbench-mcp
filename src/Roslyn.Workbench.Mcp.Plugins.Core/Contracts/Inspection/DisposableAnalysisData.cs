namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-disposables.
/// </summary>
internal sealed record DisposableAnalysisData : IQueryResponse
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    public BoundedCollection<DisposableFinding> Findings { get; init; } = BoundedCollection.Empty<DisposableFinding>();
}
