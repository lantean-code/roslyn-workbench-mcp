namespace Roslyn.Workbench.Mcp.Workspace.State;

internal static class WorkspaceErrorCodes
{
    public const string WorkspaceBusy = "WorkspaceBusy";
    public const string WorkspaceNotOpen = "WorkspaceNotOpen";
    public const string WorkspaceAlreadyOpen = "WorkspaceAlreadyOpen";
    public const string WorkspaceNotSupported = "WorkspaceNotSupported";
    public const string WorkspaceOutOfDate = "WorkspaceOutOfDate";
    public const string WorkspaceLoadFailed = "WorkspaceLoadFailed";
    public const string WorkspaceCapacityReached = "WorkspaceCapacityReached";
    public const string InvalidRequest = "InvalidRequest";
    public const string DocumentNotFound = "DocumentNotFound";
    public const string DocumentAmbiguous = "DocumentAmbiguous";
    public const string TransactionRequired = "NoActiveTransaction";
    public const string TransactionAlreadyActive = "TransactionAlreadyActive";
    public const string TransactionConflicted = "TransactionConflicted";
    public const string TransactionOwner = "TransactionOwnedByWorkspace";
    public const string TransactionHistoryUnavailable = "TransactionHistoryUnavailable";
    public const string TransactionCapacity = "RevisionCapacityReached";
    public const string CommitRecoveryCapacity = "CommitRecoveryCapacityReached";
    public const string LinkedDocumentConflict = "LinkedDocumentConflict";
    public const string MutationCandidateChanged = "MutationCandidateChanged";
    public const string SnapshotMismatch = "SnapshotMismatch";
}
