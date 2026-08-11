namespace Roslyn.Workbench.Mcp.Protocol.Results;

internal static class RequiredActionContinuationMapper
{
    public static ToolContinuation? Map(RequiredAction? requiredAction)
    {
        if (requiredAction is null)
        {
            return null;
        }

        return requiredAction.Value switch
        {
            RequiredAction.OpenWorkspace => ToolContinuation.CallTool(
                ServerOwnedToolRegistration.WorkspaceOpenName,
                "Open a workspace before retrying the request."),
            RequiredAction.StartTransaction => ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionStartName,
                "Start a transaction before retrying the request."),
            RequiredAction.RollbackTransaction => ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionRollbackName,
                "Roll back the current transaction before continuing."),
            RequiredAction.ReloadWorkspace => ToolContinuation.CallTool(
                ServerOwnedToolRegistration.WorkspaceReloadName,
                "Reload the workspace before retrying the request."),
            RequiredAction.ResolveTargetAgain => ToolContinuation.ReviseRequest(
                "Resolve the target against the current workspace snapshot, replace the stale selector and snapshot precondition, then retry the request."),
            RequiredAction.CommitOrRollback => ToolContinuation.ChooseTool(
                [
                    ServerOwnedToolRegistration.TransactionCommitName,
                    ServerOwnedToolRegistration.TransactionRollbackName,
                ],
                "Finish the active transaction by either committing its staged changes or discarding them before continuing."),
            RequiredAction.ReduceTransactionHistory => ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionHistoryName,
                "Undo at least one staged revision before retrying the mutation; the retried mutation will replace the discarded redo branch."),
            RequiredAction.Retry => ToolContinuation.RetryRequest(
                "Retry the same request."),
            RequiredAction.ResolveRecovery => ToolContinuation.ResolveExternally(
                "Resolve the unfinished recovery state in the Host state directory or affected workspace before retrying the request."),
            RequiredAction.NarrowRequest => ToolContinuation.ReviseRequest(
                "Reduce the scope or requested change limit, then retry the request."),
            _ => throw new InvalidOperationException($"Required action '{requiredAction}' does not have a published continuation mapping."),
        };
    }
}
