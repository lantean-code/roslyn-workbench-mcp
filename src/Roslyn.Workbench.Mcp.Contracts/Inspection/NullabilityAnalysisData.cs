namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-nullability.
/// </summary>
public sealed record NullabilityAnalysisData
{
    /// <summary>
    /// Gets the returned findings.
    /// </summary>
    public IReadOnlyList<NullabilityFinding> Findings { get; init; } = [];

    /// <summary>
    /// Gets the number of findings returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more findings were available.
    /// </summary>
    public bool HasMore { get; init; }
}
