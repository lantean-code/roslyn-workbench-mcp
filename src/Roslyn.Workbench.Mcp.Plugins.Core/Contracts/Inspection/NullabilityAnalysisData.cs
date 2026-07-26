namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-nullability.
/// </summary>
internal sealed record NullabilityAnalysisData
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    public BoundedCollection<NullabilityFinding> Findings { get; init; } = BoundedCollection.Empty<NullabilityFinding>();
}
