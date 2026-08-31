using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents acquired cross-process commit ownership, contention, or an acquisition failure.
/// </summary>
internal sealed class WorkspaceCommitLockAcquisition
{
    /// <summary>
    /// Gets the outcome of the commit lock acquisition attempt.
    /// </summary>
    public WorkspaceCommitLockAcquisitionStatus Status { get; }

    /// <summary>
    /// Gets the owned commit lock when acquisition succeeds.
    /// </summary>
    public IWorkspaceCommitLock? Lock { get; }

    /// <summary>
    /// Gets the failure message when the lock could not be acquired.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the commit lock was acquired.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Lock))]
    public bool IsAcquired => Status == WorkspaceCommitLockAcquisitionStatus.Acquired;

    /// <summary>
    /// Gets a value indicating whether another process owns the commit lock.
    /// </summary>
    public bool IsContended => Status == WorkspaceCommitLockAcquisitionStatus.Contended;

    /// <summary>
    /// Gets a value indicating whether lock acquisition failed.
    /// </summary>
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

    /// <summary>
    /// Creates a successful acquisition result for an owned commit lock.
    /// </summary>
    /// <param name="ownership">The acquired commit lock whose ownership transfers to the result.</param>
    /// <returns>A successful result that owns <paramref name="ownership"/>.</returns>
    public static WorkspaceCommitLockAcquisition Acquired(IWorkspaceCommitLock ownership)
    {
        return new WorkspaceCommitLockAcquisition(
            WorkspaceCommitLockAcquisitionStatus.Acquired,
            ownership,
            errorMessage: null);
    }

    /// <summary>
    /// Creates a result that represents lock contention.
    /// </summary>
    /// <returns>A result that represents lock contention.</returns>
    public static WorkspaceCommitLockAcquisition Contended()
    {
        return new WorkspaceCommitLockAcquisition(
            WorkspaceCommitLockAcquisitionStatus.Contended,
            ownership: null,
            errorMessage: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="errorMessage">The message that explains the failure.</param>
    /// <returns>A result that represents failure.</returns>
    public static WorkspaceCommitLockAcquisition Failed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new WorkspaceCommitLockAcquisition(
            WorkspaceCommitLockAcquisitionStatus.Failed,
            ownership: null,
            errorMessage);
    }
}
