namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-async.
/// </summary>
public sealed record AsyncAnalysisData
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    public BoundedCollection<AsyncFinding> Findings { get; init; } = BoundedCollection<AsyncFinding>.Empty();
}
