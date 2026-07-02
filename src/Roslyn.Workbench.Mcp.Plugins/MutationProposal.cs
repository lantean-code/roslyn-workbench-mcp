using System.Text.Json.Serialization;

using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents a plugin-produced candidate mutation before the host stages it.
/// </summary>
public sealed record MutationProposal
{
    /// <summary>
    /// Gets the candidate changed solution.
    /// </summary>
    [JsonIgnore]
    public Solution? CandidateSolution { get; init; }

    /// <summary>
    /// Gets the concise mutation summary.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the warnings raised while composing the proposal.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
