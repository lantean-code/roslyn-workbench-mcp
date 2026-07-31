namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed class CodeActionReferenceLifecycleObserver : IWorkspaceSnapshotLifecycleObserver
{
    private readonly ICodeActionReferenceState _state;

    public CodeActionReferenceLifecycleObserver(ICodeActionReferenceState state)
    {
        _state = state;
    }

    public void InvalidateWorkspace(string workspaceId, long workspaceEpoch)
    {
        _state.InvalidateWorkspace(workspaceId, workspaceEpoch);
    }

    public void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        _state.InvalidateTransaction(workspaceId, workspaceEpoch, transactionId);
    }

    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        _state.InvalidateSnapshots(snapshots);
    }
}
