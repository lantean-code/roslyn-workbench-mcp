using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-nullability.
/// </summary>
public sealed record NullabilityAnalysisData
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    public BoundedCollection<NullabilityFinding> Findings { get; init; } = BoundedCollection<NullabilityFinding>.Empty();
}
