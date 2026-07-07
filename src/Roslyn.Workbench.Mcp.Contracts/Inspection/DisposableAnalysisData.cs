using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-disposables.
/// </summary>
public sealed record DisposableAnalysisData
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    public BoundedCollection<DisposableFinding> Findings { get; init; } = BoundedCollection<DisposableFinding>.Empty();
}
