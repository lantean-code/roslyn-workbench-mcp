namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents a plugin-produced candidate mutation before the host stages it.
/// </summary>
public sealed record MutationCandidate
{
    /// <summary>
    /// Gets the candidate changed solution.
    /// </summary>
    public required Solution CandidateSolution { get; init; }

    /// <summary>
    /// Gets the concise mutation summary.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the warnings raised while composing the candidate.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
