using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitLockAcquisition
{
    public WorkspaceCommitLockAcquisitionStatus Status { get; }

    public IWorkspaceCommitLock? Lock { get; }

    public string? ErrorMessage { get; }

    [MemberNotNullWhen(true, nameof(Lock))]
    public bool IsAcquired => Status == WorkspaceCommitLockAcquisitionStatus.Acquired;

    public bool IsContended => Status == WorkspaceCommitLockAcquisitionStatus.Contended;

    [MemberNotNullWhen(true, nameof(ErrorMessage))]
    public bool IsFailed => Status == WorkspaceCommitLockAcquisitionStatus.Failed;

    private WorkspaceCommitLockAcquisition(
        WorkspaceCommitLockAcquisitionStatus status,
        IWorkspaceCommitLock? ownership,
        string? errorMessage)
    {
        Status = status;
        Lock = ownership;
        ErrorMessage = errorMessage;
    }

    public static WorkspaceCommitLockAcquisition Acquired(IWorkspaceCommitLock ownership)
    {
        return new WorkspaceCommitLockAcquisition(
            WorkspaceCommitLockAcquisitionStatus.Acquired,
            ownership,
            errorMessage: null);
    }

    public static WorkspaceCommitLockAcquisition Contended()
    {
        return new WorkspaceCommitLockAcquisition(
            WorkspaceCommitLockAcquisitionStatus.Contended,
            ownership: null,
            errorMessage: null);
    }

    public static WorkspaceCommitLockAcquisition Failed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new WorkspaceCommitLockAcquisition(
            WorkspaceCommitLockAcquisitionStatus.Failed,
            ownership: null,
            errorMessage);
    }
}
