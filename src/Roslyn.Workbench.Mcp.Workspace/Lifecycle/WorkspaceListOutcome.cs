namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed record WorkspaceListOutcome
{
    public IReadOnlyList<WorkspaceIdentity> Workspaces { get; init; } = [];

    public Guid? TransactionOwnerWorkspaceId { get; init; }
}
