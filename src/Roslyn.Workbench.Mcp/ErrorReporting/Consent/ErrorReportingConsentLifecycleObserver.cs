namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal sealed class ErrorReportingConsentLifecycleObserver : IWorkspaceSnapshotLifecycleObserver
{
    private readonly IErrorReportingConsentStore _store;

    public ErrorReportingConsentLifecycleObserver(IErrorReportingConsentStore store)
    {
        _store = store;
    }

    public void InvalidateWorkspace(string workspaceId, long workspaceEpoch)
    {
        _store.InvalidateWorkspace(workspaceId, workspaceEpoch);
    }

    public void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
    }

    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
    }
}
