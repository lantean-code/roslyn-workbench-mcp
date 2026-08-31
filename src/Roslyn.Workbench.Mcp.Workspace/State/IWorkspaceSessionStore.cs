namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Owns the atomic Host snapshot of loaded Workspace sessions and allocates monotonic state identities.
/// </summary>
internal interface IWorkspaceSessionStore
{
    /// <summary>
    /// Reads the current immutable Host snapshot.
    /// </summary>
    /// <returns>The current Host snapshot.</returns>
    WorkspaceHostSnapshot ReadSnapshot();

    /// <summary>
    /// Reads one loaded session from the current snapshot.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier.</param>
    /// <returns>The session, or <see langword="null"/> when it is not loaded.</returns>
    WorkspaceSessionSnapshot? ReadSession(Guid workspaceId);

    /// <summary>
    /// Allocates a process-unique Workspace identifier.
    /// </summary>
    /// <returns>The allocated identifier.</returns>
    Guid AllocateWorkspaceId();

    /// <summary>
    /// Allocates the next load epoch.
    /// </summary>
    /// <returns>The allocated epoch.</returns>
    long AllocateWorkspaceEpoch();

    /// <summary>
    /// Allocates the next snapshot identifier.
    /// </summary>
    /// <returns>The allocated snapshot identifier.</returns>
    WorkspaceSnapshotId AllocateWorkspaceSnapshotId();

    /// <summary>
    /// Allocates the next transaction identifier.
    /// </summary>
    /// <returns>The allocated transaction identifier.</returns>
    WorkspaceTransactionId AllocateWorkspaceTransactionId();

    /// <summary>
    /// Atomically validates and adds a newly loaded Workspace session.
    /// </summary>
    /// <param name="session">The session to add.</param>
    /// <param name="validate">Validates the current Host snapshot before publication.</param>
    /// <returns>A validation error, or <see langword="null"/> when the session was added.</returns>
    WorkspaceOperationError? TryAddWorkspace(WorkspaceSessionSnapshot session, Func<WorkspaceHostSnapshot, WorkspaceOperationError?> validate);

    /// <summary>
    /// Removes a loaded Workspace from the Host snapshot.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier to remove.</param>
    /// <returns>The removed session, or <see langword="null"/> when it was not loaded.</returns>
    WorkspaceSessionSnapshot? RemoveWorkspace(Guid workspaceId);

    /// <summary>
    /// Atomically removes and returns every loaded Workspace session during Host shutdown.
    /// </summary>
    /// <returns>The sessions removed from the Host snapshot.</returns>
    IReadOnlyList<WorkspaceSessionSnapshot> DrainWorkspaces();

    /// <summary>
    /// Replaces a loaded session with a newer immutable snapshot.
    /// </summary>
    /// <param name="session">The replacement session.</param>
    void ReplaceSession(WorkspaceSessionSnapshot session);

    /// <summary>
    /// Replaces a session after staging and invalidates cache generations for discarded snapshots.
    /// </summary>
    /// <param name="session">The replacement session containing the staged transaction revision.</param>
    /// <param name="discardedSnapshotIds">Snapshot identifiers made unreachable by the staging operation.</param>
    void ReplaceSessionAfterStaging(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<WorkspaceSnapshotId> discardedSnapshotIds);

    /// <summary>
    /// Atomically admits a transaction when the supplied session is still current and no transaction exists.
    /// </summary>
    /// <param name="session">The current session snapshot on which to start the transaction.</param>
    /// <returns>The admitted session or a structured rejection.</returns>
    TransactionAdmissionResult TryStartTransaction(WorkspaceSessionSnapshot session);

    /// <summary>
    /// Atomically completes the current transaction when the supplied session remains current.
    /// </summary>
    /// <param name="session">The current transactional session snapshot.</param>
    /// <returns>The completed session or a structured completion failure.</returns>
    TransactionCompletionResult TryCompleteTransaction(WorkspaceSessionSnapshot session);
}
