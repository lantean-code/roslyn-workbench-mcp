using System.Text.Json.Serialization;

using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationProposal
{
    [JsonIgnore]
    public Solution? CandidateSolution { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
