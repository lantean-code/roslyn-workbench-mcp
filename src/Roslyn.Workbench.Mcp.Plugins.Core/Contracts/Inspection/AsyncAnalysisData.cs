namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-async.
/// </summary>
internal sealed record AsyncAnalysisData : IQueryResponse
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    [Description("The returned findings.")]
    public BoundedCollection<AsyncFinding> Findings { get; init; } = BoundedCollection.Empty<AsyncFinding>();
}
