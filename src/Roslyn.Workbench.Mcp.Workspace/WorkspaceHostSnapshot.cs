namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceHostSnapshot
{
    public IReadOnlyDictionary<string, WorkspaceSessionSnapshot> Workspaces { get; init; } = new Dictionary<string, WorkspaceSessionSnapshot>(StringComparer.Ordinal);

    public string? TransactionOwnerWorkspaceId { get; init; }
}
