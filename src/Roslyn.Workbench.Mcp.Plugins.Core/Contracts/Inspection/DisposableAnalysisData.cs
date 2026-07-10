using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

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
