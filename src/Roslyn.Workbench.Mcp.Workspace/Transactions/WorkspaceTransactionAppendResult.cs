namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceTransactionAppendResult
{
    public required WorkspaceTransaction Transaction { get; init; }

    public IReadOnlyList<WorkspaceSnapshotId> DiscardedSnapshotIds { get; init; } = [];
}
