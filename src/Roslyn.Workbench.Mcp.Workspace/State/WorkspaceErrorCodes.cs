namespace Roslyn.Workbench.Mcp.Workspace.State;

internal static class WorkspaceErrorCodes
{
    internal const string WorkspaceBusy = "WorkspaceBusy";
    internal const string WorkspaceNotOpen = "WorkspaceNotOpen";
    internal const string WorkspaceAlreadyOpen = "WorkspaceAlreadyOpen";
    internal const string WorkspaceNotSupported = "WorkspaceNotSupported";
    internal const string WorkspaceOutOfDate = "WorkspaceOutOfDate";
    internal const string WorkspaceLoadFailed = "WorkspaceLoadFailed";
    internal const string WorkspaceCapacityReached = "WorkspaceCapacityReached";
    internal const string TransactionRequired = "NoActiveTransaction";
    internal const string TransactionAlreadyActive = "TransactionAlreadyActive";
    internal const string TransactionConflicted = "TransactionConflicted";
    internal const string TransactionOwner = "TransactionOwnedByWorkspace";
    internal const string TransactionHistoryUnavailable = "TransactionHistoryUnavailable";
    internal const string TransactionCapacity = "RevisionCapacityReached";
    internal const string SnapshotMismatch = "SnapshotMismatch";
}
