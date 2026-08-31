namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Defines stable error codes for Workspace lifecycle, selection, snapshot and transaction failures.
/// </summary>
internal static class WorkspaceErrorCodes
{
    /// <summary>
    /// The selected Workspace cannot currently grant the requested operation lease.
    /// </summary>
    public const string WorkspaceBusy = "WorkspaceBusy";

    /// <summary>
    /// The selected Workspace is not loaded.
    /// </summary>
    public const string WorkspaceNotOpen = "WorkspaceNotOpen";

    /// <summary>
    /// The requested path or alias already identifies a loaded Workspace.
    /// </summary>
    public const string WorkspaceAlreadyOpen = "WorkspaceAlreadyOpen";

    /// <summary>
    /// The requested Workspace kind or configuration is unsupported.
    /// </summary>
    public const string WorkspaceNotSupported = "WorkspaceNotSupported";

    /// <summary>
    /// A monitored input changed and the loaded Workspace requires reload.
    /// </summary>
    public const string WorkspaceOutOfDate = "WorkspaceOutOfDate";

    /// <summary>
    /// The Workspace could not be loaded.
    /// </summary>
    public const string WorkspaceLoadFailed = "WorkspaceLoadFailed";

    /// <summary>
    /// The Host has reached its configured loaded-Workspace capacity.
    /// </summary>
    public const string WorkspaceCapacityReached = "WorkspaceCapacityReached";

    /// <summary>
    /// The request violates a Workspace operation contract.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// No addressable document matches the selector.
    /// </summary>
    public const string DocumentNotFound = "DocumentNotFound";

    /// <summary>
    /// More than one addressable document matches the selector.
    /// </summary>
    public const string DocumentAmbiguous = "DocumentAmbiguous";

    /// <summary>
    /// The operation requires an active transaction.
    /// </summary>
    public const string TransactionRequired = "NoActiveTransaction";

    /// <summary>
    /// The selected Workspace already has an active transaction.
    /// </summary>
    public const string TransactionAlreadyActive = "TransactionAlreadyActive";

    /// <summary>
    /// The active transaction is conflicted and cannot accept ordinary operations.
    /// </summary>
    public const string TransactionConflicted = "TransactionConflicted";

    /// <summary>
    /// Another Workspace owns the process-wide transaction slot.
    /// </summary>
    public const string TransactionOwner = "TransactionOwnedByWorkspace";

    /// <summary>
    /// The requested transaction revision is no longer retained.
    /// </summary>
    public const string TransactionHistoryUnavailable = "TransactionHistoryUnavailable";

    /// <summary>
    /// The transaction has reached its configured revision capacity.
    /// </summary>
    public const string TransactionCapacity = "RevisionCapacityReached";

    /// <summary>
    /// The commit-recovery journal has reached its configured capacity.
    /// </summary>
    public const string CommitRecoveryCapacity = "CommitRecoveryCapacityReached";

    /// <summary>
    /// Linked documents produced incompatible candidate changes.
    /// </summary>
    public const string LinkedDocumentConflict = "LinkedDocumentConflict";

    /// <summary>
    /// A mutation candidate changed after it was proposed.
    /// </summary>
    public const string MutationCandidateChanged = "MutationCandidateChanged";

    /// <summary>
    /// The supplied snapshot precondition does not match the current Workspace snapshot.
    /// </summary>
    public const string SnapshotMismatch = "SnapshotMismatch";
}
