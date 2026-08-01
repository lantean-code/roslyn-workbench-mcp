namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed record WorkspaceHostSnapshot
{
    public IReadOnlyDictionary<Guid, WorkspaceSessionSnapshot> Workspaces { get; init; } = new Dictionary<Guid, WorkspaceSessionSnapshot>();

    public Guid? TransactionOwnerWorkspaceId { get; init; }
}
