namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Retains process-lifetime transaction commit confirmation in memory.
/// </summary>
internal sealed class CommitConfirmationState : ICommitConfirmationState
{
    private int _approvedForSession;

    /// <inheritdoc/>
    public bool IsApprovedForSession => Volatile.Read(ref _approvedForSession) != 0;

    /// <inheritdoc/>
    public void ApproveForSession()
    {
        Interlocked.Exchange(ref _approvedForSession, 1);
    }
}
