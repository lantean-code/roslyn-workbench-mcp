using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceListOutcome
{
    public IReadOnlyList<WorkspaceIdentity> Workspaces { get; init; } = [];

    public string? TransactionOwnerWorkspaceId { get; init; }
}
